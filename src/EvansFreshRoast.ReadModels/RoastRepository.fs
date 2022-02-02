namespace EvansFreshRoast.ReadModels

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils
open Npgsql.FSharp
open NodaTime
open System
open System.Globalization

type RoastSummaryView =
    { Id: Id<Roast>
      Name: RoastName
      RoastDate: LocalDate
      OrderByDate: LocalDate
      CustomersCount: NonNegativeInt
      Coffees: list<Id<Coffee> * CoffeeName>
      RoastStatus: RoastStatus
      OrdersCount: NonNegativeInt }

type RoastDetailedView =
    { Id: Id<Roast>
      Name: RoastName
      RoastDate: LocalDate
      OrderByDate: LocalDate
      Customers: Id<Customer> list
      Coffees: Id<Coffee> list
      Orders: Order list
      Status: RoastStatus
      SentRemindersCount: NonNegativeInt }

module RoastRepository =
    let private awaitIgnoreOk task =
        async {
            return! task
            |> Async.AwaitTask
            |> Async.Ignore
            |> Async.map Ok
        }

    let updateRoast connectionString (event: DomainEvent<Roast, Event>) =
        let connection = Sql.connect <| ConnectionString.value connectionString
        let roastUuid = Sql.uuid <| Id.value event.AggregateId

        match event.Body with
        | Created fields ->
            let insertRoastSql =
                """
                INSERT INTO roasts(
                    roast_id
                  , roast_name
                  , roast_date
                  , order_by_date
                  , customer_ids
                  , coffee_ids
                  , roast_status
                ) VALUES (
                    @roastId
                  , @name
                  , @date::date
                  , @orderByDate::date
                  , '{}'
                  , '{}'
                  , 'NotPublished'
                )
                """

            async {
                try
                    return! connection
                    |> Sql.query insertRoastSql
                    |> Sql.parameters
                        [ "roastId", roastUuid
                          "name", fields.Name |> RoastName.value |> Sql.string
                          "date", fields.RoastDate.ToString("R", null) |> Sql.string
                          "orderByDate", fields.OrderByDate.ToString("R", null) |> Sql.string ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex -> return Error <| exn("Error inserting roast.", ex)
            }

        | OrderPlaced order ->
            let insertOrderSql =
                """
                CREATE TEMP TABLE tmp_inserted_order_ids(id BIGINT NOT NULL);

                WITH inserted_order_ids AS (
                    INSERT INTO orders(customer_id, placed_time, roast_fk)
                    VALUES (@customerId, @placedTime, @roastId)
                    RETURNING order_id
                )
                INSERT INTO tmp_inserted_order_ids(id)
                    SELECT order_id FROM inserted_order_ids;
                """

            let insertOrderParams =
                [ [ "customerId", order.CustomerId |> Id.value |> Sql.uuid
                    "placedTime", order.Timestamp.ToDateTimeOffset() |> Sql.timestamptz
                    "roastId", roastUuid ] ]

            let insertOrderLineItemSql =
                """
                INSERT INTO order_line_items(order_fk, coffee_id, quantity)
                VALUES (
                    (SELECT id FROM tmp_inserted_order_ids LIMIT 1)
                  , @coffeeId
                  , @quantity
                )
                """

            let insertOrderLineItemParams =
                order.LineItems
                |> Seq.map (fun kvp ->
                    [ "coffeeId", kvp.Key |> Id.value |> Sql.uuid
                      "quantity", kvp.Value |> Quantity.value |> Sql.int ])
                |> List.ofSeq

            async {
                try
                    return! connection
                    |> Sql.executeTransactionAsync
                        [ insertOrderSql, insertOrderParams
                          insertOrderLineItemSql, insertOrderLineItemParams ]
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error adding order to roast.", ex)
            }

        | OrderCancelled customerId ->
            let deleteOrderSql =
                """
                WITH
                delete_order_ids AS (
                    SELECT order_id AS id FROM orders
                    WHERE customer_id = @customerId
                    AND roast_fk = @roastId
                ),
                deleted_order_line_item_ids AS (
                    DELETE FROM order_line_items
                    WHERE order_fk IN(SELECT id FROM delete_order_ids)
                    RETURNING order_line_item_id
                )
                DELETE FROM orders
                WHERE order_id IN(SELECT id FROM delete_order_ids)
                """

            async {
                try
                    return! connection
                    |> Sql.query deleteOrderSql
                    |> Sql.parameters
                        [ "customerId", customerId |> Id.value |> Sql.uuid
                          "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error deleting cancelled order.", ex)
            }

        | OrderConfirmed(customerId, invoiceAmt) ->
            let getOrderIdSql =
                """
                SELECT order_id
                FROM roasts
                JOIN orders ON roast_fk = roast_id
                WHERE roast_id = @roastId
                AND customer_id = @customerId
                """

            let createInvoiceSql =
                """
                WITH inserted_invoice_ids AS (
                    INSERT INTO invoices(invoice_amount)
                    VALUES (@invoiceAmount)
                    RETURNING invoice_id
                )
                UPDATE orders
                SET invoice_fk = (SELECT invoice_id FROM inserted_invoice_ids LIMIT 1)
                WHERE order_id = @orderId
                """

            async {
                let getOrderId =
                    connection
                    |> Sql.query getOrderIdSql
                    |> Sql.parameters
                        [ "roastId", roastUuid
                          "customerId", customerId |> Id.value |> Sql.uuid ]
                    |> Sql.executeAsync (fun row -> row.int64 "order_id")
                    |> Async.AwaitTask

                let insertInvoice orderId =
                    connection
                    |> Sql.query createInvoiceSql
                    |> Sql.parameters
                        [ "invoiceAmount", UsdInvoiceAmount.value invoiceAmt |> Sql.decimal
                          "orderId", Sql.int64 orderId ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk

                try
                    return! getOrderId
                    |> Async.map List.tryHead
                    |> Async.bind (
                        Option.map insertInvoice
                        >> Option.defaultValue (
                            Error <| exn("No order found for customer id.")
                            |> Async.lift
                        )
                    )
                with
                | ex ->
                    return Error <| exn("Error creating invoice.", ex)
            }

        | InvoicePaid(customerId, paymentMethod) ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        WITH invoice_ids AS(
                            SELECT invoice_id
                            FROM roasts
                            JOIN orders ON roast_fk = roast_id
                            JOIN invoices ON invoice_id = invoice_fk
                            WHERE roast_id = @roastId
                            AND customer_id = @customerId
                        )
                        UPDATE invoices
                        SET payment_method = @paymentMethod
                        WHERE invoice_id = (SELECT invoice_id FROM invoice_ids LIMIT 1)
                        """
                    |> Sql.parameters
                        [ "roastId", roastUuid
                          "customerId", customerId |> Id.value |> Sql.uuid
                          "paymentMethod", paymentMethod |> string |> Sql.string ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error setting payment method on invoice", ex)
            }

        | ReminderSent ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE roasts
                        SET reminders_sent_count = reminders_sent_count + 1
                        WHERE roast_id = @roastId
                        """
                    |> Sql.parameters [ "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error incrementing the count of reminders sent.", ex)
            }

        | CoffeesAdded coffeeIds ->
            async {
                try
                    return! connection
                    |> Sql.executeTransactionAsync [
                        """
                        UPDATE roasts
                        SET coffee_ids = array_append(coffee_ids, @coffeeId)
                        WHERE roast_id = @roastId
                        """,
                        coffeeIds
                        |> List.map (fun coffeeId ->
                            [ "coffeeId", coffeeId |> Id.value |> Sql.uuid
                              "roastId", roastUuid ])
                    ]
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error adding coffees to roast.", ex)
            }

        | CoffeesRemoved coffeeIds ->
            async {
                try
                    return! connection
                    |> Sql.executeTransactionAsync [
                        """
                        UPDATE roasts
                        SET coffee_ids = ARRAY_REMOVE(coffee_ids, @coffeeId)
                        WHERE roast_id = @roastId
                        """,
                        coffeeIds
                        |> List.map (fun cId ->
                            [ "coffeeId", cId |> Id.value |> Sql.uuid
                              "roastId", roastUuid ])
                    ]
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error removing coffee ids.", ex)
            }

        | CustomersAdded customerIds ->
            async {
                try
                    return! connection
                    |> Sql.executeTransactionAsync [
                        """
                        UPDATE roasts
                        SET customer_ids = array_append(customer_ids, @customerId)
                        WHERE roast_id = @roastId
                        """,
                        customerIds
                        |> List.map (fun customerId ->
                            [ "customerId", customerId |> Id.value |> Sql.uuid
                              "roastId", roastUuid ])
                    ]
                    |> awaitIgnoreOk
                    
                with
                | ex ->
                    return Error <| exn("Error adding customers to roast.", ex)
            }

        | CustomersRemoved customerIds ->
            async {
                try
                    return! connection
                    |> Sql.executeTransactionAsync [
                        """
                        UPDATE roasts
                        SET customer_ids = ARRAY_REMOVE(customer_ids, @customerId)
                        WHERE roast_id = @roastId
                        """,
                        customerIds
                        |> List.map (fun cId ->
                            [ "customerId", cId |> Id.value |> Sql.uuid
                              "roastId", roastUuid ])
                    ]
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error removing customer ids.", ex)
            }

        | RoastDatesChanged (roastDate, orderByDate) ->
            let formatDate (dt: LocalDate) =
                dt.ToString("R", CultureInfo.InvariantCulture)

            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE roasts
                        SET roast_date = @roastDate
                          , order_by_date = @orderByDate
                        WHERE roast_id = @roastId
                        """
                    |> Sql.parameters
                        [ "roastDate", roastDate |> formatDate |> Sql.string
                          "orderByDate", orderByDate |> formatDate |> Sql.string
                          "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error updating roast dates.", ex)
            }

        | RoastStarted _ ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE roasts
                        SET roast_status = 'Open'
                        WHERE roast_id = @roastId
                        """
                    |> Sql.parameters [ "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error updating roast status.", ex)
            }

        | RoastCompleted ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE roasts
                        SET roast_status = 'Closed'
                        WHERE roast_id = @roastId
                        """
                    |> Sql.parameters [ "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | ex ->
                    return Error <| exn("Error updating roast status.", ex)
            }

    type private DbRoastRow =
        { RoastId: Guid
          RoastName: string
          RoastDate: DateTime
          OrderByDate: DateTime
          CustomerIds: Guid[]
          CoffeeIds: Guid[]
          RoastStatus: string
          RemindersSentCount: int
          OrderId: int64 option
          CustomerId: Guid option
          PlacedTime: DateTimeOffset option
          OrderLineItemId: int64 option
          CoffeeId: Guid option
          Quantity: int option
          InvoiceAmount: decimal option
          PaymentMethod: string option }

    let parseRoastStatus =
        function
        | "NotPublished" -> NotPublished
        | "Open" -> Open
        | "Closed" -> Closed
        | _ -> failwith "Invalid roast status."

    let getRoast connectionString roastId =
        let connection = Sql.connect <| ConnectionString.value connectionString

        let selectRoastSql =
            """
            SELECT
                roast_id
              , roast_name
              , roast_date
              , order_by_date
              , customer_ids
              , coffee_ids
              , roast_status
              , reminders_sent_count
              , order_id
              , customer_id
              , placed_time
              , order_line_item_id
              , coffee_id
              , quantity
              , invoice_amount
              , payment_method
            FROM roasts
            LEFT JOIN orders ON roast_fk = roast_id
            LEFT JOIN order_line_items ON order_fk = order_id
            LEFT JOIN invoices ON invoice_id = invoice_fk
            WHERE roast_id = @roastId
            """

        let readRoastRow (row: RowReader) =
            { RoastId = row.uuid "roast_id"
              RoastName = row.string "roast_name"
              RoastDate = row.dateTime "roast_date"
              OrderByDate = row.dateTime "order_by_date"
              CustomerIds = row.uuidArray "customer_ids"
              CoffeeIds = row.uuidArray "coffee_ids"
              RoastStatus = row.string "roast_status"
              RemindersSentCount = row.int "reminders_sent_count"
              OrderId = row.int64OrNone "order_id"
              CustomerId = row.uuidOrNone "customer_id"
              PlacedTime = row.datetimeOffsetOrNone "placed_time"
              OrderLineItemId = row.int64OrNone "order_line_item_id"
              CoffeeId = row.uuidOrNone "coffee_id"
              Quantity = row.intOrNone "quantity"
              InvoiceAmount = row.decimalOrNone "invoice_amount"
              PaymentMethod = row.stringOrNone "payment_method" }

        let mapDbRoastRow dbRoastRow orders =
            { Id = dbRoastRow.RoastId |> Id.create |> unsafeAssertOk
              Name = dbRoastRow.RoastName |> RoastName.create |> unsafeAssertOk
              RoastDate = LocalDate.FromDateTime(dbRoastRow.RoastDate)
              OrderByDate = LocalDate.FromDateTime(dbRoastRow.OrderByDate)
              Customers =
                dbRoastRow.CustomerIds
                |> Array.map (Id.create >> unsafeAssertOk)
                |> List.ofArray
              Coffees =
                dbRoastRow.CoffeeIds
                |> Array.map (Id.create >> unsafeAssertOk)
                |> List.ofArray
              Orders = orders
              Status = dbRoastRow.RoastStatus |> parseRoastStatus
              SentRemindersCount = dbRoastRow.RemindersSentCount |> NonNegativeInt.create |> unsafeAssertOk }

        let convertResultsToDetailedRoastView (rows: DbRoastRow list) =
            let roastsWithNoOrders =
                rows
                |> List.filter (fun r -> Option.isNone r.OrderId)
                |> List.map (fun r -> mapDbRoastRow r [])

            let rowsWithOrders =
                rows
                |> List.filter (fun r -> Option.isSome r.OrderId)

            rowsWithOrders
            |> List.groupBy (fun r -> r.RoastId)
            |> List.map (fun (_, roastGroup) ->
                let orders =
                    roastGroup
                    |> List.groupBy (fun rgr -> rgr.OrderId.Value)
                    |> List.map (fun (_, orderGroup) ->
                        let orderDetails =
                            { CustomerId = orderGroup.Head.CustomerId.Value |> Id.create |> unsafeAssertOk
                              Timestamp = OffsetDateTime.FromDateTimeOffset(orderGroup.Head.PlacedTime.Value)
                              LineItems =
                                orderGroup
                                |> List.map (fun x ->
                                    let coffeeId: Id<Coffee> = x.CoffeeId.Value |> Id.create |> unsafeAssertOk
                                    let quantity = x.Quantity.Value |> Quantity.create |> unsafeAssertOk
                                    
                                    coffeeId, quantity)
                                |> dict }

                        let isConfirmed = Option.isSome orderGroup.Head.InvoiceAmount
                        let isPaid = Option.isSome orderGroup.Head.PaymentMethod
                        
                        match isConfirmed, isPaid with
                        | true, true ->
                            let invoiceAmt =
                                orderGroup.Head.InvoiceAmount.Value
                                |> UsdInvoiceAmount.create
                                |> unsafeAssertOk

                            let paymentMethod =
                                orderGroup.Head.PaymentMethod.Value
                                |> function
                                    | "Unknown" -> Unknown
                                    | "Venmo" -> Venmo
                                    | "Cash" -> Cash
                                    | "Check" -> Check
                                    | _ -> failwith "Error parsing payment method."

                            ConfirmedOrder(orderDetails, PaidInvoice(invoiceAmt, paymentMethod))

                        | true, _ ->
                            let invoiceAmt =
                                orderGroup.Head.InvoiceAmount.Value
                                |> UsdInvoiceAmount.create
                                |> unsafeAssertOk

                            ConfirmedOrder(orderDetails, UnpaidInvoice(invoiceAmt))

                        | _ ->
                            UnconfirmedOrder orderDetails)

                
                mapDbRoastRow roastGroup.Head orders
            )
            |> List.append roastsWithNoOrders

        async {
            return! connection
            |> Sql.query selectRoastSql
            |> Sql.parameters [ "roastId", roastId |> Id.value |> Sql.uuid ]
            |> Sql.executeAsync readRoastRow
            |> Async.AwaitTask
            |> Async.map (
                convertResultsToDetailedRoastView
                >> List.tryHead
            )
        }

    let getAllRoasts connectionString (getAllCoffees: unit -> Async<(Id<Coffee> * Coffee) list>) =
        let toLocalDate (dt: DateTime) = LocalDate.FromDateTime(dt)
        
        async {
            let! allCoffees = getAllCoffees()

            return! (ConnectionString.value connectionString)
            |> Sql.connect
            |> Sql.query
                """
                SELECT
                    roast_id
                  , roast_name
                  , roast_date
                  , order_by_date
                  , COALESCE(ARRAY_LENGTH(customer_ids, 1), 0) AS customers_count
                  , coffee_ids
                  , roast_status
                  , COUNT(order_id) AS orders_count
                FROM roasts
                LEFT JOIN orders ON roast_fk = roast_id
                GROUP BY roast_id
                """
            |> Sql.executeAsync (fun row ->
                let rowCoffeeIds = row.uuidArray "coffee_ids"

                { Id = row.uuid "roast_id" |> Id.create |> unsafeAssertOk
                  Name = row.string "roast_name" |> RoastName.create |> unsafeAssertOk
                  RoastDate = row.dateTime "roast_date" |> toLocalDate
                  OrderByDate = row.dateTime "order_by_date" |> toLocalDate
                  CustomersCount = row.int "customers_count" |> NonNegativeInt.create |> unsafeAssertOk
                  RoastStatus = row.string "roast_status" |> parseRoastStatus
                  OrdersCount = row.int "orders_count" |> NonNegativeInt.create |> unsafeAssertOk
                  Coffees =
                    allCoffees
                    |> List.filter (fun (coffeeId, _) ->
                        rowCoffeeIds |> Seq.exists ((=) (coffeeId |> Id.value)))
                    |> List.map (fun (coffeeId, coffee) -> coffeeId, coffee.Name) })
            |> Async.AwaitTask
        }
