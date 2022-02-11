module Types

open Thoth.Json

type Session =
    { Username: string
      ExpirationInSeconds: int }
    
    static member decoder =
        Decode.map2
            (fun unm expir ->
                { Username = unm
                  ExpirationInSeconds = expir })
            (Decode.field "username" Decode.string)
            (Decode.field "expirationInSeconds" Decode.int)

    static member encode session =
        Encode.object
            [ "username", Encode.string session.Username
              "expirationInSeconds", Encode.int session.ExpirationInSeconds ]

type OtpToken = private OtpToken of string

module OtpToken =
    let create s = OtpToken s

    let apply f (OtpToken tk) = f tk

    let value = apply id
