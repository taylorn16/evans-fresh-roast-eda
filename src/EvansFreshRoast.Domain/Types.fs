namespace EvansFreshRoast.Domain

type SmsMsg = private SmsMsg of NonEmptyString
module SmsMsg =
    let create sms =
        NonEmptyString.create sms |> Result.map SmsMsg

    let apply f (SmsMsg sms) = sms |> NonEmptyString.apply f

    let value = apply id
