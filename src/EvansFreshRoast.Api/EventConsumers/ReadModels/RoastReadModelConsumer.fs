namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.ReadModels
open EvansFreshRoast.Utils
open Thoth.Json.Net

type RoastReadModelConsumer
    ( logger: ILogger<CoffeeReadModelConsumer>,
      compositionRoot: CompositionRoot,
      domainEventsHub: IHubContext<DomainEventsHub> ) =
    inherit EventConsumerBase<Roast, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.roast",
        "domain.events.roast.readModel",
        decodeDomainEvent decodeRoastEvent,
        domainEventsHub
    )

    override _.handleEvent event =
        let payload =
            encodeDomainEvent encodeRoastEvent event
            |> Encode.toString 2
        
        RoastRepository.updateRoast
            compositionRoot.ReadStoreConnectionString
            event
        |> Async.map (Result.map <| fun _ -> Some payload)
