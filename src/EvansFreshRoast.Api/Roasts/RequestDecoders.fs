module EvansFreshRoast.Api.Roasts.RequestDecoders

open Thoth.Json.Net
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Serialization.Common
open EvansFreshRoast.Serialization.Roast

let decodeAddCoffeesCmd: Decoder<Command> =
    Decode.list decodeId
    |> Decode.map AddCoffees

let decodeRemoveCoffeesCmd: Decoder<Command> =
    Decode.list decodeId
    |> Decode.map RemoveCoffees

let decodeAddCustomersCmd: Decoder<Command> =
    Decode.list decodeId
    |> Decode.map AddCustomers

let decodeRemoveCustomersCmd: Decoder<Command> =
    Decode.list decodeId
    |> Decode.map RemoveCustomers

let decodeCreateRoastCmd: Decoder<Command> =
    Decode.map3
        (fun nm stDt obDt ->
            { Name = nm
              RoastDate = stDt
              OrderByDate = obDt })
        (Decode.field "name" decodeRoastName)
        (Decode.field "roastDate" Decode.datetime)
        (Decode.field "orderByDate" Decode.datetime)
    |> Decode.map Create

let decodeChangeRoastDatesCmd: Decoder<Command> =
    Decode.map2
        (fun stDt obDt -> stDt, obDt)
        (Decode.field "roastDate" Decode.datetime)
        (Decode.field "orderByDate" Decode.datetime)
    |> Decode.map UpdateRoastDates
