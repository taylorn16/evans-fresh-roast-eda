namespace EvansFreshRoast.ReadModels

open Npgsql.FSharp
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open Thoth.Json.Net

module Customer =
    let updateReadModel connectionString (event: DomainEvent<Customer, Event>) =
        async {
            let connection = Sql.connect <| ConnectionString.value connectionString

            let eventUuid = event.AggregateId |> Id.value |> Sql.uuid

            let rec generateRecursiveJsonbSet column (pathValues: (string * string) list) =
                match pathValues with
                | [] ->
                    ""
                | [(path, value)] ->
                    $"jsonb_set({column}, '{{{path}}}', '{value}', true)"
                | (path, value)::rest ->
                    $"jsonb_set({generateRecursiveJsonbSet column rest}, '{{{path}}}', '{value}', true)"

            match event.Body with
            | Created fields ->
                let json = Encode.Auto.toString<CustomerCreateFields>(2, fields, CamelCase)

                let sql =
                    """
                    INSERT INTO customers_read (id, view)
                    VALUES (@id, @view)
                    """

                try
                    return! connection
                    |> Sql.query sql
                    |> Sql.parameters
                        [ "id", eventUuid
                          "view", Sql.jsonb json ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | _ -> return Error "Error inserting new customer read model row."

            | Updated fields ->
                let jsonbSet =
                    [ if fields.Name.IsSome then
                        "name", $"\"{CustomerName.value fields.Name.Value}\""
                      if fields.PhoneNumber.IsSome then
                        "phoneNumber", $"\"{UsPhoneNumber.value fields.PhoneNumber.Value}\"" ]
                    |> generateRecursiveJsonbSet "view"

                try
                    return! connection
                    |> Sql.query
                        $"""
                        UPDATE customers_read
                        SET view = {jsonbSet}
                        WHERE id = @id
                        """
                    |> Sql.parameters [ "id", eventUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | _ -> return Error "Error updating customer read model row (name, phoneNumber)."
            
            | Event.Subscribed ->
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE customers_read
                        SET view = jsonb_set(view, '{status}', '"Subscribed"', true)
                        WHERE id = @id
                        """
                    |> Sql.parameters [ "id", eventUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | _ ->
                    return Error "Error updating customer read model row (status)."

            | Event.Unsubscribed ->
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE customers_read
                        SET view = jsonb_set(view, '{status}', '"Unsubscribed"', true)
                        WHERE id = @id
                        """
                    |> Sql.parameters [ "id", eventUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.map (fun _ -> ())
                    |> Async.map Ok
                with
                | _ ->
                    return Error "Error updating customer read model row (status)."
        }
