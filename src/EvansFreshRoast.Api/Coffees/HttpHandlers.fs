module EvansFreshRoast.Api.Coffees.HttpHandlers

open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Models
open EvansFreshRoast.Api.Coffees.RequestDecoders
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Giraffe

let withCoffeeId (id: System.Guid) (createHandler: Id<Coffee> -> HttpHandler): HttpHandler = 
    fun next ctx -> task {
        match Id.create id with
        | Ok roastId ->
            return! (createHandler roastId) next ctx

        | Error _ ->
            return! RequestErrors.BAD_REQUEST "Invalid coffee id." next ctx
    }

let handleCommandAsync (compositionRoot: CompositionRoot) (ctx: HttpContext) id cmd =
    Async.StartAsTask(
        compositionRoot.CoffeeCommandHandler id cmd,
        cancellationToken = ctx.RequestAborted)

let getCoffees (compositionRoot: CompositionRoot) (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! coffees = Async.StartAsTask(
            compositionRoot.GetAllCoffees(),
            cancellationToken=ctx.RequestAborted)

        let coffeeDtos =
            coffees
            |> List.map (fun (id, coff) ->
                { Id = Id.value id
                  Name = CoffeeName.value coff.Name
                  Description = CoffeeDescription.value coff.Description
                  PricePerBag = UsdPrice.value coff.PricePerBag
                  WeightPerBag = OzWeight.value coff.WeightPerBag })

        return! Successful.OK coffeeDtos next ctx
    }

let getCoffee (compositionRoot: CompositionRoot) id (next: HttpFunc) (ctx: HttpContext) =
    task {
        match compositionRoot.GetCoffee <!> (Id.create id) with
        | Ok getCoffee ->
            match! Async.StartAsTask(getCoffee, cancellationToken=ctx.RequestAborted) with
            | Some (coffeeId, coffee) ->
                let coffeeDto =
                    { Id = Id.value coffeeId
                      Name = CoffeeName.value coffee.Name
                      Description = CoffeeDescription.value coffee.Description
                      PricePerBag = UsdPrice.value coffee.PricePerBag
                      WeightPerBag = OzWeight.value coffee.WeightPerBag }

                return! Successful.OK coffeeDto next ctx
            | None ->
                return! RequestErrors.NOT_FOUND "Coffee not found." next ctx
        
        | Error e ->
            return! RequestErrors.BAD_REQUEST e next ctx
    }

let putCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        let coffeeId = Id.create id |> unsafeAssertOk

        match! handleCommandAsync compositionRoot ctx coffeeId cmd with
        | Ok event ->
            let response = {| eventId = event.Id |> Id.value |}
            return! Successful.ACCEPTED response next ctx

        | Error (DomainError NoUpdateFieldsSupplied) ->
            return! RequestErrors.BAD_REQUEST "Must specify at least one field to update." next ctx

        | Error handlerErr ->
            let logger = ctx.GetLogger("putCoffee")
            logger.LogError($"{handlerErr}")
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeUpdateCoffeeCmd

let postCoffee (compositionRoot: CompositionRoot): HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        match! handleCommandAsync compositionRoot ctx (Id.newId()) cmd with
        | Ok event ->
            let response =
                {| coffeeId = event.AggregateId |> Id.value
                   eventId = event.Id |> Id.value |}
            return! Successful.ACCEPTED response next ctx

        | Error handlerErr ->
            let logger = ctx.GetLogger("postCoffee")
            logger.LogError($"{handlerErr}")
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeCreateCoffeeCmd

let activateCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun coffeeId next (ctx: HttpContext) -> task {
        match! handleCommandAsync compositionRoot ctx coffeeId Activate with
        | Ok evt ->
            let response = {| eventId = evt.Id |> Id.value |}
            return! Successful.ACCEPTED response next ctx

        | Error handlerErr ->
            let logger = ctx.GetLogger("activateCoffee")
            logger.LogError($"{handlerErr}")
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> withCoffeeId id

let deactivateCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun coffeeId next (ctx: HttpContext) -> task {
        match! handleCommandAsync compositionRoot ctx coffeeId Deactivate with
        | Ok evt ->
            let response = {| eventId = evt.Id |> Id.value |}
            return! Successful.ACCEPTED response next ctx
            
        | Error handlerErr ->
            let logger = ctx.GetLogger("deactivateCoffee")
            logger.LogError($"{handlerErr}")
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> withCoffeeId id
