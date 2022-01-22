module EvansFreshRoast.EventStore.Coffee

open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.EventStore
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.Utils

type CustomerEventStoreError =
    | DataError of EventStoreDbError
    | SerializationError of string
    | PublishingError of exn

let loadCoffeeEvents connectionString =
    Db.loadEvents connectionString decodeCoffeeEvent DataError SerializationError

let saveCoffeeEvent connectionString (event: DomainEvent<Coffee, Event>) =
    let getEventName =
        function
        | Created _ -> "Coffee Created"
        | Updated _ -> "Coffee Updated"
        | Activated _ -> "Coffee Activated"
        | Deactivated _ -> "Coffee Deactivated"

    Db.saveEvent connectionString encodeCoffeeEvent "Coffee" getEventName DataError event
    |> Async.map (
        Result.bind (fun _ -> 
            Publisher.publishCoffeeEvent event
            |> Result.mapError PublishingError)
    )
