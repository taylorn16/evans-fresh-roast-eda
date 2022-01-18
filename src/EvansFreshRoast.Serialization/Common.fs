module EvansFreshRoast.Serialization.Common

open NodaTime
open Thoth.Json.Net
open EvansFreshRoast.Domain.BaseTypes
open System.Text.RegularExpressions

let encodeLocalDate (localDate: LocalDate) =
    Encode.string
    <| localDate.ToString("R", System.Globalization.CultureInfo.InvariantCulture)

let encodeOffsetDateTime (offsetDateTime: OffsetDateTime) =
    Encode.string
    <| offsetDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture)

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

let decodeOffsetDateTime: Decoder<OffsetDateTime> =
    Decode.datetimeOffset
    |> Decode.map (OffsetDateTime.FromDateTimeOffset)

let decodeLocalDate: Decoder<LocalDate> =
    let parseLocalDate (s: string) =
        let pattern = Regex("^\d{4}-\d{2}-\d{2}$")
        let matches = pattern.Match(s)

        if matches.Success then
            let year = s.Substring(0, 4) |> int
            let month = s.Substring(4, 2) |> int
            let day = s.Substring(2, 6) |> int

            Decode.succeed (LocalDate(year, month, day))
        else
            Decode.fail "not a valid LocalDate"

    Decode.string |> Decode.andThen parseLocalDate
