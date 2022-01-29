namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.ReadModels

type RoastReadModelConsumer
    ( logger: ILogger<CoffeeReadModelConsumer>,
      compositionRoot: CompositionRoot ) =
    inherit EventConsumerBase<Roast, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.roast",
        "domain.events.roast.readModel",
        decodeDomainEvent decodeRoastEvent
    )

    override _.handleEvent event =
        RoastRepository.updateRoast
            compositionRoot.ReadStoreConnectionString
            event
