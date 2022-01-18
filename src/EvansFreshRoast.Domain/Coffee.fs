module EvansFreshRoast.Domain.Coffee

open EvansFreshRoast.Domain.DomainTypes
open EvansFreshRoast.Utils
open EvansFreshRoast.Domain.Aggregate

type Coffee =
    { Name: CoffeeName
      Description: CoffeeDescription
      PricePerBag: UsdPrice
      WeightPerBag: OzWeight
      Status: CoffeeStatus }
    static member Empty =
        { Name = "<empty>" |> CoffeeName.create |> unsafeAssertOk
          Description = "<empty>" |> CoffeeDescription.create |> unsafeAssertOk
          PricePerBag = UsdPrice.zero
          WeightPerBag = OzWeight.zero
          Status = Inactive }

type CoffeeUpdateFields =
    { Name: CoffeeName option
      Description: CoffeeDescription option
      PricePerBag: UsdPrice option
      WeightPerBag: OzWeight option }

type Event =
    | Updated of CoffeeUpdateFields
    | Activated
    | Deactivated

type Command =
    | Update of CoffeeUpdateFields
    | Activate
    | Deactivate

type Error = | NoUpdateFieldsSupplied

let execute (_: Coffee) cmd =
    match cmd with
    | Update fields ->
        let hasAtLeastOneField =
            [ fields.Name |> Option.isSome
              fields.Description |> Option.isSome
              fields.PricePerBag |> Option.isSome
              fields.WeightPerBag |> Option.isSome ]
            |> Seq.exists ((=) true)

        if hasAtLeastOneField then
            Ok <| Updated fields
        else
            Error NoUpdateFieldsSupplied

    | Activate -> Ok Activated

    | Deactivate -> Ok Deactivated

let apply (coffee: Coffee) event =
    match event with
    | Updated fields ->
        let name =
            fields.Name |> Option.defaultValue coffee.Name

        let description =
            fields.Description
            |> Option.defaultValue coffee.Description

        let price =
            fields.PricePerBag
            |> Option.defaultValue coffee.PricePerBag

        let weight =
            fields.WeightPerBag
            |> Option.defaultValue coffee.WeightPerBag

        { coffee with
            Name = name
            Description = description
            PricePerBag = price
            WeightPerBag = weight }

    | Activated -> { coffee with Status = Active }

    | Deactivated -> { coffee with Status = Inactive }

let aggregate =
    { Execute = execute
      Apply = apply
      Empty = Coffee.Empty }
