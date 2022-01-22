namespace EvansFreshRoast.EventConsumers.Sms

open EvansFreshRoast.EventConsumers
open EvansFreshRoast.Framework
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Utils
open EvansFreshRoast.ReadModels
open Microsoft.Extensions.Logging
open EvansFreshRoast.Sms

type CustomerSmsConsumer(logger: ILogger<CustomerSmsConsumer>) =
    inherit EventConsumerBase<Customer, Event>(
        logger,
        "domain.events",
        "domain.events.customer",
        "domain.events.customer.readModel",
        decodeDomainEvent decodeCustomerEvent
    )

    // TODO: inject this from config/env
    let fromPhoneNumber = UsPhoneNumber.create "1111111111" |> unsafeAssertOk

    // TODO: inject this dependency from config/environment
    let connectionString =
        // "Host=readmodelsdb;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        "Host=localhost;Port=2345;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        |> ConnectionString.create

    override _.handleEvent event =
        Customer.handleEvent
            (Twilio.sendSms fromPhoneNumber)
            (CustomerRepository.getCustomer connectionString)
            event
