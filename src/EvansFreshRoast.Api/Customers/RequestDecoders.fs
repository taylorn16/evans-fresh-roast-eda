module EvansFreshRoast.Api.Customers.RequestDecoders

open Thoth.Json.Net
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Serialization.Customer

let decodeCreateCustomerCmd: Decoder<Command> =
    Decode.map2
        (fun nm phn ->
            { Name = nm
              PhoneNumber = phn }: CustomerCreateFields)
        (Decode.field "name" decodeCustomerName)
        (Decode.field "phoneNumber" decodePhoneNumber)
    |> Decode.map Create
