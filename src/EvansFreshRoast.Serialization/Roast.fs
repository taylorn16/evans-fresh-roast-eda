module EvansFreshRoast.Serialization.Roast

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open System.Collections.Generic
open EvansFreshRoast.Serialization.Common

let encodeLineItem (coffeeId: Id<Coffee>, quantity) =
    Encode.object [ "coffeeId", Encode.guid <| Id.value coffeeId
                    "quantity", Encode.int <| Quantity.value quantity ]

let encodeOrderDetails (order: OrderDetails) =
    let tuple (kvp: KeyValuePair<'a, 'b>) = kvp.Key, kvp.Value
    
    Encode.object [ "customerId", Encode.guid <| Id.value order.CustomerId
                    "timestamp", Encode.datetimeOffset order.Timestamp
                    "lineItems",
                    Encode.array (
                        order.LineItems
                        |> Seq.map (tuple >> encodeLineItem)
                        |> Seq.toArray
                    ) ]

let encodeRoastEvent event =
    match event with
    | Created fields ->
        Encode.object [ "name", Encode.string <| RoastName.value fields.Name
                        "roastDate", Encode.datetime fields.RoastDate
                        "orderByDate", Encode.datetime fields.OrderByDate ]

    | OrderPlaced details ->
        encodeOrderDetails details

    | OrderCancelled customerId ->
        Encode.object [ "customerId", Encode.guid <| Id.value customerId
                        "$$event", Encode.string "orderCancelled" ]

    | OrderConfirmed(customerId, invoiceAmt) ->
        Encode.object [ "customerId", Encode.guid <| Id.value customerId
                        "invoiceAmt", Encode.decimal <| UsdInvoiceAmount.value invoiceAmt ]
                        
    | CoffeesAdded coffeeIds ->
        Encode.object [ "addedCoffeeIds",
                        Encode.array (
                            coffeeIds
                            |> Seq.map (Id.value >> Encode.guid)
                            |> Seq.toArray
                        ) ]

    | CustomersAdded customerIds ->
        Encode.object [ "addedCustomerIds",
                        Encode.list (
                            customerIds
                            |> List.map (Id.value >> Encode.guid)
                        ) ]

    | RoastDatesChanged (roastDate, orderByDate) ->
        Encode.object [ "roastDate", Encode.datetime roastDate
                        "orderByDate", Encode.datetime orderByDate ]

    | RoastStarted summary ->
        Encode.object [ "summary", Encode.string summary ]

    | RoastCompleted ->
        Encode.string "roastCompleted"

    | CustomersRemoved customerIds ->
        Encode.object [ "removedCustomerIds",
                        Encode.list (
                            customerIds
                            |> List.map (Id.value >> Encode.guid)
                        ) ]

    | CoffeesRemoved coffeeIds ->
        Encode.object [ "removedCoffeeIds",
                        Encode.list (
                            coffeeIds
                            |> List.map (Id.value >> Encode.guid)
                        ) ]

    | ReminderSent ->
        Encode.string "reminderSent"

    | InvoicePaid(customerId, paymentMethod) ->
        Encode.object [ "customerId", customerId |> Id.value |> Encode.guid
                        "paymentMethod", paymentMethod |> string |> Encode.string ]

let decodePaymentMethod: Decoder<PaymentMethod> =
    Decode.string
    |> Decode.andThen (
        function
        | "Unknown" -> Decode.succeed Unknown
        | "Venmo" -> Decode.succeed Venmo
        | "Cash" -> Decode.succeed Cash
        | "Check" -> Decode.succeed Check
        | s -> Decode.fail $"'{s}' is not a valid payment method"
    )

let decodeInvoicePaid: Decoder<Event> =
    Decode.map2
        (fun customerId paymentMethod -> InvoicePaid(customerId, paymentMethod))
        (Decode.field "customerId" decodeId)
        (Decode.field "paymentMethod" decodePaymentMethod)

let decodeQuantity: Decoder<Quantity> =
    let parseQuantity qty =
        Quantity.create qty
        |> function
            | Ok q -> Decode.succeed q
            | Error e -> Decode.fail $"{e}"

    Decode.int |> Decode.andThen parseQuantity

