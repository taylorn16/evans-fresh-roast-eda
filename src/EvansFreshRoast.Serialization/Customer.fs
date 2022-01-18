module EvansFreshRoast.Serialization.Customer

open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Domain.DomainTypes
open Thoth.Json.Net

let encodeCustomerEvent event =
    match event with
    | Updated fields ->
        Encode.object [ if fields.Name.IsSome then
                            "name", Encode.string (CustomerName.value fields.Name.Value)
                        if fields.PhoneNumber.IsSome then
                            "phoneNumber", Encode.string (UsPhoneNumber.value fields.PhoneNumber.Value) ]

    | Event.Subscribed -> Encode.string "subscribed"

    | Event.Unsubscribed -> Encode.string "unsubscribed"

let decodeCustomerName: Decoder<CustomerName> =
    Decode.string
    |> Decode.andThen (
        CustomerName.create
        >> function
            | Ok name -> Decode.succeed name
            | Error err -> Decode.fail $"{err}"
    )

let decodePhoneNumber: Decoder<UsPhoneNumber> =
    Decode.string
    |> Decode.andThen (
        UsPhoneNumber.create
        >> function
            | Ok phone -> Decode.succeed phone
            | Error err -> Decode.fail $"{err}"
    )

let decodeSubscribedOrUnsubscribed: Decoder<Event> =
    Decode.string
    |> Decode.andThen (function
        | "subscribed" -> Decode.succeed Event.Subscribed
        | "unsubscribed" -> Decode.succeed Event.Unsubscribed
        | s -> Decode.fail $"expected 'subscribed' or 'unsubscribed', got {s}")

let decodeUpdated: Decoder<Event> =
    Decode.map2
        (fun name phoneNumber ->
            Updated
                { Name = name
                  PhoneNumber = phoneNumber })
        (Decode.optional "name" decodeCustomerName)
        (Decode.optional "phoneNumber" decodePhoneNumber)

let decodeCustomerEvent: Decoder<Event> =
    Decode.oneOf [ decodeUpdated
                   decodeSubscribedOrUnsubscribed ]
