module EvansFreshRoast.Api.Roasts.HttpHandlers

open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Roasts.RequestDecoders
open EvansFreshRoast.Api.Models
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils
open EvansFreshRoast.Serialization.Roast
open Giraffe
open Microsoft.AspNetCore.Http
open System.Collections.Generic

let withRoastId (id: System.Guid) (createHandler: Id<Roast> -> HttpHandler): HttpHandler = 
    fun next ctx -> task {
        match Id.create id with
        | Ok roastId ->
            return! (createHandler roastId) next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id." next ctx
    }

let postRoast (compositionRoot: CompositionRoot): HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        let! handleCommand = compositionRoot.GetRoastCommandHandler()
        let handleCommandTask =
            Async.StartAsTask(
                handleCommand (Id.newId()) cmd,
                cancellationToken = ctx.RequestAborted)

        match! handleCommandTask with // Todo: think about response body and error cases
        | Ok ev ->
            let responseText =
                sprintf
                    "Roast created. Event Id = %A; Roast Id = %A"
                    (Id.value ev.Id)
                    (Id.value ev.AggregateId)
            return! Successful.ACCEPTED responseText next ctx

        | Error handlerError ->
            return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx
    }
    |> useRequestDecoder decodeCreateRoastCmd

let putRoast (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        match Id.create id with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()
            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId cmd,
                    cancellationToken = ctx.RequestAborted)

            match! handleCommandTask with // Todo: think about response body and error cases
            | Ok ev ->
                let responseText =
                    sprintf
                        "Roast dates changed. Event Id = %A; Roast Id = %A"
                        (Id.value ev.Id)
                        (Id.value ev.AggregateId)
                return! Successful.ACCEPTED responseText next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid id." next ctx
    }
    |> useRequestDecoder decodeChangeRoastDatesCmd

let putCoffees (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()

            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId cmd,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Coffees added." next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id" next ctx
    }
    |> useRequestDecoder decodeAddCoffeesCmd

let deleteCoffees (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()

            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId cmd,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Coffees removed." next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id." next ctx
    }
    |> useRequestDecoder decodeRemoveCoffeesCmd

let putCustomers (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()

            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId cmd,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Customers added." next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id" next ctx
    }
    |> useRequestDecoder decodeAddCustomersCmd

let deleteCustomers (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()

            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId cmd,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Customers removed." next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id." next ctx
    }
    |> useRequestDecoder decodeRemoveCustomersCmd

let putCustomerInvoice (compositionRoot: CompositionRoot) (roastId, customerId): HttpHandler =
    fun paymentMethod (next: HttpFunc) (ctx: HttpContext) -> task {
        match roastId |> Id.create, customerId |> Id.create with
        | Ok roastId, Ok customerId ->
            let cmd = PayInvoice(customerId, paymentMethod)

            let! handleCommand = compositionRoot.GetRoastCommandHandler()
            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId cmd,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Invoice paid." next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast and/or customer id." next ctx
    }
    |> useRequestDecoder decodePaymentMethod

let postOpenRoast (compositionRoot: CompositionRoot) id: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()
            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId StartRoast,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Roast started!" next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id." next ctx
    }

let postFollowUp (compositionRoot: CompositionRoot) id: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()
            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId SendReminder,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Reminder sent!" next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id." next ctx
    }

let postCompletion (compositionRoot: CompositionRoot) id: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        match id |> Id.create with
        | Ok roastId ->
            let! handleCommand = compositionRoot.GetRoastCommandHandler()
            let handleCommandTask =
                Async.StartAsTask(
                    handleCommand roastId CompleteRoast,
                    cancellationToken=ctx.RequestAborted)

            match! handleCommandTask with
            | Ok _ ->
                return! Successful.ACCEPTED "Roast completed." next ctx

            | Error handlerError ->
                return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx

        | _ ->
            return! RequestErrors.BAD_REQUEST "Invalid roast id." next ctx
    }

let getRoasts (compositionRoot: CompositionRoot): HttpHandler =
    fun next ctx -> task {
        let! roasts =
            Async.StartAsTask(
                compositionRoot.GetAllRoasts(),
                cancellationToken=ctx.RequestAborted)
        
        let roastDtos =
            roasts
            |> List.map (fun rsv ->
                { Id = rsv.Id |> Id.value
                  Name = rsv.Name |> RoastName.value
                  RoastDate = rsv.RoastDate.ToString("R", null)
                  OrderByDate = rsv.OrderByDate.ToString("R", null)
                  CustomersCount = rsv.CustomersCount |> NonNegativeInt.value
                  RoastStatus = rsv.RoastStatus |> string
                  OrdersCount = rsv.OrdersCount |> NonNegativeInt.value
                  Coffees =
                    rsv.Coffees
                    |> List.map (fun (coffeeId, coffeeName) ->
                        { Id = coffeeId |> Id.value
                          Name = coffeeName |> CoffeeName.value }) }
            )

        return! Successful.OK roastDtos next ctx
    }

let getRoast (compositionRoot: CompositionRoot) id: HttpHandler =
    fun roastId next (ctx: HttpContext) -> task {
        let! roast =
            Async.StartAsTask(
                compositionRoot.GetRoast roastId,
                cancellationToken=ctx.RequestAborted)

        match roast with
        | Some roast ->
            let dto =
                { Id = roast.Id |> Id.value
                  Name = roast.Name |> RoastName.value
                  RoastDate = roast.RoastDate.ToString("R", null)
                  OrderByDate = roast.OrderByDate.ToString("R", null)
                  Customers = roast.Customers |> List.map Id.value
                  Coffees = roast.Coffees |> List.map Id.value
                  Status = string roast.Status
                  SentRemindersCount = roast.SentRemindersCount |> NonNegativeInt.value
                  Orders =
                    roast.Orders
                    |> List.map (fun order ->
                        let mapInvoice =
                            function
                            | PaidInvoice(amt, paymentMethod) ->
                                Some { Amount = UsdInvoiceAmount.value amt
                                       PaymentMethod = Some <| string paymentMethod }
                                           
                            | UnpaidInvoice amt ->
                                Some { Amount = UsdInvoiceAmount.value amt
                                       PaymentMethod = None }

                        let mapLineItems (lineItems: IDictionary<Id<Coffee>, Quantity>) =
                            lineItems
                            |> Seq.map (fun kvp -> kvp.Key |> Id.value, kvp.Value |> Quantity.value)
                            |> Map.ofSeq
                        
                        match order with
                        | ConfirmedOrder(details, invoice) ->
                            { CustomerId = details.CustomerId |> Id.value
                              Timestamp = details.Timestamp.ToString("o", null)
                              Invoice = mapInvoice invoice
                              LineItems = mapLineItems details.LineItems }

                        | UnconfirmedOrder details ->
                            { CustomerId = details.CustomerId |> Id.value
                              Timestamp = details.Timestamp.ToString("o", null)
                              Invoice = None
                              LineItems = mapLineItems details.LineItems }
                    ) }
                    
            return! Successful.OK dto next ctx

        | None ->
            return! RequestErrors.NOT_FOUND "Roast not found." next ctx
    }
    |> withRoastId id
