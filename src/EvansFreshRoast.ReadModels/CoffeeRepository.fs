namespace EvansFreshRoast.ReadModels

open Npgsql.FSharp
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open Thoth.Json.Net
open EvansFreshRoast.Serialization.Coffee

module CoffeeRepository =
    let updateCoffee connectionString (event: DomainEvent<Coffee, Event>) =
        let connection = Sql.connect <| ConnectionString.value connectionString
        let coffeeUuid = event.AggregateId |> Id.value |> Sql.uuid

        match event.Body with
        | Created coffee ->
            let json =
                Encode.object
                    [ "name", Encode.string <| CoffeeName.value coffee.Name
                      "description", Encode.string <| CoffeeDescription.value coffee.Description
                      "pricePerBag", Encode.decimal <| UsdPrice.value coffee.PricePerBag
                      "weightPerBag", Encode.decimal <| OzWeight.value coffee.WeightPerBag
                      "status", Encode.string "Active" ]
                |> Encode.toString 2
            
            let sql =
                """
                INSERT INTO coffees(coffee_id, coffee_data)
                VALUES (@id, @data)
                """

            async {
                try
                    return! connection
                    |> Sql.query sql
                    |> Sql.parameters
                        [ "id", coffeeUuid
                          "data", Sql.jsonb json ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error inserting new coffee read model row.", ex)
            }

        | Updated coffee ->
            let jsonbSet =
                [ if coffee.Name.IsSome then
                    "name", Encode.string <| CoffeeName.value coffee.Name.Value
                  if coffee.Description.IsSome then
                    "description", Encode.string <| CoffeeDescription.value coffee.Description.Value
                  if coffee.PricePerBag.IsSome then
                    "pricePerBag", Encode.decimal <| UsdPrice.value coffee.PricePerBag.Value
                  if coffee.WeightPerBag.IsSome then
                    "weightPerBag", Encode.decimal <| OzWeight.value coffee.WeightPerBag.Value ]
                |> Helpers.generateRecursiveJsonbSet "coffee_data"

            async {
                try
                    return! connection
                    |> Sql.query
                        $"""
                        UPDATE coffees
                        SET coffee_data = {jsonbSet}
                        WHERE coffee_id = @id
                        """
                    |> Sql.parameters [ "id", coffeeUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn(
                        "Error updating coffee read model row (name?, description?, pricePerBag?, weightPerBag?).",
                        ex)
            }

        | Activated ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE coffees
                        SET coffee_data = jsonb_set(coffee_data, '{status}', '"Active"', true)
                        WHERE coffee_id = @id
                        """
                    |> Sql.parameters [ "id", coffeeUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error updating coffee read model row (status).", ex)
            }

        | Deactivated ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE coffees
                        SET coffee_data = jsonb_set(coffee_data, '{status}', '"Inactive"', true)
                        WHERE coffee_id = @id
                        """
                    |> Sql.parameters [ "id", coffeeUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error updating coffee read model row (status).", ex)
            }

    let decodeStatus: Decoder<CoffeeStatus> =
        Decode.string
        |> Decode.andThen (
            function
            | "Active" -> Decode.succeed Active
            | "Inactive" -> Decode.succeed Inactive
            | s -> Decode.fail $"Expected one of 'Active' or 'Inactive' but got '{s}.'"
        )

    let decodeCoffee: Decoder<Coffee> =
        Decode.map5
            (fun nm desc ppb wpb st ->
                { Name = nm
                  Description = desc
                  PricePerBag = ppb
                  WeightPerBag = wpb
                  Status = st })
            (Decode.field "name" decodeCoffeeName)
            (Decode.field "description" decodeCoffeeDescription)
            (Decode.field "pricePerBag" decodePrice)
            (Decode.field "weightPerBag" decodeWeight)
            (Decode.field "status" decodeStatus)

    let getCoffee connectionString (coffeeId: Id<Coffee>) =
        let sql =
            """
            SELECT coffee_id, coffee_data
            FROM coffees
            WHERE coffee_id = @id
            LIMIT 1
            """

        async {
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query sql
            |> Sql.parameters [ "id", Sql.uuid <| Id.value coffeeId ]
            |> Sql.executeAsync (fun row ->
                row.string "coffee_data"
                |> Decode.fromString decodeCoffee)
            |> Async.AwaitTask
            |> Async.map (
                List.take 1
                >> List.tryHead
                >> Option.bind Result.toOption
                >> Option.map (fun coffee -> coffeeId, coffee)
            )
        }

    let getAllCoffees connectionString =
        let sql =
            """
            SELECT coffee_id, coffee_data
            FROM coffees
            """

        async {
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query sql
            |> Sql.executeAsync (fun row ->
                let tuple a b = a, b

                let coffee = 
                    row.string "coffee_data"
                    |> Decode.fromString decodeCoffee
                    
                let id: Result<Id<Coffee>, string> =
                    row.uuid "coffee_id"
                    |> Id.create
                    |> Result.mapError string
                    
                tuple <!> id <*> coffee)
            |> Async.AwaitTask
            |> Async.map (
                List.filter isOk
                >> List.map unsafeAssertOk
            )
        }
