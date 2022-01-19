namespace EvansFreshRoast.Domain

open EvansFreshRoast.Framework
open EvansFreshRoast.Utils

type Customer =
    { Name: CustomerName
      PhoneNumber: UsPhoneNumber
      Status: CustomerStatus }
    static member Empty =
        { Name = "<empty>" |> CustomerName.create |> unsafeAssertOk
          PhoneNumber = "0000000000" |> UsPhoneNumber.create |> unsafeAssertOk
          Status = Unconfirmed }

type CustomerUpdateFields =
    { Name: CustomerName option
      PhoneNumber: UsPhoneNumber option }

module Customer =
    type Event =
        | Updated of CustomerUpdateFields
        | Subscribed
        | Unsubscribed

    type Command =
        | Update of CustomerUpdateFields
        | Subscribe
        | Unsubscribe

    type Error = | NoUpdateFieldsSupplied

    let execute (_: Customer) cmd =
        match cmd with
        | Update fields ->
            let hasAtLeastOneField =
                [ fields.Name |> Option.isSome
                  fields.PhoneNumber |> Option.isSome ]
                |> Seq.exists ((=) true)

            if hasAtLeastOneField then
                Ok <| Updated fields
            else
                Error NoUpdateFieldsSupplied

        | Subscribe -> Ok <| Subscribed

        | Unsubscribe -> Ok <| Unsubscribed

    let apply (customer: Customer) event =
        match event with
        | Updated fields ->
            let name =
                fields.Name |> Option.defaultValue customer.Name

            let phone =
                fields.PhoneNumber
                |> Option.defaultValue customer.PhoneNumber

            { customer with
                Name = name
                PhoneNumber = phone }
        | Subscribed -> { customer with Status = CustomerStatus.Subscribed }
        | Unsubscribed -> { customer with Status = CustomerStatus.Unsubscribed }

    let aggregate =
        { Execute = execute
          Apply = apply
          Empty = Customer.Empty }
