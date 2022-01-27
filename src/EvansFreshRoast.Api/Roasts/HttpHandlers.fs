module EvansFreshRoast.Api.Roasts.HttpHandlers

open EvansFreshRoast.Api.Models
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open Giraffe
open Microsoft.AspNetCore.Http

let handlePostRoast (compositionRoot: CompositionRoot): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->task {
        let! dto = ctx.BindJsonAsync<CreateRoastDto>()

        let cmd = Roast.UpdateRoastDates(dto.RoastDate, dto.OrderByDate)

        let! handleCommand = compositionRoot.CreateRoastCommandHandler()
        let handleCommandTask =
            Async.StartAsTask(
                cmd |> handleCommand (Id.newId()),
                cancellationToken = ctx.RequestAborted)

        match! handleCommandTask with // Todo: think about response body and error cases
        | Ok ev ->
            let responseText =
                sprintf
                    "Roast created. Event Id = %A; Aggregate Id = %A"
                    (Id.value ev.Id)
                    (Id.value ev.AggregateId)

            return! Successful.ACCEPTED responseText next ctx
        | Error handlerError ->
            return! ServerErrors.INTERNAL_ERROR $"{handlerError}" next ctx
    }
