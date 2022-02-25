namespace EvansFreshRoast.Api

open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Thoth.Json.Net

module HttpHandlers =
    let useRequestDecoder (decoder: Decoder<'a>) (createHandler: 'a -> HttpHandler): HttpHandler =
        fun next ctx -> task {
            let! body = ctx.ReadBodyFromRequestAsync()
            
            match Decode.fromString decoder body with
            | Ok a ->
                let innerHandler = createHandler a
                return! innerHandler next ctx
            | Error decoderErr ->
                return! RequestErrors.BAD_REQUEST $"{decoderErr}" next ctx    
        }

    let authenticate: HttpHandler =
        requiresAuthentication
            (challenge JwtBearerDefaults.AuthenticationScheme >=> text "Please authenticate.")