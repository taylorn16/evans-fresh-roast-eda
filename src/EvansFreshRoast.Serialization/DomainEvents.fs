namespace EvansFreshRoast.Serialization

open Thoth.Json.Net
open EvansFreshRoast.Domain.Aggregate
open EvansFreshRoast.Serialization.Common

module DomainEvents =
    let encodeDomainEvent (encodeEvent: Encoder<'Event>) : Encoder<DomainEvent<_, 'Event>> =
        fun event ->
            Encode.object [ "id", encodeId event.Id
                            "aggregateId", encodeId event.AggregateId
                            "timestamp", encodeOffsetDateTime event.Timestamp
                            "version", encodeAggregateVersion event.Version
                            "body", encodeEvent event.Body ]

    let decodeDomainEvent (decodeEvent: Decoder<'Event>) : Decoder<DomainEvent<'State, 'Event>> =
        Decode.map5
            (fun id aggregateId timestamp version body ->
                { Id = id
                  AggregateId = aggregateId
                  Timestamp = timestamp
                  Version = version
                  Body = body })
            (Decode.field "id" decodeId)
            (Decode.field "aggregateId" decodeId)
            (Decode.field "timestamp" decodeOffsetDateTime)
            (Decode.field "version" decodeAggregateVersion)
            (Decode.field "body" decodeEvent)
