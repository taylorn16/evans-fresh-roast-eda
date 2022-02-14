module EvansFreshRoast.Api.Auth.RequestDecoders

open Thoth.Json.Net
open EvansFreshRoast.Dto

let decodeLoginRequest: Decoder<LoginRequest> = 
    Decode.map2
        (fun code id ->
            { LoginCode = code
              LoginToken = id })
        (Decode.field "loginCode" Decode.string)
        (Decode.field "loginToken" Decode.guid)
