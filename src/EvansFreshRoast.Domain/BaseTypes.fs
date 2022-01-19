namespace EvansFreshRoast.Domain

open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open System.Text.RegularExpressions

type DomainValidationError =
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

type PositiveInt = private PositiveInt of int
module PositiveInt =
    let create i =
        if i < 1 then
            Error <| DomainTypeError IntIsNotPositive
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
            | 0 -> Error <| DomainTypeError StringIsEmpty
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
            |> Result.ofOption (DomainTypeError <| StringExceedsLimit 200))
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
            |> Result.ofOption (DomainTypeError <| StringExceedsLimit 100))
        |> Result.map String100

    let apply f (String100 s) = s |> NonEmptyString.apply f

    let value = apply id
