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

let getRoasts: Async<Result<RoastSummary list, string>> = async {
    let! response =
        Http.request $"{baseUri}/roasts"
        |> Http.method GET
        |> Http.send

    match response.statusCode with
    | 200 ->
        return Decode.fromString (Decode.list RoastSummary.decoder) response.responseText
    
    | sc ->
        return Error $"{sc}: Error fetching roasts."
}

let getRoast (id: Guid): Async<Result<RoastDetails, string>> = async {
    let! response =
        Http.request $"{baseUri}/roasts/{id}"
        |> Http.method GET
        |> Http.send
        
    match response.statusCode with
    | 200 ->
        return Decode.fromString RoastDetails.decoder response.responseText
        
    | sc ->
        return Error $"{sc}: Error fetching roast."
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
    | 200
    | 202 ->
        return response.responseText
        |> Decode.fromString decoder

    | sc ->
        return Error $"{sc}: Error making POST request to {uri}"
}

let saveCoffee coffee =
    let encode (coffeeRequest: CreateCoffeeRequest) =
        Encode.object [ "name", Encode.string coffeeRequest.Name
                        "description", Encode.string coffeeRequest.Description
                        "pricePerBag", Encode.decimal coffeeRequest.PricePerBag
                        "weightPerBag", Encode.decimal coffeeRequest.WeightPerBag ]
    
    makePostRequest
        $"{baseUri}/coffees"
        (Encode.toString 2 <| encode coffee)
        EventAcceptedResponse.decoder

let saveCustomer customer =
    makePostRequest
        $"{baseUri}/customers"
        (Encode.Auto.toString<CreateCustomerRequest>(2, customer, CaseStrategy.CamelCase))
        EventAcceptedResponse.decoder

let saveRoast roast =
    makePostRequest
        $"{baseUri}/roasts"
        (Encode.Auto.toString<CreateRoastRequest>(2, roast, CaseStrategy.CamelCase))
        EventAcceptedResponse.decoder

let decodeCoffee: Decoder<Coffee> =
    Decode.object <| fun get ->
        { Id = get.Required.Field "id" Decode.guid
          Name = get.Required.Field "name" Decode.string
          Description = get.Required.Field "description" Decode.string
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

let getCustomers: Async<Result<Customer list, string>> = async {
    let! response =
        Http.request $"{baseUri}/customers"
        |> Http.method GET
        |> Http.send
    
    match response.statusCode with
    | 200 ->
        return Decode.fromString (Decode.list Customer.decoder) response.responseText
    
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

let putRoastCoffees (roastId: Guid) (coffeeIds: Guid list): Async<Result<EventAcceptedResponse, string>> =
    async {
        let requestContent =
            coffeeIds
            |> List.map Encode.guid
            |> Encode.list
            |> Encode.toString 2
        
        let! response =
            Http.request $"{baseUri}/roasts/{roastId}/coffees"
            |> Http.method PUT
            |> Http.header (Headers.contentType "application/json")
            |> Http.header (Headers.accept "application/json")
            |> Http.content (BodyContent.Text requestContent)
            |> Http.send
            
        match response.statusCode with
        | 202 ->
            return Decode.fromString EventAcceptedResponse.decoder response.responseText
            
        | sc ->
            return Error $"{sc}: Error adding coffees to roast."
    }

let putRoastCustomers (roastId: Guid) (customerIds: Guid list): Async<Result<EventAcceptedResponse, string>> =
    async {
        let requestContent =
            customerIds
            |> List.map Encode.guid
            |> Encode.list
            |> Encode.toString 2
        
        let! response =
            Http.request $"{baseUri}/roasts/{roastId}/customers"
            |> Http.method PUT
            |> Http.header (Headers.contentType "application/json")
            |> Http.header (Headers.accept "application/json")
            |> Http.content (BodyContent.Text requestContent)
            |> Http.send
            
        match response.statusCode with
        | 202 ->
            return Decode.fromString EventAcceptedResponse.decoder response.responseText
            
        | sc ->
            return Error $"{sc}: Error adding customers to roast."
    }

let postOpenRoast (roastId: Guid): Async<Result<EventAcceptedResponse, string>> =
    async {
        let! response =
            Http.request $"{baseUri}/roasts/{roastId}/open"
            |> Http.method POST
            |> Http.header (Headers.accept "application/json")
            |> Http.send
            
        match response.statusCode with
        | 202 ->
            return Decode.fromString EventAcceptedResponse.decoder response.responseText
            
        | sc ->
            return Error $"{sc}: Error opening roast."
    }

let postRoastComplete (roastId: Guid): Async<Result<EventAcceptedResponse, string>> =
    async {
        let! response =
            Http.request $"{baseUri}/roasts/{roastId}/complete"
            |> Http.method POST
            |> Http.header (Headers.accept "application/json")
            |> Http.send
            
        match response.statusCode with
        | 202 ->
            return Decode.fromString EventAcceptedResponse.decoder response.responseText
            
        | sc ->
            return Error $"{sc}: Error closing roast."
    }

let postOrderPaid (roastId: Guid) (customerId: Guid): Async<Result<EventAcceptedResponse, string>> =
    async {
        let content =
            Encode.string "Unknown"
            |> Encode.toString 2
        
        let! response =
            Http.request $"{baseUri}/roasts/{roastId}/customers/{customerId}/invoice"
            |> Http.method PUT
            |> Http.header (Headers.accept "application/json")
            |> Http.header (Headers.contentType "application/json")
            |> Http.content (BodyContent.Text content)
            |> Http.send
            
        match response.statusCode with
        | 202 ->
            return Decode.fromString EventAcceptedResponse.decoder response.responseText
            
        | sc ->
            return Error $"{sc}: Error marking invoice paid."
    }

let postFollowUp (roastId: Guid): Async<Result<EventAcceptedResponse, string>> =
    async {let! response =
            Http.request $"{baseUri}/roasts/{roastId}/follow-up"
            |> Http.method POST
            |> Http.header (Headers.accept "application/json")
            |> Http.send
            
        match response.statusCode with
        | 202 ->
            return Decode.fromString EventAcceptedResponse.decoder response.responseText
            
        | sc ->
            return Error $"{sc}: Error marking invoice paid."
    }
