module Api

open EvansFreshRoast.Dto
open Fable.SimpleHttp
#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open Types
open System

// TODO: this file is a mess, yo

let private baseUri = "/api/v1"

let getAuthCode phoneNumber = async {
    let! response =
        Http.request $"{baseUri}/auth/code?phoneNumber={phoneNumber}"
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
        Http.request $"{baseUri}/auth/login"
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

let makePostRequest uri json decoder = async {
    let! response =
        Http.request uri
        |> Http.method POST
        |> Http.header (Headers.contentType "application/json")
        |> Http.header (Headers.accept "application/json")
        |> Http.content (BodyContent.Text json)
        |> Http.send

    match response.statusCode with
    | 200 ->
        return response.responseText
        |> Decode.fromString decoder

    | sc ->
        return Error $"{sc}: Error making POST request to {uri}"
}

let saveCoffee coffee =
    makePostRequest
        $"{baseUri}/coffees"
        (Encode.Auto.toString<CreateCoffeeRequest>(2, coffee))
        EventAcceptedResponse.decoder

let saveCustomer customer =
    makePostRequest
        $"{baseUri}/customers"
        (Encode.Auto.toString<CreateCustomerRequest>(2, customer))
        EventAcceptedResponse.decoder

let decodeCoffee: Decoder<Coffee> =
    Decode.object <| fun get ->
        { Id = get.Required.Field "id" Decode.guid
          Name = get.Required.Field "name" Decode.string
          Description = get.Required.Field "name" Decode.string
          PricePerBag = get.Required.Field "pricePerBag" Decode.decimal
          WeightPerBag = get.Required.Field "weightPerBag" Decode.decimal
          IsActive = get.Required.Field "isActive" Decode.bool }

let getCoffee (id: Guid) = async {
    let! response =
        Http.request $"{baseUri}/coffees/{id}"
        |> Http.method GET
        |> Http.send
    
    match response.statusCode with
    | 200 ->
        return Decode.fromString decodeCoffee response.responseText
    
    | sc ->
        return Error $"{sc}: Error fetching coffee."
}

let getCoffees = async {
    let! response =
        Http.request $"{baseUri}/coffees"
        |> Http.method GET
        |> Http.send
    
    match response.statusCode with
    | 200 ->
        return Decode.fromString (Decode.list decodeCoffee) response.responseText
    
    | sc ->
        return Error $"{sc}: Error fetching coffees."
}

let decodeCustomer: Decoder<Customer> =
    Decode.object <| fun get ->
        { Id = get.Required.Field "id" Decode.guid
          Name = get.Required.Field "name" Decode.string
          PhoneNumber = get.Required.Field "phoneNumber" Decode.string }

let getCustomers = async {
    let! response =
        Http.request $"{baseUri}/customers"
        |> Http.method GET
        |> Http.send
    
    match response.statusCode with
    | 200 ->
        return Decode.fromString (Decode.list decodeCustomer) response.responseText
    
    | sc ->
        return Error $"{sc}: Error fetching customers."
}

let refreshToken = async {
    let! response =
        Http.request $"{baseUri}/auth/refresh-token"
        |> Http.method GET
        |> Http.send
        
    match response.statusCode with
    | 200 ->
        return Decode.fromString Decode.datetimeOffset response.responseText
        
    | sc ->
        return Error $"{sc}: Error refreshing token."
}
