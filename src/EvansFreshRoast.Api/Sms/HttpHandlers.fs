module EvansFreshRoast.Api.Sms.HttpHandlers

open Giraffe
open Microsoft.AspNetCore.Http
open EvansFreshRoast.Api.Composition
open Twilio.AspNet.Common
open EvansFreshRoast.Domain
open EvansFreshRoast.Utils
open EvansFreshRoast.Sms.IncomingCommandParser

let receiveIncomingSms (compositionRoot: CompositionRoot): HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let! smsRequest = ctx.BindJsonAsync<SmsRequest>()

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
                            let response = Twilio.TwiML.MessagingResponse()
                            response.Message($"Something went wrong: {handlerErr}") |> ignore

                            return! Successful.OK (response.ToString()) next ctx // TODO: how do XML?

                    | CustomerCommand(customerId, customerCmd) ->
                        let handleCommand = compositionRoot.CustomerCommandHandler

                        match! handleCommand customerId customerCmd with
                        | Ok _ ->
                            return! Successful.ok (text "") next ctx

                        | Error handlerErr ->
                            let response = Twilio.TwiML.MessagingResponse()
                            response.Message($"Something went wrong: {handlerErr}") |> ignore

                            return! Successful.OK (response.ToString()) next ctx

                | Error parseErr ->
                    let response = Twilio.TwiML.MessagingResponse()
                    response.Message($"Couldn't parse: {parseErr}") |> ignore

                    return! Successful.OK (response.ToString()) next ctx

            | None ->
                // No customer
                return! Successful.ok (text "") next ctx

        | Error _ ->
            // No message body??
            return! Successful.ok (text "") next ctx
    }
