module EvansFreshRoast.Serialization.Customer

open Thoth.Json.Net
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer

let encodeCustomerEvent event =
    match event with
    | Created fields ->
        Encode.object
            [ "$$event", Encode.string "created"
              "name", Encode.string <| CustomerName.value fields.Name
              "phoneNumber", Encode.string <| UsPhoneNumber.value fields.PhoneNumber ]
    | Updated fields ->
        Encode.object
            [ "$$event", Encode.string "created"
              if fields.Name.IsSome then
                  "name", Encode.string <| CustomerName.value fields.Name.Value
              if fields.PhoneNumber.IsSome then
                  "phoneNumber", Encode.string <| UsPhoneNumber.value fields.PhoneNumber.Value ]

    | Subscribed -> Encode.string "subscribed"

    | Unsubscribed -> Encode.string "unsubscribed"

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

let decodeSubscribedUnsubscribed: Decoder<Event> =
    Decode.string
    |> Decode.andThen (function
        | "subscribed" -> Decode.succeed Subscribed
        | "unsubscribed" -> Decode.succeed Unsubscribed
        | s -> Decode.fail $"expected 'subscribed' or 'unsubscribed', got '{s}'.")

let decodeCreated: Decoder<Event> =
    Decode.map2
        (fun name phoneNumber ->
            Created
                { Name = name
                  PhoneNumber = phoneNumber })
        (Decode.field "name" decodeCustomerName)
        (Decode.field "phoneNumber" decodePhoneNumber)

let decodeUpdated: Decoder<Event> =
    Decode.map2
        (fun name phoneNumber ->
            Updated
                { Name = name
                  PhoneNumber = phoneNumber })
        (Decode.optional "name" decodeCustomerName)
        (Decode.optional "phoneNumber" decodePhoneNumber)

let decodeUpdatedCreated: Decoder<Event> =
    Decode.field "$$event" Decode.string
    |> Decode.andThen (
        function
        | "created" -> decodeCreated
        | "updated" -> decodeUpdated
        | s -> Decode.fail $"Expected 'created' or 'updated' but got '{s}'."
    )

let decodeCustomerEvent: Decoder<Event> =
    Decode.oneOf [ decodeUpdatedCreated
                   decodeSubscribedUnsubscribed ]
