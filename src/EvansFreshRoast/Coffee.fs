module EvansFreshRoast.Coffee

open EvansFreshRoast.Models
open EvansFreshRoast.Composition
open EvansFreshRoast.Domain
open EvansFreshRoast.EventStore.Coffee
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open EvansFreshRoast.Serialization.Coffee
open Microsoft.AspNetCore.Http
open Giraffe
open Thoth.Json.Net

let eventStoreConnectionString =
    "Host=localhost;Port=5432;Database=evans_fresh_roast_events;Username=event_store_user;Password=event_store_pass;"
    |> ConnectionString.create

let useRequestDecoder (decoder: Decoder<'a>) (createHandler: 'a -> HttpHandler): HttpHandler =
    fun next ctx -> task {
        let! body = ctx.ReadBodyFromRequestAsync()
        
        match Decode.fromString decoder body with
        | Ok a ->
            let innerHandler = createHandler a
            return! innerHandler next ctx
        | Error decoderErr ->
            return! RequestErrors.BAD_REQUEST $"{decoderErr}" next ctx    
    }

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

let getCoffeeCommandHandler (compositionRoot: CompositionRoot) =
    Aggregate.createHandler
        Coffee.aggregate
        compositionRoot.LoadCoffeeEvents
        compositionRoot.SaveCoffeeEvent

let decodeCoffeeUpdated: Decoder<Coffee.Command> =
    Decode.map4
        (fun nm desc pr wt ->
            { Name = nm 
              Description = desc
              PricePerBag = pr
              WeightPerBag = wt }: CoffeeUpdated)
        (Decode.optional "name" decodeCoffeeName)
        (Decode.optional "description" decodeCoffeeDescription)
        (Decode.optional "pricePerBag" decodePrice)
        (Decode.optional "weightPerBag" decodeWeight)
    |> Decode.map Coffee.Command.Update

let putCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        let coffeeId = Id.create id |> unsafeAssertOk // TODO: don't assert ok!

        let handleCommandTask =
            Async.StartAsTask(
                getCoffeeCommandHandler compositionRoot coffeeId cmd,
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
            return! ServerErrors.INTERNAL_ERROR $"{handlerErr}" next ctx
    }
    |> useRequestDecoder decodeCoffeeUpdated

let decodeCreateCoffee: Decoder<Coffee.Command> =
    Decode.map4
        (fun nm desc pr wt ->
            { Name = nm
              Description = desc
              PricePerBag = pr
              WeightPerBag = wt }: CoffeeCreated)
        (Decode.field "name" decodeCoffeeName)
        (Decode.field "description" decodeCoffeeDescription)
        (Decode.field "pricePerBag" decodePrice)
        (Decode.field "weightPerBag" decodeWeight)
    |> Decode.map Coffee.Command.Create

let postCoffee (compositionRoot: CompositionRoot): HttpHandler =
    fun cmd (next: HttpFunc) (ctx: HttpContext) -> task {
        let handleCommandTask =
            Async.StartAsTask(
                getCoffeeCommandHandler compositionRoot (Id.newId()) cmd,
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
    |> useRequestDecoder decodeCreateCoffee

let activateCoffee (compositionRoot: CompositionRoot) id: HttpHandler =
    fun next ctx -> task {
        match Id.create id with
        | Ok coffeeId ->
            let handleCommandTask =
                Async.StartAsTask(
                    (getCoffeeCommandHandler compositionRoot)
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

let coffeeRoutes compositionRoot = choose [
    GET >=> choose [
        routeCif "/coffees/%O" (getCoffee compositionRoot)
        routeCix "/coffees(/?)" >=> getCoffees compositionRoot
    ]
    POST >=> choose [
        routeCix "/coffees(/?)" >=> postCoffee compositionRoot
    ]
    PUT >=> choose [
        routeCif "/coffees/%O/activate" (activateCoffee compositionRoot)
        routeCif "/coffees/%O" (putCoffee compositionRoot)
    ]
]
