module EvansFreshRoast.EventStore.Roast

open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.EventStore
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils

let loadRoastEvents connectionString =
    Db.loadEvents connectionString decodeRoastEvent DatabaseError SerializationError

let saveRoastEvent connectionString (event: DomainEvent<Roast, Event>) =
    let getEventName =
        function
        | Created _ -> "Roast Created"
        | OrderPlaced _ -> "Order Placed"
        | OrderCancelled _ -> "Order Cancelled"
        | OrderConfirmed _ -> "Order Confirmed"
        | CoffeesAdded _ -> "Coffees Added"
        | CustomersAdded _ -> "Customers Added"
        | RoastDatesChanged _ -> "Roast Dates Changed"
        | RoastStarted -> "Roast Started"
        | RoastCompleted -> "Roast Completed"

    Db.saveEvent connectionString encodeRoastEvent "Roast" getEventName DatabaseError event
    |> Async.map (
        Result.bind (fun _ ->
            Publisher.publishRoastEvent event
            |> Result.mapError PublishingError)
    )
