namespace EvansFreshRoast.Api.EventConsumers.Sms

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open Microsoft.Extensions.Logging
open EvansFreshRoast.Sms

type CustomerSmsConsumer
    ( logger: ILogger<CustomerSmsConsumer>,
      compositionRoot: CompositionRoot ) =
    inherit EventConsumerBase<Customer, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.customer",
        "domain.events.customer.sms",
        decodeDomainEvent decodeCustomerEvent
    )

    override _.handleEvent event =
        Customer.handleEvent
            (Twilio.sendSms compositionRoot.TwilioFromPhoneNumber)
            compositionRoot.GetCustomer
            event
