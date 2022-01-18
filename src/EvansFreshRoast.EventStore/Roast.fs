module EvansFreshRoast.EventStore.Roast

open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.EventStore
open EvansFreshRoast.Domain.Aggregate
open EvansFreshRoast.Utils

type RoastEventStoreError =
    | DataError of EventStoreDbError
    | SerializationError of string
    | PublishingError of exn

let loadRoastEvents connectionString =
    EventStoreDb.loadEvents connectionString decodeRoastEvent DataError SerializationError

let saveRoastEvent connectionString (event: DomainEvent<Roast, Event>) =
    let getEventName =
        function
        | OrderPlaced _ -> "Order Placed"
        | OrderCancelled _ -> "Order Cancelled"
        | OrderConfirmed _ -> "Order Confirmed"
        | CoffeesAdded _ -> "Coffees Added"
        | CustomersAdded _ -> "Customers Added"
        | RoastDatesChanged _ -> "Roast Dates Changed"
        | RoastStarted -> "Roast Started"
        | RoastCompleted -> "Roast Completed"

    EventStoreDb.saveEvent connectionString encodeRoastEvent "Roast" getEventName DataError event
    |> Async.map (
        Result.bind (fun _ ->
            Publisher.publishRoastEvent event
            |> Result.mapError PublishingError)
    )
