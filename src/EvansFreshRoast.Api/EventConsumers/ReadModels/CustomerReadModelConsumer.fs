namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api.EventConsumers
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Framework
open EvansFreshRoast.ReadModels

type CustomerReadModelConsumer(logger: ILogger<CustomerReadModelConsumer>) =
    inherit EventConsumerBase<Customer, Event>(
        logger,
        "domain.events",
        "domain.events.customer",
        "domain.events.customer.readModel",
        decodeDomainEvent decodeCustomerEvent
    )

    // TODO: inject this dependency from config/environment
    let connectionString =
        // "Host=readmodelsdb;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        "Host=localhost;Port=2345;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        |> ConnectionString.create

    override _.handleEvent event = CustomerRepository.updateCustomer connectionString event