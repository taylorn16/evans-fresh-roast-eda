module EvansFreshRoast.Serialization.Roast

open Thoth.Json.Net
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
                    "timestamp", encodeOffsetDateTime order.Timestamp
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
                        "roastDate", encodeLocalDate fields.RoastDate
                        "orderByDate", encodeLocalDate fields.OrderByDate ]

    | OrderPlaced details ->
        encodeOrderDetails details

    | OrderCancelled customerId ->
        Encode.object [ "customerId", Encode.guid <| Id.value customerId ]

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
                        Encode.array (
                            customerIds
                            |> Seq.map (Id.value >> Encode.guid)
                            |> Seq.toArray
                        ) ]

    | RoastDatesChanged (roastDate, orderByDate) ->
        Encode.object [ "roastDate", encodeLocalDate roastDate
                        "orderByDate", encodeLocalDate orderByDate ]

    | RoastStarted ->
        Encode.string "roastStarted"

    | RoastCompleted ->
        Encode.string "roastCompleted"

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
        (Decode.field "timestamp" decodeOffsetDateTime)
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

let decodeOrderCancelled: Decoder<Event> =
    Decode.map OrderCancelled (Decode.field "customerId" decodeId)

let decodeCoffeesAdded: Decoder<Event> =
    Decode.field
        "addedCoffeeIds"
        (Decode.array decodeId
         |> Decode.map (Array.toList >> CoffeesAdded))

let decodeCustomersAdded: Decoder<Event> =
    Decode.field
        "addedCustomerIds"
        (Decode.array decodeId
         |> Decode.map (Array.toList >> CustomersAdded))

let decodeRoastDatesChanged: Decoder<Event> =
    Decode.map2
        (fun roastDate orderByDate -> RoastDatesChanged(roastDate, orderByDate))
        (Decode.field "roastDate" decodeLocalDate)
        (Decode.field "orderByDate" decodeLocalDate)

let decodeRoastStartedOrCompleted: Decoder<Event> =
    Decode.string
    |> Decode.andThen (function
        | "roastStarted" -> Decode.succeed RoastStarted
        | "roastCompleted" -> Decode.succeed RoastCompleted
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
        (Decode.field "roastDate" decodeLocalDate)
        (Decode.field "orderByDate" decodeLocalDate)

let decodeRoastEvent: Decoder<Event> =
    Decode.oneOf [ decodeOrderPlaced
                   decodeOrderConfirmed
                   decodeOrderCancelled
                   decodeCoffeesAdded
                   decodeCustomersAdded
                   decodeRoastDatesChanged
                   decodeRoastStartedOrCompleted
                   decodeCreated ]
