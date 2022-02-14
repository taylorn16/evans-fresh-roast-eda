module Types

open Thoth.Json
open System

type Session =
    { Username: string
      ExpirationInSeconds: int }
    
    static member decoder =
        Decode.map2
            (fun unm expir ->
                { Username = unm
                  ExpirationInSeconds = expir })
            (Decode.field "username" Decode.string)
            (Decode.field "expirationInSeconds" Decode.int)

    static member encode session =
        Encode.object
            [ "username", Encode.string session.Username
              "expirationInSeconds", Encode.int session.ExpirationInSeconds ]

type OtpToken = private OtpToken of string

module OtpToken =
    let create tk = OtpToken tk

    let apply f (OtpToken tk) = f tk

    let value = apply id

type CoffeeName = private CoffeeName of string

module CoffeeName =
    let create nm = CoffeeName nm

    let apply f (CoffeeName nm) = f nm

    let value = apply id

    let decoder: Decoder<CoffeeName> =
        Decode.map CoffeeName Decode.string

type CoffeeDescription = private CoffeeDescription of string

module CoffeeDescription =
    let create nm = CoffeeDescription nm

    let apply f (CoffeeDescription nm) = f nm

    let value = apply id

    let decoder: Decoder<CoffeeDescription> =
        Decode.map CoffeeDescription Decode.string

type UsdPrice = private UsdPrice of decimal

module UsdPrice =
    let create pr = UsdPrice pr

    let apply f (UsdPrice pr) = f pr

    let value = apply id

    let decoder: Decoder<UsdPrice> =
        Decode.map UsdPrice Decode.decimal

type OzWeight = private OzWeight of decimal

module OzWeight =
    let create wt = OzWeight wt

    let apply f (OzWeight wt) = f wt

    let value = apply id

    let decoder: Decoder<OzWeight> =
        Decode.map OzWeight Decode.decimal

type Coffee =
    { Name: CoffeeName
      Description: CoffeeDescription
      PricePerBag: UsdPrice
      WeightPerBag: OzWeight }

    static member encode coffee =
        Encode.object
            [ "name", Encode.string <| CoffeeName.value coffee.Name
              "description", Encode.string <| CoffeeDescription.value coffee.Description
              "pricePerBag", Encode.decimal <| UsdPrice.value coffee.PricePerBag
              "weightPerBag", Encode.decimal <| OzWeight.value coffee.WeightPerBag ]

    static member decoder =
        Decode.map4
            (fun nm desc pr wt ->
                { Name = nm
                  Description = desc
                  PricePerBag = pr
                  WeightPerBag = wt })
            (Decode.field "name" CoffeeName.decoder)
            (Decode.field "description" CoffeeDescription.decoder)
            (Decode.field "pricePerBag" UsdPrice.decoder)
            (Decode.field "weightPerBag" OzWeight.decoder)

type AsyncApiEventResponse =
    { Message: string
      AggregateId: Guid
      EventId: Guid }

    static member decoder =
        Decode.map3
            (fun msg agId evId ->
                { Message = msg
                  AggregateId = agId
                  EventId = evId })
            (Decode.field "message" Decode.string)
            (Decode.field "aggregateId" Decode.guid)
            (Decode.field "eventId" Decode.guid)
