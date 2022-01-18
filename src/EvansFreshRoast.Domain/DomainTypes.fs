namespace EvansFreshRoast.Domain.DomainTypes

open System.Text.RegularExpressions
open EvansFreshRoast.Domain.BaseTypes
open NodaTime
open System.Collections.Generic

type CoffeeDescription = private CoffeeDescription of String200

module CoffeeDescription =
    let create desc =
        String200.create desc
        |> Result.map CoffeeDescription

    let apply f (CoffeeDescription s) = s |> String200.apply f

    let value = apply id

type CoffeeName = CoffeeName of String100

module CoffeeName =
    let create desc =
        String100.create desc |> Result.map CoffeeName

    let apply f (CoffeeName s) = s |> String100.apply f

    let value = apply id

type CoffeeStatus =
    | Active
    | Inactive

type UsdPrice = private UsdPrice of decimal

module UsdPrice =
    let create price =
        match price with
        | p when p < 0m -> Error PriceIsNegative
        | p when p > 1000m -> Error PriceExceeds1000
        | _ -> Ok(UsdPrice price)

    let apply f (UsdPrice price) = f price

    let value = apply id

    let zero = UsdPrice 0m

type OzWeight = private OzWeight of decimal

module OzWeight =
    let create ounces =
        match ounces with
        | oz when oz < 0m -> Error WeightIsNegative
        | oz when oz > 800m -> Error WeightExceeds50
        | _ -> Ok(OzWeight ounces)

    let apply f (OzWeight oz) = f oz

    let value = apply id

    let zero = OzWeight 0m

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
            | _ -> Error <| PhoneNumberFormatIsInvalid

        |> Result.map UsPhoneNumber

    let apply f (UsPhoneNumber phn) = f phn

    let value = apply id

    let format phn =
        let fmt (s: string) =
            sprintf "(%s) %s-%s" (s.Substring(0, 3)) (s.Substring(3, 3)) (s.Substring(6, 4))

        phn |> apply fmt

type UsdInvoiceAmount = private UsdInvoiceAmount of decimal

module UsdInvoiceAmount =
    let create amt =
        match amt with
        | a when a < 0m -> Error InvoiceAmountIsNegative
        | a when a > 1000m -> Error InvoiceAmountExceeds1000
        | _ -> Ok(UsdInvoiceAmount amt)

    let apply f (UsdInvoiceAmount amt) = f amt

    let value = apply id

type Quantity = private Quantity of int

module Quantity =
    let create qty =
        match qty with
        | q when q < 0 -> Error QuantityIsNegative
        | q when q > 50 -> Error QuantityExceeds50Bags
        | _ -> Ok(Quantity qty)

    let apply f (Quantity qty) = f qty

    let value = apply id

type CoffeeReferenceId = private CoffeeReferenceId of string

module CoffeeReferenceId =
    let create ref =
        Regex.IsMatch(ref, "^[A-Z]$")
        |> function
            | true -> Ok <| CoffeeReferenceId ref
            | false -> Error ReferenceIdMustBeAtoZ

    let apply f (CoffeeReferenceId ref) = f ref

    let value = apply id

type PaymentMethod =
    | Unknown
    | Venmo
    | Cash
    | Check

type Coffee =
    { Id: Id<Coffee>
      Name: CoffeeName
      Description: CoffeeDescription
      PricePerBag: UsdPrice
      WeightPerBag: OzWeight
      Status: CoffeeStatus }

type CustomerProjection =
    { Id: Id<CustomerProjection>
      Name: CustomerName
      PhoneNumber: UsPhoneNumber
      Status: CustomerStatus }

type Invoice =
    | PaidInvoice of UsdInvoiceAmount * PaymentMethod
    | UnpaidInvoice of UsdInvoiceAmount

type OrderLineItem =
    { OrderReferenceId: CoffeeReferenceId
      Quantity: Quantity }

type OrderDetails =
    { CustomerId: Id<CustomerProjection>
      Timestamp: OffsetDateTime
      LineItems: IDictionary<Id<Coffee>, Quantity> }

type Order =
    | UnconfirmedOrder of OrderDetails
    | ConfirmedOrder of OrderDetails * Invoice

type SmsMsg = private SmsMsg of NonEmptyString

module SmsMsg =
    let create sms =
        NonEmptyString.create sms |> Result.map SmsMsg

    let apply f (SmsMsg sms) = sms |> NonEmptyString.apply f

    let value = apply id