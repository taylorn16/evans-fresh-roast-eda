module EvansFreshRoast.Serialization.Roast

open Thoth.Json.Net
open EvansFreshRoast.Domain.BaseTypes
open EvansFreshRoast.Domain.DomainTypes
open EvansFreshRoast.Domain.Roast
open NodaTime
open System.Text.RegularExpressions
open EvansFreshRoast.Serialization.Common

let encodeRoastEvent event =
    let encodeLineItem (coffeeId, quantity) =
        Encode.object [ "coffeeId", Encode.guid <| Id.value coffeeId
                        "quantity", Encode.int <| Quantity.value quantity ]

    let toTuple (kvp: System.Collections.Generic.KeyValuePair<'a, 'b>) = kvp.Key, kvp.Value

    match event with
    | OrderPlaced details ->
        Encode.object [ "customerId", Encode.guid <| Id.value details.CustomerId
                        "timestamp", encodeOffsetDateTime details.Timestamp
                        "lineItems",
                        Encode.array (
                            details.LineItems
                            |> Seq.map (toTuple >> encodeLineItem)
                            |> Seq.toArray
                        ) ]
    | OrderCancelled customerId ->
        Encode.object [ "customerId", Encode.guid <| Id.value customerId
                        "action", Encode.string "cancelled" ]
    | OrderConfirmed customerId ->
        Encode.object [ "customerId", Encode.guid <| Id.value customerId
                        "action", Encode.string "confirmed" ]
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
    | RoastStarted -> Encode.string "roastStarted"
    | RoastCompleted -> Encode.string "roastCompleted"

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

let decodeOrderCancelledOrConfirmed: Decoder<Event> =
    let decodeCustomerId = Decode.field "customerId" decodeId

    Decode.field "action" Decode.string
    |> Decode.andThen (function
        | "cancelled" -> decodeCustomerId |> Decode.map OrderCancelled
        | "confirmed" -> decodeCustomerId |> Decode.map OrderConfirmed
        | _ -> Decode.fail "should have been cancelled or confirmed")

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

let decodeRoastEvent: Decoder<Event> =
    Decode.oneOf [ decodeOrderPlaced
                   decodeOrderCancelledOrConfirmed
                   decodeCoffeesAdded
                   decodeCustomersAdded
                   decodeRoastDatesChanged
                   decodeRoastStartedOrCompleted ]
