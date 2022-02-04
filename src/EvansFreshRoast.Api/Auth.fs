module EvansFreshRoast.Api.Auth

open Microsoft.AspNetCore.Http
open Giraffe
open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Auth
open EvansFreshRoast.Sms
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open EvansFreshRoast.Framework
open EvansFreshRoast.Serialization.Common
open Microsoft.Extensions.Logging
open Thoth.Json.Net
open NodaTime
open System

let getLoginCode (compositionRoot: CompositionRoot): HttpHandler =
    fun phoneNumber (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("getLoginCode")

        let getUser = Repository.getUser compositionRoot.ReadStoreConnectionString
        let createUserLogin = Repository.createUserLogin compositionRoot.ReadStoreConnectionString

        match! getUser phoneNumber with
        | Ok user ->
            match! createUserLogin user.Id with
            | Ok (userLoginId, loginCode) ->
                let sendSms = Twilio.sendSms compositionRoot.TwilioFromPhoneNumber
                let msg =
                    $"Your Evan's Fresh Roast security code is {LoginCode.value loginCode}"
                    |> SmsMsg.create
                    |> unsafeAssertOk

                match! sendSms user.PhoneNumber msg with
                | Ok () ->
                    return! Successful.ok (text ((userLoginId |> Id.value).ToString())) next ctx

                | Error ex ->
                    logger.LogError(ex, "Error sending login code SMS.")
                    return! ServerErrors.INTERNAL_ERROR "" next ctx

            | Error ex ->
                logger.LogError(ex, "Error creating user login.")
                return! ServerErrors.INTERNAL_ERROR "" next ctx

        | Error ex ->
            logger.LogError(ex, $"Couldn't find user with phone number {UsPhoneNumber.formatE164 phoneNumber}")
            return! ServerErrors.INTERNAL_ERROR "" next ctx            
    }
    |> useRequestDecoder decodePhoneNumber

[<CLIMutable>]
type LoginRequest =
    { LoginCode: string
      LoginToken: Id<UserLogin> }

let decodeLoginRequest: Decoder<LoginRequest> = 
    Decode.map2
        (fun code id ->
            { LoginCode = code
              LoginToken = id })
        (Decode.field "loginCode" Decode.string)
        (Decode.field "loginToken" decodeId)

let login (compositionRoot: CompositionRoot): HttpHandler =
    fun request (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("login")

        let getUserLogin = Repository.getUserLogin compositionRoot.ReadStoreConnectionString
        let validateUserLogin = Repository.validateUserLogin compositionRoot.ReadStoreConnectionString
        let now = OffsetDateTime.FromDateTimeOffset(DateTimeOffset.Now)

        match! getUserLogin request.LoginToken with
        | Ok userLogin ->
            let codesMatch = LoginCode.value userLogin.Code = request.LoginCode
            let loginNotExpired = userLogin.ExpiresAt.ToInstant() > now.ToInstant()

            match codesMatch, loginNotExpired, userLogin.HasBeenUsed with
            | true, true, false ->
                match! validateUserLogin userLogin.Id with
                | Ok userId ->
                    let token = Jwt.create compositionRoot.JwtConfig userId
                    
                    let setCookies =
                        setHttpHeader "Set-Cookie" $"auth_token={token}; SameSite=Strict; Secure; HttpOnly; Max-Age=900"
                        >=> setHttpHeader "Set-Cookie" $"logged_in=true; SameSite=Strict; Secure; Max-Age=900"
                    return! (setCookies >=> Successful.ok (text token)) next ctx

                | Error ex ->
                    logger.LogError(ex, "Error validating user ")
                    return! ServerErrors.INTERNAL_ERROR "" next ctx

            | _ ->
                logger.LogWarning(
                    $"Invalid login attempt. UserLoginId = %A{userLogin.Id}; "
                    + $"codesMatch = {codesMatch}; "
                    + $"loginExpired = {not loginNotExpired}; "
                    + $"loginAlreadyUsed = {userLogin.HasBeenUsed}.")
                return! RequestErrors.FORBIDDEN "Invalid login." next ctx

        | Error ex ->
            logger.LogError(ex, "Error looking up user login.")
            return! ServerErrors.INTERNAL_ERROR "" next ctx
    }
    |> useRequestDecoder decodeLoginRequest
