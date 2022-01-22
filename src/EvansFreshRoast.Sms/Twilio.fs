namespace EvansFreshRoast.Sms

open EvansFreshRoast.Domain
open Twilio.Rest.Api.V2010.Account
open Twilio.Types

module Twilio =
    let sendSms fromPhoneNumber toPhoneNumber message =
        async {
            try
                MessageResource.Create(
                ``to``=PhoneNumber(UsPhoneNumber.value toPhoneNumber),
                ``from``=PhoneNumber(UsPhoneNumber.value fromPhoneNumber),
                ``body``=SmsMsg.value message) |> ignore

                return Ok ()
            with
            | _ ->
                return Error "Sending SMS failed, yo."
        }
        
