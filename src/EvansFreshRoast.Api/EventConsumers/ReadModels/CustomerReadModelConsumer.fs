namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.ReadModels

type CustomerReadModelConsumer
    ( logger: ILogger<CustomerReadModelConsumer>,
      compositionRoot: CompositionRoot ) =
    inherit EventConsumerBase<Customer, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.customer",
        "domain.events.customer.readModel",
        decodeDomainEvent decodeCustomerEvent
    )

    override _.handleEvent event =
        CustomerRepository.updateCustomer
            compositionRoot.ReadStoreConnectionString
            event
