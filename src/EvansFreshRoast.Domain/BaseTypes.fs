namespace EvansFreshRoast.Domain.BaseTypes

open EvansFreshRoast.Utils
open System
open System.Text.RegularExpressions

// Should rename this `DomainValidationError` and make cases underneath more explicit

type DomainError =
    | IdIsEmpty
    | VersionIsNotPositive
    | IntIsNotPositive
    | StringExceedsLimit of int
    | StringIsEmpty
    | PriceIsNegative
    | PriceExceeds1000
    | WeightIsNegative
    | WeightExceeds50
    | PhoneNumberFormatIsInvalid
    | InvoiceAmountIsNegative
    | InvoiceAmountExceeds1000
    | QuantityIsNegative
    | QuantityExceeds50Bags
    | ReferenceIdMustBeAtoZ

type Id<'a> = private Id of Guid

module Id =
    let create guid =
        if guid = Guid.Empty then
            Error IdIsEmpty
        else
            Ok <| Id guid

    let value (Id guid) = guid

    let newId () = Id <| Guid.NewGuid()

type AggregateVersion = private AggregateVersion of int64

module AggregateVersion =
    let create (version: int64) =
        if version < 1L then
            Error VersionIsNotPositive
        else
            Ok <| AggregateVersion version

    let apply f (AggregateVersion v) = f v

    let value = apply id

    let increment = apply (AggregateVersion << (+) 1L)

    let one = AggregateVersion 1L

type PositiveInt = private PositiveInt of int

module PositiveInt =
    let create i =
        if i < 1 then
            Error IntIsNotPositive
        else
            Ok <| PositiveInt i

    let apply f (PositiveInt i) = f i

    let value = apply id

type NonEmptyString = private NonEmptyString of string

module NonEmptyString =
    let create (s: string) =
        Regex.Replace(s, "\s", "")
        |> String.length
        |> function
            | 0 -> Error StringIsEmpty
            | _ -> Ok <| NonEmptyString s

    let apply f (NonEmptyString s) = f s

    let value = apply id


type String200 = private String200 of NonEmptyString

module String200 =
    let create s =
        let lte200 n = n <= 200

        NonEmptyString.create s
        |> Result.bind (fun s ->
            Some s
            |> Option.filter (NonEmptyString.apply (String.length >> lte200))
            |> Result.ofOption (StringExceedsLimit 200))
        |> Result.map String200

    let apply f (String200 s) = s |> NonEmptyString.apply f

    let value = apply id

type String100 = private String100 of NonEmptyString

module String100 =
    let create s =
        let lte100 n = n <= 100

        NonEmptyString.create s
        |> Result.bind (fun s ->
            Some s
            |> Option.filter (NonEmptyString.apply (String.length >> lte100))
            |> Result.ofOption (StringExceedsLimit 100))
        |> Result.map String100

    let apply f (String100 s) = s |> NonEmptyString.apply f

    let value = apply id
