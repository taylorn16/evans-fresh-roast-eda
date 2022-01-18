namespace EvansFreshRoast.EventStore

open System
open NodaTime
open EvansFreshRoast.Utils
open Npgsql.FSharp
open Thoth.Json.Net
open EvansFreshRoast.Domain.BaseTypes
open EvansFreshRoast.Domain

type DomainEvent =
    { Id: Guid
      AggregateId: Guid
      AggregateName: string
      Version: int64
      EventName: string
      Payload: string
      Timestamp: OffsetDateTime }

type EventStoreDbError =
    | DatabaseErrorSavingEvent
    | DatabaseErrorLoadingEvents

[<RequireQualifiedAccess>]
module EventStoreDb =
    let private createEvent connectionString domainEvent =
        async {
            try
                return!
                    connectionString
                    |> Sql.connect
                    |> Sql.query
                        """
                    INSERT INTO events
                        ( aggregate_name
                        , aggregate_id
                        , aggregate_version
                        , event_id
                        , event_name
                        , event_payload
                        , created_timestamp )
                    VALUES
                        ( @aggregateName
                        , @aggregateId
                        , @version
                        , @eventId
                        , @eventName
                        , @payload
                        , @timestamp )
                    """
                    |> Sql.parameters [ "aggregateName", Sql.string domainEvent.AggregateName
                                        "aggregateId", Sql.uuid domainEvent.AggregateId
                                        "version", Sql.int64 domainEvent.Version
                                        "eventId", Sql.uuid domainEvent.Id
                                        "eventName", Sql.string domainEvent.EventName
                                        "payload", Sql.string domainEvent.Payload
                                        "timestamp", Sql.timestamptz (domainEvent.Timestamp.ToDateTimeOffset()) ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                    |> Async.map Ok
            with
            | _ -> return Error DatabaseErrorSavingEvent
        }

    let private getAllEvents connectionString aggregateId =
        async {
            try
                return!
                    connectionString
                    |> Sql.connect
                    |> Sql.query
                        """
                    SELECT
                        *
                    FROM events
                    WHERE aggregate_id = @aggregateId ORDER BY created_timestamp
                    """
                    |> Sql.parameters [ "aggregateId", Sql.uuid aggregateId ]
                    |> Sql.executeAsync (fun read ->
                        { Id = read.uuid "event_id"
                          AggregateId = read.uuid "aggregate_id"
                          AggregateName = read.string "aggregate_name"
                          Version = read.int64 "aggregate_version"
                          EventName = read.string "event_name"
                          Payload = read.string "event_payload"
                          Timestamp =
                            read.datetimeOffset "created_timestamp"
                            |> OffsetDateTime.FromDateTimeOffset })
                    |> Async.AwaitTask
                    |> Async.map Ok
            with
            | _ -> return Error DatabaseErrorLoadingEvents
        }

    let loadEvents
        (connectionString: string)
        (decoder: Decoder<'Event>)
        (mapDbError: EventStoreDbError -> 'Error)
        (mapDecoderError: string -> 'Error)
        (aggregateId: Id<'State>)
        =
        async {
            match! getAllEvents connectionString (Id.value aggregateId) with
            | Ok events ->
                return
                    events
                    |> List.map (fun evt ->
                        let id = (Id.create evt.Id) |> Result.unsafeAssertOk

                        let version =
                            (AggregateVersion.create evt.Version)
                            |> Result.unsafeAssertOk

                        let body = Decode.fromString decoder evt.Payload

                        body
                        |> Result.map (fun ev ->
                            { Id = id
                              AggregateId = aggregateId
                              Timestamp = evt.Timestamp
                              Version = version
                              Body = ev }: Aggregate.DomainEvent<'State, 'Event>))
                    |> Result.sequence Seq.head
                    |> Result.map List.ofArray
                    |> Result.mapError mapDecoderError

            | Error dbErr -> return Error(mapDbError dbErr)
        }

    let saveEvent
        (connectionString: string)
        (encoder: Encoder<'Event>)
        (aggregateName: string)
        (getEventName: 'Event -> string)
        (mapDbError: EventStoreDbError -> 'Error)
        (event: Aggregate.DomainEvent<'State, 'Event>)
        =
        async {
            let domainEvent =
                { Id = Id.value event.Id
                  AggregateName = aggregateName
                  AggregateId = Id.value event.AggregateId
                  Version = AggregateVersion.value event.Version
                  EventName = getEventName event.Body
                  Payload = Encode.toString 2 (encoder event.Body)
                  Timestamp = event.Timestamp }

            match! createEvent connectionString domainEvent with
            | Ok _ -> return Ok ()
            | Error dbErr -> return Error <| mapDbError dbErr
        }
