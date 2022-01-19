module EvansFreshRoast.Serialization.Coffee

open Thoth.Json.Net
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee

let encodeCoffeeEvent event =
    match event with
    | Updated fields ->
        Encode.object
            [ if fields.Name.IsSome then
                  "name", Encode.string (CoffeeName.value fields.Name.Value)
              if fields.Description.IsSome then
                  "description", Encode.string (CoffeeDescription.value fields.Description.Value)
              if fields.PricePerBag.IsSome then
                  "pricePerBag", Encode.decimal (UsdPrice.value fields.PricePerBag.Value)
              if fields.WeightPerBag.IsSome then
                  "weightPerBag", Encode.decimal (OzWeight.value fields.WeightPerBag.Value) ]

    | Activated -> Encode.string "activated"

    | Deactivated -> Encode.string "deactivated"

let decodeCoffeeName: Decoder<CoffeeName> =
    Decode.string
    |> Decode.andThen (
        CoffeeName.create
        >> function
            | Ok name -> Decode.succeed name
            | Error err -> Decode.fail $"{err}"
    )

let decodeCoffeeDescription: Decoder<CoffeeDescription> =
    Decode.string
    |> Decode.andThen (
        CoffeeDescription.create
        >> function
            | Ok name -> Decode.succeed name
            | Error err -> Decode.fail $"{err}"
    )

let decodePrice: Decoder<UsdPrice> =
    Decode.decimal
    |> Decode.andThen (
        UsdPrice.create
        >> function
            | Ok price -> Decode.succeed price
            | Error err -> Decode.fail $"{err}"
    )

let decodeWeight: Decoder<OzWeight> =
    Decode.decimal
    |> Decode.andThen (
        OzWeight.create
        >> function
            | Ok weight -> Decode.succeed weight
            | Error err -> Decode.fail $"{err}"
    )

let decodeUpdated: Decoder<Event> =
    Decode.map4
        (fun name description price weight ->
            Updated
                { Name = name
                  Description = description
                  PricePerBag = price
                  WeightPerBag = weight })
        (Decode.optional "name" decodeCoffeeName)
        (Decode.optional "description" decodeCoffeeDescription)
        (Decode.optional "pricePerBag" decodePrice)
        (Decode.optional "weightPerBag" decodeWeight)

let decodeActivatedOrDeactivated: Decoder<Event> =
    Decode.string
    |> Decode.andThen (function
        | "activated" -> Decode.succeed Activated
        | "deactivated" -> Decode.succeed Deactivated
        | s -> Decode.fail $"expected 'activated' or 'deactivated', got '{s}'")

let decodeCoffeeEvent: Decoder<Event> =
    Decode.oneOf [ decodeUpdated
                   decodeActivatedOrDeactivated ]
