module Types

type OtpToken = private OtpToken of string
module OtpToken =
    let create tk = OtpToken tk

    let apply f (OtpToken tk) = f tk

    let value = apply id
