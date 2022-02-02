module EvansFreshRoast.EventStore.Coffee

open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.EventStore
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Utils
open RabbitMQ.Client

let loadCoffeeEvents connectionString =
    Db.loadEvents connectionString decodeCoffeeEvent DatabaseError SerializationError

let saveCoffeeEvent connectionString (connectionFactory: IConnectionFactory) (event: DomainEvent<Coffee, Event>) =
    let getEventName =
        function
        | Created _ -> "Coffee Created"
        | Updated _ -> "Coffee Updated"
        | Activated _ -> "Coffee Activated"
        | Deactivated _ -> "Coffee Deactivated"

    Db.saveEvent connectionString encodeCoffeeEvent "Coffee" getEventName DatabaseError event
    |> Async.map (
        Result.bind (fun _ -> 
            Publisher.publishCoffeeEvent connectionFactory event
            |> Result.mapError PublishingError)
    )
