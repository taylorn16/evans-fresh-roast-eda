namespace EvansFreshRoast.Api.EventConsumers.Sms

open EvansFreshRoast.Api
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Logging
open EvansFreshRoast.Sms
open EvansFreshRoast.Utils

type CustomerSmsConsumer
    ( logger: ILogger<CustomerSmsConsumer>,
      compositionRoot: CompositionRoot,
      domainEventsHub: IHubContext<DomainEventsHub> ) =
    inherit EventConsumerBase<Customer, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.customer",
        "domain.events.customer.sms",
        decodeDomainEvent decodeCustomerEvent,
        domainEventsHub
    )

    override _.handleEvent event =
        Customer.handleEvent
            (Twilio.sendSms compositionRoot.TwilioFromPhoneNumber)
            compositionRoot.GetCustomer
            event
        |> Async.map (Result.map <| fun _ -> None)
