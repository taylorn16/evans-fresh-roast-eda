namespace EvansFreshRoast.EventStore

open RabbitMQ.Client
open EvansFreshRoast.Framework
open Thoth.Json.Net
open System.Text
open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Domain

module Publisher =
    let exchangeName = "domain.events"

    let connectionFactory = ConnectionFactory(
        HostName = "rabbitmq", //"rabbitmq"
        UserName = "guest",
        Password = "guest",
        Port = 5672)

    let private publishEvent route (encoder: Encoder<'Event>) (domainEvent: DomainEvent<_, 'Event>) =
        use connection = connectionFactory.CreateConnection()
        use channel = connection.CreateModel()

        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true, false, null)

        let body =
            Encode.toString 2 (encodeDomainEvent encoder domainEvent)
            |> Encoding.UTF8.GetBytes

        try
            channel.BasicPublish(exchangeName, route, true, null, body)
            Ok ()
        with ex ->
            Error ex

    let publishRoastEvent: DomainEvent<Roast, Roast.Event> -> Result<unit, exn> =
        publishEvent "domain.events.roast" encodeRoastEvent

    let publishCoffeeEvent: DomainEvent<Coffee, Coffee.Event> -> Result<unit, exn> =
        publishEvent "domain.events.coffee" encodeCoffeeEvent

    let publishCustomerEvent: DomainEvent<Customer, Customer.Event> -> Result<unit, exn> =
        publishEvent "domain.events.customer" encodeCustomerEvent
