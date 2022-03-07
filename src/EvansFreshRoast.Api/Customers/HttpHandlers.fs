module EvansFreshRoast.Api.Customers.HttpHandlers

open Giraffe
open Microsoft.AspNetCore.Http
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Framework
open EvansFreshRoast.Dto
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Utils
open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Customers.RequestDecoders
open Thoth.Json.Net

let withCustomerId (id: System.Guid) (createHandler: Id<Customer> -> HttpHandler): HttpHandler = 
    fun next ctx -> task {
        match Id.create id with
        | Ok roastId ->
            return! (createHandler roastId) next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid customer id." next ctx
    }

let handleCommandAsync (compositionRoot: CompositionRoot) (ctx: HttpContext) id cmd =
    Async.StartAsTask(
        compositionRoot.CustomerCommandHandler id cmd,
        cancellationToken = ctx.RequestAborted)

let getCustomers (compositionRoot: CompositionRoot) =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let! customers = Async.StartAsTask(
            compositionRoot.GetAllCustomers(),
            cancellationToken=ctx.RequestAborted)

        let response =
            customers
            |> List.map (fun (id, cust) ->
                { Id = Id.value id
                  Name = CustomerName.value cust.Name
                  PhoneNumber = UsPhoneNumber.format cust.PhoneNumber
                  Status = cust.Status })
            |> List.map Customer.encode
            |> Encode.list
            |> Encode.toString 2

        return! Successful.ok (text response) next ctx
    }

let getCustomer (compositionRoot: CompositionRoot) id =
    fun customerId (next: HttpFunc) (ctx: HttpContext) -> task {
        match! Async.StartAsTask(compositionRoot.GetCustomer customerId, cancellationToken=ctx.RequestAborted) with
        | Some (customerId, customer) ->
            let response =
                { Id = Id.value customerId
                  Name = CustomerName.value customer.Name
                  PhoneNumber = UsPhoneNumber.format customer.PhoneNumber
                  Status = customer.Status }
                |> Customer.encode
                |> Encode.toString 2 

            return! Successful.ok (text response) next ctx
        | None ->
            return! RequestErrors.NOT_FOUND "Customer not found." next ctx
    }
    |> withCustomerId id

let postCustomer (compositionRoot: CompositionRoot): HttpHandler =
    fun cmd next (ctx: HttpContext) -> task {
        match! handleCommandAsync compositionRoot ctx (Id.newId()) cmd with
        | Ok event ->
            let response =
                { AggregateId = event.AggregateId |> Id.value
                  EventId = event.Id |> Id.value
                  Message = None }

            return! Successful.ACCEPTED response next ctx

        | Error handlerErr ->
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeCreateCustomerCmd

let putCustomer (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd next (ctx: HttpContext) -> task {
        let customerId = Id.create id |> unsafeAssertOk

        match! handleCommandAsync compositionRoot ctx customerId cmd with
        | Ok event ->
            let response = {| eventId = event.Id |> Id.value |}
            return! Successful.ACCEPTED response next ctx

        | Error (DomainError NoUpdateFieldsSupplied) ->
            return! RequestErrors.BAD_REQUEST "Must specify at least one property to update." next ctx

        | Error handlerErr ->
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeUpdateCustomerCmd
