namespace EvansFreshRoast.Api

open Microsoft.AspNetCore.Http
open Giraffe
open EvansFreshRoast.Api.Models
open EvansFreshRoast.Domain
open EvansFreshRoast.EventStore.Roast
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils

module HttpHandlers =
    type DtoParseError =
        | DtoFieldMissing
        | DomainValidationErr of ConstrainedTypeError<ValidationError>

    let eventStoreConnectionString =
        // "Host=eventstoredb;Database=evans_fresh_roast_events;Username=event_store_user;Password=event_store_pass;"
        "Host=localhost;Port=5432;Database=evans_fresh_roast_events;Username=event_store_user;Password=event_store_pass;"
        |> ConnectionString.create

    let invert (opt: option<Result<'a, 'b>>) =
        match opt with
        | Some res ->
            match res with
            | Ok a -> Ok <| Some a
            | Error b -> Error b
        | None -> Ok None

    let toOptionalDomainValue
        (ctor: 'a -> Result<'b, ConstrainedTypeError<ValidationError>>)
        (rawVal: 'a)
        =
        rawVal
        |> Option.ofObj
        |> Option.map ctor
        |> invert

    let toRequiredDomainValue
        (ctor: 'a -> Result<'b, ConstrainedTypeError<ValidationError>>)
        (rawVal: 'a)
        =
        rawVal
        |> Option.ofObj
        |> Result.ofOption DtoFieldMissing
        |> Result.bind (ctor >> Result.mapError DomainValidationErr)

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

    let roastRoutes (compositionRoot: CompositionRoot) = choose [
        POST >=> choose [
            routeCix "/roasts(/?)" >=> handlePostRoast
        ]
    ]

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

    let postCustomer (compositionRoot: CompositionRoot) (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<CreateCustomerDto>()

            let buildCreateFields nm phn =
                { Name = nm
                  PhoneNumber = phn }: CustomerCreateFields

            let name = dto.Name |> toRequiredDomainValue CustomerName.create
            let phoneNumber = dto.PhoneNumber |> toRequiredDomainValue UsPhoneNumber.create

            let handleCommand =
                Aggregate.createHandler
                    Customer.aggregate
                    compositionRoot.LoadCustomerEvents
                    compositionRoot.SaveCustomerEvent

            let cmdResultAsync =
                buildCreateFields <!> name <*> phoneNumber
                |> Result.map Customer.Command.Create
                |> Result.map (handleCommand <| Id.newId())

            match cmdResultAsync with
            | Ok resultAsync ->
                match! Async.StartAsTask(resultAsync, cancellationToken=ctx.RequestAborted) with
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

    let customerRoutes compositionRoot = choose [
        GET >=> choose [
            routeCif "/customers/%O" (getCustomer compositionRoot)
            routeCix "/customers(/?)" >=> getCustomers compositionRoot
        ]
        POST >=> choose [
            routeCix "/customers(/?)" >=> postCustomer compositionRoot
        ]
    ]