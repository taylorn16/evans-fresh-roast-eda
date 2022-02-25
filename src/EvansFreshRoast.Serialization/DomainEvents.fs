namespace EvansFreshRoast.Serialization

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open EvansFreshRoast.Framework
open EvansFreshRoast.Serialization.Common

module DomainEvents =
    let encodeDomainEvent (encodeEvent: Encoder<'Event>) : Encoder<DomainEvent<_, 'Event>> =
        fun event ->
            Encode.object [ "id", encodeId event.Id
                            "aggregateId", encodeId event.AggregateId
                            "timestamp", Encode.datetimeOffset event.Timestamp
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
            (Decode.field "timestamp" Decode.datetimeOffset)
            (Decode.field "version" decodeAggregateVersion)
            (Decode.field "body" decodeEvent)
