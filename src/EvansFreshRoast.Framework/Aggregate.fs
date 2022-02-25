namespace EvansFreshRoast.Framework

open EvansFreshRoast.Utils
open System

type DomainEvent<'State, 'Event> =
    { Id: Id<DomainEvent<'State, 'Event>>
      AggregateId: Id<'State>
      Timestamp: DateTimeOffset
      Version: AggregateVersion
      Body: 'Event }

type Aggregate<'State, 'Command, 'Event, 'Error> =
    { Empty: 'State
      Apply: 'State -> 'Event -> 'State
      Execute: 'State -> 'Command -> Result<'Event, 'Error> }

type AggregateHandlerError<'DomainError, 'LoadEventsError, 'SaveEventError> =
    | FailedToLoadEvents of 'LoadEventsError
    | DomainError of 'DomainError
    | FailedToGetCurrentVersion
    | FailedToSaveEvent of 'SaveEventError

module Aggregate =
    let createHandler
        (aggregate: Aggregate<'State, 'Command, 'Event, 'DomainError>)
        (loadEvents: Id<'State> -> Async<Result<DomainEvent<'State, 'Event> list, 'LoadEventsError>>)
        (saveEvent: DomainEvent<'State, 'Event> -> Async<Result<unit, 'SaveEventError>>)
        =
        fun id cmd ->
            async {
                match! loadEvents id with
                | Error err ->
                    return Error <| FailedToLoadEvents err

                | Ok events ->
                    let currentVersion =
                        events
                        |> List.tryLast
                        |> Option.map (fun e -> e.Version)

                    let execute currentState expectedVersion = async {
                        match aggregate.Execute currentState cmd with
                        | Ok evt ->
                            let domainEvent =
                                { Id = Id.newId()
                                  AggregateId = id
                                  Timestamp = DateTimeOffset.Now
                                  Version = expectedVersion
                                  Body = evt }
                            
                            return! domainEvent
                            |> saveEvent
                            |> Async.map (
                                Result.mapError FailedToSaveEvent
                                >> Result.map (fun _ -> domainEvent)
                            )

                        | Error e ->
                            return Error <| DomainError e
                    }

                    match events, currentVersion with
                    | [], _ ->
                        return! execute aggregate.Empty AggregateVersion.one

                    | _, Some version ->
                        let currentState =
                            events
                            |> Seq.map (fun ev -> ev.Body)
                            |> Seq.fold aggregate.Apply aggregate.Empty

                        return! execute currentState (AggregateVersion.increment version)

                    | _ ->
                        return Error FailedToGetCurrentVersion
            }
