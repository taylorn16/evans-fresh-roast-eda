namespace EvansFreshRoast.Api.EventConsumers.ReadModels

open EvansFreshRoast.Api
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Logging
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.ReadModels
open EvansFreshRoast.Utils
open Thoth.Json.Net

type CustomerReadModelConsumer
    ( logger: ILogger<CustomerReadModelConsumer>,
      compositionRoot: CompositionRoot,
      domainEventsHub: IHubContext<DomainEventsHub> ) =
    inherit EventConsumerBase<Customer, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.customer",
        "domain.events.customer.readModel",
        decodeDomainEvent decodeCustomerEvent,
        domainEventsHub
    )

    override _.handleEvent event =
        let payload = event |> encodeDomainEvent encodeCustomerEvent |> Encode.toString 2
        
        CustomerRepository.updateCustomer
            compositionRoot.ReadStoreConnectionString
            event
        |> Async.map (Result.map <| fun _ -> Some payload)
