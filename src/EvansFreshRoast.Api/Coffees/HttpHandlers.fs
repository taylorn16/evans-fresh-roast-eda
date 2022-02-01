module EvansFreshRoast.Api.Coffees.HttpHandlers

open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Models
open EvansFreshRoast.Api.Coffees.RequestDecoders
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Domain
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Giraffe

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
                return! RequestErrors.NOT_FOUND "Coffee not found" next ctx
        
        | Error e ->
            return! RequestErrors.BAD_REQUEST e next ctx
    }

let putCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        let coffeeId = Id.create id |> unsafeAssertOk // TODO: don't assert ok!

        let handleCommandTask =
            Async.StartAsTask(
                cmd |> compositionRoot.CoffeeCommandHandler coffeeId,
                cancellationToken = ctx.RequestAborted)

        match! handleCommandTask with
        | Ok event ->
            let responseText =
                sprintf
                    "Coffee updated. Event Id = %A; Coffee Id = %A"
                    (Id.value event.Id)
                    (Id.value event.AggregateId)

            return! Successful.accepted (text responseText) next ctx

        | Error handlerErr ->
            ctx.GetLogger("putCoffee").LogError($"{handlerErr}");
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeUpdateCoffeeCmd

let postCoffee (compositionRoot: CompositionRoot): HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        let handleCommandTask =
            Async.StartAsTask(
                cmd |> compositionRoot.CoffeeCommandHandler (Id.newId()),
                cancellationToken = ctx.RequestAborted)

        match! handleCommandTask with
        | Ok event ->
            let responseText =
                sprintf
                    "Coffee created. Event Id = %A; Coffee Id = %A"
                    (Id.value event.Id)
                    (Id.value event.AggregateId)

            return! Successful.accepted (text responseText) next ctx
        | Error handlerErr ->
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeCreateCoffeeCmd

let activateCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun next ctx -> task {
        match Id.create id with
        | Ok coffeeId ->
            let handleCommandTask =
                Async.StartAsTask(
                    (compositionRoot.CoffeeCommandHandler)
                        coffeeId 
                        Coffee.Command.Activate,
                    cancellationToken = ctx.RequestAborted)
                
            match! handleCommandTask with
            | Ok evt ->
                return! Successful.accepted (text "activated") next ctx
            | Error err ->
                return! ServerErrors.internalError (text "handler error") next ctx
        | Error err ->
            return! ServerErrors.internalError (text "invalid id") next ctx
    }
