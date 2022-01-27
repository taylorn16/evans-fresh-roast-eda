module EvansFreshRoast.Api.Customers.HttpHandlers

open Giraffe
open Microsoft.AspNetCore.Http
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Framework
open EvansFreshRoast.Api.Models
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Customers.RequestDecoders

let getCustomers (compositionRoot: CompositionRoot) (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! customers = Async.StartAsTask(
            compositionRoot.GetAllCustomers(),
            cancellationToken=ctx.RequestAborted)

        let customerDtos =
            customers
            |> List.map (fun (id, cust) ->
                { Id = Id.value id
                  Name = CustomerName.value cust.Name
                  PhoneNumber = UsPhoneNumber.format cust.PhoneNumber })

        return! Successful.OK customerDtos next ctx
    }

let getCustomer (compositionRoot: CompositionRoot) id (next: HttpFunc) (ctx: HttpContext) =
    let getValidationErrorHandler error =
        let genericErrorHandler =
            ServerErrors.INTERNAL_ERROR "Something went wrong."

        match error with
        | FrameworkTypeError err ->
            match err with
            | IdIsEmpty -> RequestErrors.BAD_REQUEST "Customer id cannot be empty."
            | _ -> genericErrorHandler
        | _ -> genericErrorHandler
    
    task {
        match compositionRoot.GetCustomer <!> (Id.create id) with
        | Ok getCustomer ->
            match! Async.StartAsTask(getCustomer, cancellationToken=ctx.RequestAborted) with
            | Some (customerId, customer) ->
                let customerDto =
                    { Id = Id.value customerId
                      Name = CustomerName.value customer.Name
                      PhoneNumber = UsPhoneNumber.format customer.PhoneNumber }

                return! Successful.OK customerDto next ctx
            | None ->
                return! RequestErrors.NOT_FOUND "Customer not found" next ctx
        
        | Error err ->
            return! getValidationErrorHandler err next ctx
    }

let postCustomer (compositionRoot: CompositionRoot): HttpHandler =
    fun cmd next (ctx: HttpContext) -> task {
        let handleCommandTask =
            Async.StartAsTask(
                cmd |> compositionRoot.CustomerCommandHandler (Id.newId()),
                cancellationToken=ctx.RequestAborted)

        match! handleCommandTask with
        | Ok event ->
            let responseText =
                sprintf
                    "Customer created. Event Id = %A; Customer Id = %A"
                    event.Id
                    event.AggregateId

            return! Successful.ACCEPTED responseText next ctx
        | Error handlerErr ->
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeCreateCustomerCmd
