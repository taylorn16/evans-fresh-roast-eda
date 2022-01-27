namespace EvansFreshRoast.ReadModels

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils
open Npgsql.FSharp
open NodaTime

module RoastRepository =
    open System.Globalization
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
                  , 'NotStarted'
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
                | _ -> return Error "Error inserting roast."
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
                | _ -> return Error "Error adding order to roast."
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
                | _ -> return Error "Error deleting cancelled order."
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
                            Error "No order found for customer id."
                            |> Async.lift
                        )
                    )
                with
                | _ -> return Error "Error creating invoice."
            }

        | InvoicePaid _ ->
            async { return Ok () }

        | ReminderSent ->
            async { return Ok () }

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
                | _ -> return Error "Error adding coffees to roast."
            }

        | CoffeesRemoved coffeeIds ->
            async { return Ok () }

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
                | _ -> return Error "Error adding customers to roast."
            }

        | CustomersRemoved customerIds ->
            async { return Ok () }

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
                | _ -> return Error "Error updating roast dates."
            }

        | RoastStarted ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE roasts
                        SET roast_status = 'InProcess'
                        WHERE roast_id = @roastId
                        """
                    |> Sql.parameters [ "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | _ -> return Error "Error updating roast status."
            }

        | RoastCompleted ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE roasts
                        SET roast_status = 'Complete'
                        WHERE roast_id = @roastId
                        """
                    |> Sql.parameters [ "roastId", roastUuid ]
                    |> Sql.executeNonQueryAsync
                    |> awaitIgnoreOk
                with
                | _ -> return Error "Error updating roast status."
            }

    let getRoast connectionString roastId =
        async {
            return None
        }

    let getAllRoasts connectionString =
        async {
            return []
        }
