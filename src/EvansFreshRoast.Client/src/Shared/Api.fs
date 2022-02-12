module Api

open Fable.SimpleHttp
open Thoth.Json
open Types

let private baseUri = "/api/v1"

let getAuthCode phoneNumber = async {
    let! response =
        Http.request $"{baseUri}/authcode?phoneNumber={phoneNumber}"
        |> Http.method GET
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Ok <| OtpToken.create response.responseText
    
    | sc ->
        return Error $"{sc}: Error getting auth code."
}

let login token oneTimePassword = async {
    let request =
        Encode.object
            [ "loginCode", Encode.string oneTimePassword
              "loginToken", Encode.string (OtpToken.value token) ]
        |> Encode.toString 2
    
    let! response =
        Http.request $"{baseUri}/login"
        |> Http.method POST
        |> Http.content (BodyContent.Text request)
        |> Http.send

    match response.statusCode with
    | 200 ->
        return
            response.responseText
            |> Decode.fromString Session.decoder
    
    | sc ->
        return Error $"{sc}: Error logging in."
}

let getRoasts() = async {
    let! response =
        Http.request $"{baseUri}/roasts"
        |> Http.method GET
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Ok () // TODO:
    
    | sc ->
        return Error $"{sc}: Error fetching roasts."
}

let getCoffees() = async {
    let! response =
        Http.request $"{baseUri}/coffees"
        |> Http.method GET
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Ok () // TODO:
    
    | sc ->
        return Error $"{sc}: Error fetching roasts."
}

let getCustomers() = async {
    let! response =
        Http.request $"{baseUri}/customers"
        |> Http.method GET
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Ok () // TODO:
    
    | sc ->
        return Error $"{sc}: Error fetching roasts."
}

// TODO: Dtos + decoders for roasts (summary and detail), coffees, customers
// TODO: endpoints/abstractions for making similar requests
