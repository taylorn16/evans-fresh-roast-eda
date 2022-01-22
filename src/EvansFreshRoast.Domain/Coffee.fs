namespace EvansFreshRoast.Domain

open EvansFreshRoast.Utils
open EvansFreshRoast.Framework

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
        | p when p < 0m -> Error <| DomainTypeError PriceIsNegative
        | p when p > 1000m -> Error <| DomainTypeError PriceExceeds1000
        | _ -> Ok(UsdPrice price)

    let apply f (UsdPrice price) = f price

    let value = apply id

    let zero = UsdPrice 0m

type OzWeight = private OzWeight of decimal

module OzWeight =
    let create ounces =
        match ounces with
        | oz when oz < 0m -> Error <| DomainTypeError WeightIsNegative
        | oz when oz > 800m -> Error <| DomainTypeError WeightExceeds50
        | _ -> Ok(OzWeight ounces)

    let apply f (OzWeight oz) = f oz

    let value = apply id

    let zero = OzWeight 0m

type Coffee =
    { Name: CoffeeName
      Description: CoffeeDescription
      PricePerBag: UsdPrice
      WeightPerBag: OzWeight
      Status: CoffeeStatus }
    static member Empty =
        { Name = "<empty>" |> CoffeeName.create |> unsafeAssertOk
          Description =
            "<empty>"
            |> CoffeeDescription.create
            |> unsafeAssertOk
          PricePerBag = UsdPrice.zero
          WeightPerBag = OzWeight.zero
          Status = Inactive }

type CoffeeUpdated =
    { Name: CoffeeName option
      Description: CoffeeDescription option
      PricePerBag: UsdPrice option
      WeightPerBag: OzWeight option }

type CoffeeCreated =
    { Name: CoffeeName
      Description: CoffeeDescription
      PricePerBag: UsdPrice
      WeightPerBag: OzWeight }

module Coffee =
    type Event =
        | Created of CoffeeCreated
        | Updated of CoffeeUpdated
        | Activated
        | Deactivated

    type Command =
        | Create of CoffeeCreated
        | Update of CoffeeUpdated
        | Activate
        | Deactivate

    type Error =
        | CoffeeAlreadyCreated
        | NoUpdateFieldsSupplied

    let execute (state: Coffee) cmd =
        match cmd with
        | Create fields ->
            if state <> Coffee.Empty then
                Error CoffeeAlreadyCreated
            else
                Ok <| Created fields
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
        | Created fields ->
            { coffee with
                Name = fields.Name
                Description = fields.Description
                PricePerBag = fields.PricePerBag
                WeightPerBag = fields.WeightPerBag }
                
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
