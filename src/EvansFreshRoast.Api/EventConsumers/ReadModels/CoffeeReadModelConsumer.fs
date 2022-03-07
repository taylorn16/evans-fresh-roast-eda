namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.ReadModels
open EvansFreshRoast.Utils
open Microsoft.AspNetCore.SignalR
open Thoth.Json.Net

type CoffeeReadModelConsumer
    ( logger: ILogger<CoffeeReadModelConsumer>,
      compositionRoot: CompositionRoot,
      domainEventsHub: IHubContext<DomainEventsHub> ) =
    inherit EventConsumerBase<Coffee, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.coffee",
        "domain.events.coffee.readModel",
        decodeDomainEvent decodeCoffeeEvent,
        domainEventsHub
    )

    override _.handleEvent event =
        let payload = event |> encodeDomainEvent encodeCoffeeEvent |> Encode.toString 2
        
        CoffeeRepository.updateCoffee
            compositionRoot.ReadStoreConnectionString
            event
        |> Async.map (Result.map <| fun _ -> Some payload)
        