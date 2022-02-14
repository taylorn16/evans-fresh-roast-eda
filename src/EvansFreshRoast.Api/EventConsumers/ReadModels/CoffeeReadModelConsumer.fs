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
open Microsoft.AspNetCore.SignalR

type CoffeeReadModelConsumer
    ( logger: ILogger<CoffeeReadModelConsumer>,
      compositionRoot: CompositionRoot,
      testHub: IHubContext<TestHub> ) =
    inherit EventConsumerBase<Coffee, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.coffee",
        "domain.events.coffee.readModel",
        decodeDomainEvent decodeCoffeeEvent
    )

    override _.handleEvent event =
        let sendCoffeeMsg (s: string) = testHub.Clients.All.SendAsync("TestMessageReceived", s)

        CoffeeRepository.updateCoffee
            compositionRoot.ReadStoreConnectionString
            event