let decodeOrderPlaced: Decoder<Event> =
    let decodeLineItem =
        Decode.map2
            (fun coffeeId quantity -> coffeeId, quantity)
            (Decode.field "coffeeId" decodeId)
            (Decode.field "quantity" decodeQuantity)

    Decode.map3
        (fun customerId timestamp lineItems ->
            OrderPlaced(
                { CustomerId = customerId
                  Timestamp = timestamp
                  LineItems = dict lineItems }
            ))
        (Decode.field "customerId" decodeId)
        (Decode.field "timestamp" Decode.datetimeOffset)
        (Decode.field "lineItems" (Decode.array decodeLineItem))

let decodeInvoiceAmount: Decoder<UsdInvoiceAmount> =
    Decode.decimal
    |> Decode.andThen (
        UsdInvoiceAmount.create
        >> function
            | Ok invoiceAmt -> Decode.succeed invoiceAmt
            | Error e -> Decode.fail $"{e}"
    )

let decodeOrderConfirmed: Decoder<Event> =
    Decode.map2
        (fun customerId invoiceAmt -> OrderConfirmed(customerId, invoiceAmt))
        (Decode.field "customerId" decodeId)
        (Decode.field "invoiceAmt" decodeInvoiceAmount)

let decodeExactStr (exp: string): Decoder<string> =
    Decode.string
    |> Decode.andThen (fun s ->
        if s = exp then
            Decode.succeed s
        else
            Decode.fail $"Expecting {exp}, got {s}."
    )

let decodeOrderCancelled: Decoder<Event> =
    Decode.map2
        (fun customerId _ -> OrderCancelled customerId)
        (Decode.field "customerId" decodeId)
        (Decode.field "$$event" (decodeExactStr "orderCancelled"))

let decodeCoffeesAdded: Decoder<Event> =
    Decode.field
        "addedCoffeeIds"
        (Decode.array decodeId
         |> Decode.map (Array.toList >> CoffeesAdded))

let decodeCoffeesRemoved: Decoder<Event> =
    Decode.field
        "removedCoffeeIds"
        (Decode.array decodeId
         |> Decode.map (Array.toList >> CoffeesRemoved))

let decodeCustomersAdded: Decoder<Event> =
    Decode.field
        "addedCustomerIds"
        (Decode.array decodeId
         |> Decode.map (Array.toList >> CustomersAdded))

let decodeCustomersRemoved: Decoder<Event> =
    Decode.field
        "removedCustomerIds"
        (Decode.array decodeId
         |> Decode.map (Array.toList >> CustomersRemoved))

let decodeRoastDatesChanged: Decoder<Event> =
    Decode.map2
        (fun roastDate orderByDate -> RoastDatesChanged(roastDate, orderByDate))
        (Decode.field "roastDate" Decode.datetime)
        (Decode.field "orderByDate" Decode.datetime)

let decodeRoastCompletedOrReminderSent: Decoder<Event> =
    Decode.string
    |> Decode.andThen (function
        | "roastCompleted" -> Decode.succeed RoastCompleted
        | "reminderSent" -> Decode.succeed ReminderSent
        | _ -> Decode.fail "expected roastStarted or roastCompleted not some other random string")

let decodeRoastName: Decoder<RoastName> =
    Decode.string
    |> Decode.andThen (
        RoastName.create
        >> function
            | Ok name -> Decode.succeed name
            | Error e -> Decode.fail $"{e}"
    )

let decodeCreated: Decoder<Event> =
    Decode.map3
        (fun nm rdt obdt ->
            Created { Name = nm
                      RoastDate = rdt
                      OrderByDate = obdt })
        (Decode.field "name" decodeRoastName)
        (Decode.field "roastDate" Decode.datetime)
        (Decode.field "orderByDate" Decode.datetime)

let decodeRoastStarted: Decoder<Event> =
    Decode.field "summary" Decode.string
    |> Decode.map RoastStarted

let decodeRoastEvent: Decoder<Event> =
    Decode.oneOf [ decodeCreated
                   decodeInvoicePaid
                   decodeOrderPlaced
                   decodeOrderConfirmed
                   decodeCoffeesAdded
                   decodeCoffeesRemoved
                   decodeCustomersAdded
                   decodeCustomersRemoved
                   decodeRoastDatesChanged
                   decodeRoastCompletedOrReminderSent
                   decodeRoastStarted
                   decodeOrderCancelled ]
