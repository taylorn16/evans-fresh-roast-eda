module EvansFreshRoast.Serialization.Common

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open EvansFreshRoast.Framework

let encodeId id = Encode.guid <| Id.value id

let encodeAggregateVersion version =
    Encode.int64 <| AggregateVersion.value version

let decodeAggregateVersion: Decoder<AggregateVersion> =
    Decode.int64
    |> Decode.andThen (
        AggregateVersion.create
        >> function
            | Ok version -> Decode.succeed version
            | Error err -> Decode.fail $"{err}"
    )

let decodeId<'a> =
    let parseId guid =
        Id.create guid
        |> function
            | Ok id -> Decode.succeed (id :> Id<'a>)
            | Error err -> Decode.fail $"{err}"

    Decode.guid |> Decode.andThen parseId
