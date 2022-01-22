namespace EvansFreshRoast.EventConsumers.ReadModels

open EvansFreshRoast.EventConsumers
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.Framework
open EvansFreshRoast.ReadModels

type CoffeeReadModelConsumer(logger: ILogger<CoffeeReadModelConsumer>) =
    inherit EventConsumerBase<Coffee, Event>(
        logger,
        "domain.events",
        "domain.events.coffee",
        "domain.events.coffee.readModel",
        decodeDomainEvent decodeCoffeeEvent
    )

    // TODO: inject this dependency from config/environment
    let connectionString =
        // "Host=readmodelsdb;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        "Host=localhost;Port=2345;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        |> ConnectionString.create

    override _.handleEvent event = CoffeeRepository.updateCoffee connectionString event