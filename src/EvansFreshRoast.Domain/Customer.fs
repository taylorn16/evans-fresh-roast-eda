namespace EvansFreshRoast.Domain

open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open System.Text.RegularExpressions

type CustomerName = private CustomerName of String100

module CustomerName =
    let create desc =
        String100.create desc |> Result.map CustomerName

    let apply f (CustomerName s) = s |> String100.apply f

    let value = apply id

type CustomerStatus =
    | Unconfirmed
    | Subscribed
    | Unsubscribed

type UsPhoneNumber = private UsPhoneNumber of string

module UsPhoneNumber =
    let create phn =
        Regex.Replace(phn, "\D", "").Trim()
        |> fun s -> (s, String.length s)
        |> function
            | (s, 11) -> Ok <| s.Substring(1)
            | (s, 10) -> Ok s
            | _ ->
                Error
                <| DomainTypeError PhoneNumberFormatIsInvalid

        |> Result.map UsPhoneNumber

    let apply f (UsPhoneNumber phn) = f phn

    let value = apply id

    let format phn =
        let fmt (s: string) =
            sprintf "(%s) %s-%s" (s.Substring(0, 3)) (s.Substring(3, 3)) (s.Substring(6, 4))

        phn |> apply fmt

type Customer =
    { Name: CustomerName
      PhoneNumber: UsPhoneNumber
      Status: CustomerStatus }
    static member Empty =
        { Name = "<empty>" |> CustomerName.create |> unsafeAssertOk
          PhoneNumber =
            "0000000000"
            |> UsPhoneNumber.create
            |> unsafeAssertOk
          Status = Unconfirmed }

type CustomerCreateFields =
    { Name: CustomerName
      PhoneNumber: UsPhoneNumber }

type CustomerUpdateFields =
    { Name: CustomerName option
      PhoneNumber: UsPhoneNumber option }

module Customer =
    type Event =
        | Created of CustomerCreateFields
        | Updated of CustomerUpdateFields
        | Subscribed
        | Unsubscribed

    type Command =
        | Create of CustomerCreateFields
        | Update of CustomerUpdateFields
        | Subscribe
        | Unsubscribe

    type Error =
        | CustomerAlreadyCreated
        | NoUpdateFieldsSupplied

    let execute (state: Customer) cmd =
        match cmd with
        | Create fields ->
            if state <> Customer.Empty then
                Error CustomerAlreadyCreated
            else
                Ok <| Created fields
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
        | Created fields ->
            { customer with
                Name = fields.Name
                PhoneNumber = fields.PhoneNumber }

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
