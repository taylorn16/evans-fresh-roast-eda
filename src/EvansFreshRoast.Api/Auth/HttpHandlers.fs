module EvansFreshRoast.Api.Auth.HttpHandlers

open Microsoft.AspNetCore.Http
open Giraffe
open EvansFreshRoast.Api.HttpHandlers
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Auth
open EvansFreshRoast.Sms
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open EvansFreshRoast.Framework
open Microsoft.Extensions.Logging
open NodaTime
open System
open EvansFreshRoast.Dto
open EvansFreshRoast.Api.Auth.RequestDecoders

let getLoginCode (compositionRoot: CompositionRoot): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("getLoginCode")

        let phoneNumber =
            ctx.BindQueryString<GetAuthCodeRequest>()
            |> fun req -> req.PhoneNumber
            |> UsPhoneNumber.create
            |> unsafeAssertOk

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

let login (compositionRoot: CompositionRoot): HttpHandler =
    fun request (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("login")

        let getUserLogin = Repository.getUserLogin compositionRoot.ReadStoreConnectionString
        let validateUserLogin = Repository.validateUserLogin compositionRoot.ReadStoreConnectionString
        let now = OffsetDateTime.FromDateTimeOffset(DateTimeOffset.Now)
        let id: Id<UserLogin> = request.LoginToken |> Id.create |> unsafeAssertOk

        match! getUserLogin id with
        | Ok userLogin ->
            let codesMatch = LoginCode.value userLogin.Code = request.LoginCode
            let loginNotExpired = userLogin.ExpiresAt.ToInstant() > now.ToInstant()

            match codesMatch, loginNotExpired, userLogin.HasBeenUsed with
            | true, true, false ->
                match! validateUserLogin userLogin.Id with
                | Ok(userId, userName) ->
                    let token = Jwt.create compositionRoot.JwtConfig userId
                    let cookie = $"efr.auth.token={token}; SameSite=Strict; Secure; HttpOnly; Max-Age=900"
                    let session =
                        {| username = UserName.value userName
                           expirationInSeconds = 900 |}
                    
                    return! (setHttpHeader "Set-Cookie" cookie
                             >=> Successful.OK session) next ctx

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
