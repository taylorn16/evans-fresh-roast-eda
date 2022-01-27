namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.ReadModels

type CoffeeReadModelConsumer
    ( logger: ILogger<CoffeeReadModelConsumer>,
      compositionRoot: CompositionRoot ) =
    inherit EventConsumerBase<Coffee, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.coffee",
        "domain.events.coffee.readModel",
        decodeDomainEvent decodeCoffeeEvent
    )

    override _.handleEvent event =
        CoffeeRepository.updateCoffee
            compositionRoot.ReadStoreConnectionString
            event
