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
open EvansFreshRoast.Utils
open Microsoft.AspNetCore.SignalR
open Thoth.Json.Net

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
        let sendCoffeeMsg (payload: string) =
            testHub.Clients.All.SendAsync("Send", payload) // MUST BE "Send"!
            |> Async.AwaitTask
            
        async {
            let! updateResult =
                CoffeeRepository.updateCoffee
                    compositionRoot.ReadStoreConnectionString
                    event
                    
            match updateResult with
            | Ok () ->
                let payload = event |> encodeDomainEvent encodeCoffeeEvent |> Encode.toString 2
                do! sendCoffeeMsg payload
                return Ok ()
            
            | Error ex ->
                return Error ex
        }
        