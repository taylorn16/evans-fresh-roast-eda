namespace EvansFreshRoast.Dto

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open EvansFreshRoast.Domain

[<CLIMutable>]
type CreateCustomerRequest =
    { Name: string
      PhoneNumber: string }

[<CLIMutable>]
type Customer =
    { Id: System.Guid
      Name: string
      PhoneNumber: string
      Status: CustomerStatus }
    static member encode customer =
        Encode.object [ "id", Encode.guid customer.Id
                        "name", Encode.string customer.Name
                        "phoneNumber", Encode.string customer.PhoneNumber
                        "status", Encode.string <| string customer.Status ]
        
    static member decoder: Decoder<Customer> =
        let decodeCustomerStatus =
            Decode.string
            |> Decode.andThen (
                function
                | "Unconfirmed" -> Decode.succeed Unconfirmed
                | "Unsubscribed" -> Decode.succeed Unsubscribed
                | "Subscribed" -> Decode.succeed Subscribed
                | s -> Decode.fail $"{s} is not a valid customer status.")
        
        Decode.object <| fun get ->
            { Id = get.Required.Field "id" Decode.guid
              Name = get.Required.Field "name" Decode.string
              PhoneNumber = get.Required.Field "phoneNumber" Decode.string
              Status = get.Required.Field "status" decodeCustomerStatus }
