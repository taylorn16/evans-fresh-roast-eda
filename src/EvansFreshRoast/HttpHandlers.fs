namespace EvansFreshRoast

module HttpHandlers =

    open Microsoft.AspNetCore.Http
    open Giraffe
    open EvansFreshRoast.Models
    open EvansFreshRoast.Domain
    open EvansFreshRoast.EventStore.Roast
    open EvansFreshRoast.EventStore.Coffee
    open EvansFreshRoast.EventStore.Customer
    open EvansFreshRoast.Framework
    open EvansFreshRoast.Utils

    type DtoParseError =
        | DtoFieldMissing
        | DomainValidationErr of ConstrainedTypeError<DomainValidationError>

    let eventStoreConnectionString =
        // "Host=eventstoredb;Database=evans_fresh_roast_events;Username=event_store_user;Password=event_store_pass;"
        "Host=localhost;Port=5432;Database=evans_fresh_roast_events;Username=event_store_user;Password=event_store_pass;"
        |> ConnectionString.create

    let handleGetHello (next: HttpFunc) (ctx: HttpContext) =
        task {
            let response = { Text = "Hello world, from Giraffe!" }
            return! json response next ctx
        }

    let invert (opt: option<Result<'a, 'b>>) =
        match opt with
        | Some res ->
            match res with
            | Ok a -> Ok <| Some a
            | Error b -> Error b
        | None -> Ok None

    let toOptionalDomainValue
        (ctor: 'a -> Result<'b, ConstrainedTypeError<DomainValidationError>>)
        (rawVal: 'a) =
        rawVal
        |> Option.ofObj
        |> Option.map ctor
        |> invert

    let toRequiredDomainValue
        (ctor: 'a -> Result<'b, ConstrainedTypeError<DomainValidationError>>)
        (rawVal: 'a) =
        rawVal
        |> Option.ofObj
        |> Result.ofOption DtoFieldMissing
        |> Result.bind (ctor >> Result.mapError DomainValidationErr)

    let handlePostCoffee (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<CreateCoffeeDto>()

            let name = dto.Name |> toOptionalDomainValue CoffeeName.create
            let description = dto.Description |> toOptionalDomainValue CoffeeDescription.create
            let price = dto.PricePerBag |>  UsdPrice.create |> Result.map Some
            let weight = dto.WeightPerBag |> OzWeight.create |> Result.map Some

            let buildUpdateFields nm desc pr wt: CoffeeUpdateFields =
                { Name = nm
                  Description = desc
                  PricePerBag = pr
                  WeightPerBag = wt }

            let handleCommand =
                Aggregate.createHandler
                    Coffee.aggregate
                    (loadCoffeeEvents eventStoreConnectionString)
                    (saveCoffeeEvent eventStoreConnectionString)

            let cmdResultAsync =
                buildUpdateFields <!> name <*> description <*> price <*> weight
                |> Result.map Coffee.Command.Update
                |> Result.map (handleCommand <| Id.newId())

            match cmdResultAsync with
            | Ok cmdResultAsync' ->
                match! Async.StartAsTask(cmdResultAsync', cancellationToken = ctx.RequestAborted) with
                | Ok evt ->
                    let responseText =
                        sprintf
                            "Coffee created. Event Id = %A; Aggregate Id = %A"
                            (Id.value evt.Id)
                            (Id.value evt.AggregateId)

                    return! Successful.accepted (text responseText) next ctx
                | Error handlerErr ->
                    return! ServerErrors.internalError (text "handler error") next ctx
            | Error domainErr ->
                return! ServerErrors.internalError (text "domain validation error") next ctx
        }

    let handleActivateCoffee (id: System.Guid) (next: HttpFunc) (ctx: HttpContext) =
        task {
            let handleCommand =
                Aggregate.createHandler
                    Coffee.aggregate
                    (loadCoffeeEvents eventStoreConnectionString)
                    (saveCoffeeEvent eventStoreConnectionString)

            match Id.create id with
            | Ok coffeeId ->
                let handleCommandTask = handleCommand coffeeId Coffee.Command.Activate
                match! Async.StartAsTask(handleCommandTask, cancellationToken = ctx.RequestAborted) with
                | Ok evt ->
                    return! Successful.accepted (text "activated") next ctx
                | Error err ->
                    return! ServerErrors.internalError (text "handler error") next ctx
            | Error err ->
                return! ServerErrors.internalError (text "invalid id") next ctx
            
        }

    let handlePostRoast (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<CreateRoastDto>()

            let cmd =
                Roast.UpdateRoastDates(dto.RoastDate, dto.OrderByDate)

            let today =
                NodaTime.LocalDate.FromDateTime(System.DateTime.Now)

            let handleCommand =
                Aggregate.createHandler
                    (Roast.createAggregate [] [] today)
                    (loadRoastEvents eventStoreConnectionString)
                    (saveRoastEvent eventStoreConnectionString)

            let handleCommandTask = handleCommand (Id.newId ()) cmd
            let! commandResult = Async.StartAsTask(handleCommandTask, cancellationToken = ctx.RequestAborted)

            match commandResult with // Todo: think about response body and error cases
            | Ok ev ->
                let responseText =
                    sprintf
                        "Roast created. Event Id = %A; Aggregate Id = %A"
                        (Id.value ev.Id)
                        (Id.value ev.AggregateId)

                return! Successful.accepted (text responseText) next ctx
            | Error err -> return! ServerErrors.internalError (text $"{err}") next ctx
        }

    let handlePostCustomer (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<CreateCustomerDto>()

            let buildUpdateFields nm phn =
                { Name = nm
                  PhoneNumber = phn }: CustomerCreateFields

            let name = dto.Name |> toRequiredDomainValue CustomerName.create
            let phoneNumber = dto.PhoneNumber |> toRequiredDomainValue UsPhoneNumber.create

            let handleCommand =
                Aggregate.createHandler
                    Customer.aggregate
                    (loadCustomerEvents eventStoreConnectionString)
                    (saveCustomerEvent eventStoreConnectionString)

            let cmdResultAsync =
                buildUpdateFields <!> name <*> phoneNumber
                |> Result.map Customer.Command.Create
                |> Result.map (handleCommand <| Id.newId())

            match cmdResultAsync with
            | Ok cmdResultAsync' ->
                match! Async.StartAsTask(cmdResultAsync', cancellationToken=ctx.RequestAborted) with
                | Ok evt ->
                    let responseText =
                        sprintf
                            "Customer created. Event Id = %A; Aggregate Id = %A"
                            evt.Id
                            evt.AggregateId

                    return! Successful.accepted (text responseText) next ctx
                | Error e ->
                    let responseText = sprintf "Handler error: %A" e
                    return! ServerErrors.internalError (text responseText) next ctx 
            | Error e ->
                let responseText = sprintf "Validation error: %A" e
                return! ServerErrors.internalError (text responseText) next ctx
        }
