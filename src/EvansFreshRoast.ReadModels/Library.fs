namespace EvansFreshRoast.ReadModels

open Npgsql.FSharp
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils

module Customer =
    let updateReadModel connectionString (event: DomainEvent<Customer, Event>) =
        async {
            let connection = Sql.connect <| ConnectionString.value connectionString

            let rec generateRecursiveJsonbSet column (pathValues: (string * string) list) =
                match pathValues with
                | [] ->
                    ""
                | [(path, value)] ->
                    $"jsonb_set({column}, '{{{path}}}', '{value}', true)"
                | (path, value)::rest ->
                    $"jsonb_set({generateRecursiveJsonbSet column rest}, '{{{path}}}', '{value}', true)"

            match event.Body with
            | Updated fields ->
                let uuid = event.AggregateId |> Id.value |> Sql.uuid

                let toSqlString toString dv =
                    dv
                    |> Option.map toString
                    |> Option.defaultValue ""
                    |> Sql.string

                let! existingRows =
                    connection
                    |> Sql.query
                        """
                        SELECT id FROM customers_read WHERE id = @id
                        """
                    |> Sql.parameters [ "id", uuid ]
                    |> Sql.executeAsync (fun row -> row.uuid "id")
                    |> Async.AwaitTask

                let jsonbSet =
                    [ if fields.Name.IsSome then
                        "name", $"\"{CustomerName.value fields.Name.Value}\""
                      if fields.PhoneNumber.IsSome then
                        "phoneNumber", $"\"{UsPhoneNumber.value fields.PhoneNumber.Value}\"" ]
                    |> generateRecursiveJsonbSet "view"

                let update =
                    async {
                        try
                            return! connection
                            |> Sql.query
                                $"""
                                UPDATE customers_read
                                SET view = {jsonbSet}
                                WHERE id = @id
                                """
                            |> Sql.parameters [ "id", uuid ]
                            |> Sql.executeNonQueryAsync
                            |> Async.AwaitTask
                            |> Async.map (fun _ -> ())
                            |> Async.map Ok
                        with
                        | _ -> return Error "cheese"
                    }

                match existingRows with
                | [] ->
                    do! connection
                        |> Sql.query
                            """
                            INSERT INTO customers_read(id, view)
                            VALUES(@id, '{ "subscribed": false }'::jsonb)
                            """
                        |> Sql.parameters [ "id", uuid ]
                        |> Sql.executeNonQueryAsync
                        |> Async.AwaitTask
                        |> Async.map (fun _ -> ())

                    return! update
                | _ ->
                    return! update
            
            | Event.Subscribed ->
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE customers_read
                        SET view = jsonb_set(view, '{subscribed}', 'true', true)
                        WHERE id = @id
                        """
                    |> Sql.parameters [ "id", event.AggregateId |> Id.value |> Sql.uuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.map (fun _ -> ())
                    |> Async.map Ok
                with
                | _ ->
                    return Error "cheese"

            | Event.Unsubscribed ->
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE customers_read
                        SET view = jsonb_set(view, '{subscribed}', 'false', true)
                        WHERE id = @id
                        """
                    |> Sql.parameters [ "id", event.AggregateId |> Id.value |> Sql.uuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.map (fun _ -> ())
                    |> Async.map Ok
                with
                | _ ->
                    return Error "cheese"
        }
