namespace EvansFreshRoast.Dto

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open System

type EventAcceptedResponse =
    { Message: string option
      AggregateId: Guid
      EventId: Guid }
    static member decoder =
        Decode.map3
            (fun msg agId evId ->
                { Message = msg
                  AggregateId = agId
                  EventId = evId })
            (Decode.optional "message" Decode.string)
            (Decode.field "aggregateId" Decode.guid)
            (Decode.field "eventId" Decode.guid)
