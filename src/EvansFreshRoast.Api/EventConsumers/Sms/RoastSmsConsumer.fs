namespace EvansFreshRoast.Api.EventConsumers.Sms

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api.EventConsumers
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open Microsoft.Extensions.Logging
open EvansFreshRoast.Sms

type RoastSmsConsumer
    ( logger: ILogger<CustomerSmsConsumer>,
      compositionRoot: CompositionRoot ) =
    inherit EventConsumerBase<Roast, Event>(
        logger,
        compositionRoot.RabbitMqConnectionFactory,
        "domain.events",
        "domain.events.roast",
        "domain.events.roast.sms",
        decodeDomainEvent decodeRoastEvent
    )

    override _.handleEvent event =
        Roast.handleEvent
            (Twilio.sendSms compositionRoot.TwilioFromPhoneNumber)
            compositionRoot.GetAllCoffees
            compositionRoot.GetRoast
            compositionRoot.GetAllCustomers
            compositionRoot.GetCustomer
            event
