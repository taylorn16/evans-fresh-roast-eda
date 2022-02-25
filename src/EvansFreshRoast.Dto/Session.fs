namespace EvansFreshRoast.Dto

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open System

type Session =
    { Username: string
      Expires: DateTimeOffset }
    static member decoder =
        Decode.map2
            (fun unm expir ->
                { Username = unm
                  Expires = expir })
            (Decode.field "username" Decode.string)
            (Decode.field "expires" Decode.datetimeOffset)

    static member encode session =
        Encode.object
            [ "username", Encode.string session.Username
              "expires", Encode.datetimeOffset session.Expires ]
