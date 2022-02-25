﻿namespace EvansFreshRoast.ReadModels

open Npgsql.FSharp
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open Thoth.Json.Net
open EvansFreshRoast.Serialization.Customer

module CustomerRepository =
    let updateCustomer connectionString (event: DomainEvent<Customer, Event>) =
        let connection = Sql.connect <| ConnectionString.value connectionString
        let customerUuid = event.AggregateId |> Id.value |> Sql.uuid

        match event.Body with
        | Created fields ->
            let json = 
                Encode.object
                    [ "name", Encode.string <| CustomerName.value fields.Name
                      "phoneNumber", Encode.string <| UsPhoneNumber.value fields.PhoneNumber
                      "status", Encode.string "Unconfirmed" ]
                |> Encode.toString 2

            let sql =
                """
                INSERT INTO customers (customer_id, customer_data)
                VALUES (@id, @data)
                """
                
            async {
                try
                    return! connection
                    |> Sql.query sql
                    |> Sql.parameters
                        [ "id", customerUuid
                          "data", Sql.jsonb json ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error inserting new customer read model row.", ex)
            }

        | Updated fields ->
            let jsonbSet =
                [ if fields.Name.IsSome then
                    "name", Encode.string <| CustomerName.value fields.Name.Value ]
                |> Helpers.generateRecursiveJsonbSet "customer_data"
            
            async {
                try
                    return! connection
                    |> Sql.query
                        $"""
                        UPDATE customers
                        SET customer_data = {jsonbSet}
                        WHERE customer_id = @id
                        """
                    |> Sql.parameters [ "id", customerUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error updating customer read model row (name?, phoneNumber?).", ex)
            }
        
        | Event.Subscribed ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE customers
                        SET customer_data = jsonb_set(customer_data, '{status}', '"Subscribed"', true)
                        WHERE customer_id = @id
                        """
                    |> Sql.parameters [ "id", customerUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error updating customer read model row (status).", ex)
            }

        | Event.Unsubscribed ->
            async {
                try
                    return! connection
                    |> Sql.query
                        """
                        UPDATE customers
                        SET customer_data = jsonb_set(customer_data, '{status}', '"Unsubscribed"', true)
                        WHERE customer_id = @id
                        """
                    |> Sql.parameters [ "id", customerUuid ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.map (fun _ -> ())
                    |> Async.map Ok
                with
                | ex ->
                    return Error <| exn("Error updating customer read model row (status).", ex)
            }

    let decodeStatus: Decoder<CustomerStatus> =
        Decode.string
        |> Decode.andThen (
            function
            | "Unconfirmed" -> Decode.succeed Unconfirmed
            | "Unsubscribed" -> Decode.succeed Unsubscribed
            | "Subscribed" -> Decode.succeed Subscribed
            | s -> Decode.fail $"Expected one of 'Unconfirmed,' 'Unsubscribed,' or 'Subscribed' but got '{s}.'"
        )

    let decodeCustomer: Decoder<Customer> =
        Decode.map3
            (fun nm phn st ->
                { Name = nm
                  PhoneNumber = phn
                  Status = st })
            (Decode.field "name" decodeCustomerName)
            (Decode.field "phoneNumber" decodePhoneNumber)
            (Decode.field "status" decodeStatus)

    let getCustomer connectionString (customerId: Id<Customer>) =
        let sql =
            """
            SELECT customer_id, customer_data
            FROM customers
            WHERE customer_id = @id
            LIMIT 1
            """

        async {
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query sql
            |> Sql.parameters [ "id", Sql.uuid <| Id.value customerId ]
            |> Sql.executeAsync (fun row ->
                row.string "customer_data"
                |> Decode.fromString decodeCustomer)
            |> Async.AwaitTask
            |> Async.map (
                List.take 1
                >> List.tryHead
                >> Option.bind Result.toOption
                >> Option.map (fun customer -> customerId, customer)
            )
        }

    let getCustomerByPhoneNumber connectionString (phoneNumber: UsPhoneNumber) =
        let sql =
            """
            SELECT customer_id, customer_data
            FROM customers
            WHERE customer_data ->> 'phoneNumber' = @phoneNumber
            LIMIT 1
            """

        async {
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query sql
            |> Sql.parameters [ "phoneNumber", Sql.string <| UsPhoneNumber.value phoneNumber ]
            |> Sql.executeAsync (fun row ->
                let createRow cust id =
                    {| customer = cust
                       id = id |}

                let customer =
                    row.string "customer_data" |> Decode.fromString decodeCustomer
                    
                let id: Result<Id<Customer>, string> =
                    row.uuid "customer_id" |> Id.create |> Result.mapError string

                createRow <!> customer <*> id)
            |> Async.AwaitTask
            |> Async.map (
                List.take 1
                >> List.tryHead
                >> Option.bind Result.toOption
                >> Option.map (fun row -> row.id, row.customer)
            )
        }

    let getAllCustomers connectionString =
        let sql =
            """
            SELECT
                customer_id
              , customer_data
            FROM customers
            """

        async {
            return! ConnectionString.value connectionString
            |> Sql.connect
            |> Sql.query sql
            |> Sql.executeAsync (fun row ->
                let tuple a b = a, b

                let customer = 
                    row.string "customer_data"
                    |> Decode.fromString decodeCustomer
                    
                let id: Result<Id<Customer>, string> =
                    row.uuid "customer_id"
                    |> Id.create
                    |> Result.mapError string
                    
                tuple <!> id <*> customer)
            |> Async.AwaitTask
            |> Async.map (
                List.filter isOk
                >> List.map unsafeAssertOk
            )
        }
