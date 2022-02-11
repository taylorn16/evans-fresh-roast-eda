module Api

open Fable.SimpleHttp
open Thoth.Json

let private baseUri = "/api/v1"

let getAuthCode phoneNumber = async {
    let! response =
        Http.request $"{baseUri}/authcode?phoneNumber={phoneNumber}"
        |> Http.method GET
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Ok response.responseText
    
    | sc ->
        return Error $"{sc}: Error getting auth code."
}


let login token oneTimePassword = async {
    let request =
        Encode.object
            [ "loginCode", Encode.string oneTimePassword
              "loginToken", Encode.string token ]
        |> Encode.toString 2
    
    let! response =
        Http.request $"{baseUri}/login"
        |> Http.method POST
        |> Http.content (BodyContent.Text request)
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Ok ()
    
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
        return Ok ()
    
    | sc ->
        return Error $"{sc}: Error fetching roasts."
}
