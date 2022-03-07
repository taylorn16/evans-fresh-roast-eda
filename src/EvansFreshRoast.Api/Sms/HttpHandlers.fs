module EvansFreshRoast.Api.Sms.HttpHandlers

open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open EvansFreshRoast.Api.Composition
open Twilio.AspNet.Common
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open EvansFreshRoast.Sms.IncomingCommandParser

let twilioResponse =
    sprintf
        """
        <Response>
            <Message>
                <Body>%s</Body>
            </Message>
        </Response>
        """

let sendTwilioResponse msg: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        ctx.SetHttpHeader("Content-Type", "application/xml")
        return! ctx.WriteStringAsync(twilioResponse msg)
    }

let verifyTwilioId (compositionRoot: CompositionRoot): HttpHandler =
    // TODO: authenticate the request with the auth token?
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let! request = ctx.BindModelAsync<SmsRequest>()

        if request.AccountSid = compositionRoot.TwilioAccountSid then
            return! next ctx
        else
            let logger = ctx.GetLogger("verifyTwilioId")
            logger.LogWarning("Received request to SMS webhook with invalid Twilio Account Sid.")
            return! RequestErrors.FORBIDDEN "Invalid request." next ctx
    }

let logOnlyTwilioError (err: string): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("logOnlyTwilioError")
        logger.LogError(err)
        
        return! Successful.OK "" next ctx
    }

let genericTwilioError (err: string): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("genericTwilioError")
        logger.LogError(err)

        let msg =
            "Uh oh! Something went wrong on our end. "
            + "Please try again in a few minutes, "
            + "or just reach out to Evan directly."
        return! (sendTwilioResponse msg) next ctx
    }

let orderParsingTwilioError (incomingMsg: SmsMsg) (respMsg: string): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let logger = ctx.GetLogger("orderParsingTwilioError")
        logger.LogDebug($"Failed to parse order: \"%s{SmsMsg.value incomingMsg}\". Response given: \"%s{respMsg}\"")

        return! (sendTwilioResponse respMsg) next ctx
    }

let receiveIncomingSms (compositionRoot: CompositionRoot): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let! smsRequest = ctx.BindModelAsync<SmsRequest>()

        match SmsMsg.create smsRequest.Body with
        | Ok message ->
            let phoneNumber = UsPhoneNumber.create smsRequest.From |> unsafeAssertOk
        
            match! compositionRoot.GetCustomerByPhoneNumber phoneNumber with
            | Some(customerId, _) ->
                let getAllRoasts () = async {
                    let! roasts = compositionRoot.GetAllRoasts()
                        
                    return roasts
                    |> List.map (fun rsv -> rsv.Id, rsv.RoastStatus)
                }

                let! command =
                    message |> parse getAllRoasts compositionRoot.GetNow customerId

                match command with
                | Ok cmd ->
                    match cmd with
                    | RoastCommand(roastId, roastCmd) ->
                        let! handleCommand =
                            Async.StartAsTask(
                                compositionRoot.GetRoastCommandHandler(),
                                cancellationToken=ctx.RequestAborted)

                        match! handleCommand roastId roastCmd with
                        | Ok _ ->
                            return! Successful.ok (text "") next ctx

                        | Error handlerErr ->
                            return! (genericTwilioError $"{handlerErr}") next ctx

                    | CustomerCommand(customerId, customerCmd) ->
                        let handleCommand = compositionRoot.CustomerCommandHandler

                        match! handleCommand customerId customerCmd with
                        | Ok _ ->
                            return! Successful.ok (text "") next ctx

                        | Error handlerErr ->
                            return! (genericTwilioError $"{handlerErr}") next ctx

                | Error parseErr ->
                    let resp =
                        match parseErr with
                        | NoOpenRoast attemptedAction ->
                            $"Sorry, all roasts are closed at the moment, so you can't {attemptedAction}. "
                            + "Please reach out to Evan directly if you need help with an order."
                            
                        | LineParseErrors lineErrs ->
                            foldLineParseErrors lineErrs
                    
                    return! (orderParsingTwilioError message resp) next ctx

            | None ->
                let logMsg = $"No customer found with phone number %s{UsPhoneNumber.value phoneNumber}"
                return! logOnlyTwilioError logMsg next ctx

        | Error _ ->
            let logMsg = $"Received a text message from {smsRequest.From}, but it was empty."
            return! logOnlyTwilioError logMsg next ctx
    }
