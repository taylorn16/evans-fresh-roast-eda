namespace EvansFreshRoast.Sms

open EvansFreshRoast.Domain
open Twilio.Rest.Api.V2010.Account
open Twilio.Types

module Twilio =
    let sendSms fromPhoneNumber toPhoneNumber message =
        async {
            try
                MessageResource.Create(
                ``to`` = PhoneNumber(UsPhoneNumber.formatE164 toPhoneNumber),
                ``from`` = PhoneNumber(UsPhoneNumber.formatE164 fromPhoneNumber),
                ``body`` = SmsMsg.value message) |> ignore

                return Ok ()
            with
            | ex ->
                return Error ex
        }
        
