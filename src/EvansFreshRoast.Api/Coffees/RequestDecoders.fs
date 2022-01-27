module EvansFreshRoast.Api.Coffees.RequestDecoders

open Thoth.Json.Net
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Serialization.Coffee

let decodeUpdateCoffeeCmd: Decoder<Command> =
    Decode.map4
        (fun nm desc pr wt ->
            { Name = nm 
              Description = desc
              PricePerBag = pr
              WeightPerBag = wt }: CoffeeUpdated)
        (Decode.optional "name" decodeCoffeeName)
        (Decode.optional "description" decodeCoffeeDescription)
        (Decode.optional "pricePerBag" decodePrice)
        (Decode.optional "weightPerBag" decodeWeight)
    |> Decode.map Update

let decodeCreateCoffeeCmd: Decoder<Coffee.Command> =
    Decode.map4
        (fun nm desc pr wt ->
            { Name = nm
              Description = desc
              PricePerBag = pr
              WeightPerBag = wt }: CoffeeCreated)
        (Decode.field "name" decodeCoffeeName)
        (Decode.field "description" decodeCoffeeDescription)
        (Decode.field "pricePerBag" decodePrice)
        (Decode.field "weightPerBag" decodeWeight)
    |> Decode.map Create
