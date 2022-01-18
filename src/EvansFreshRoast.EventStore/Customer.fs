module EvansFreshRoast.EventStore.Customer

open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.EventStore
open EvansFreshRoast.Domain.Aggregate
open EvansFreshRoast.Utils

type CustomerEventStoreError =
    | DataError of EventStoreDbError
    | SerializationError of string
    | PublishingError of exn

let loadCustomerEvents connectionString =
    EventStoreDb.loadEvents connectionString decodeCustomerEvent DataError SerializationError

let saveCustomerEvent connectionString (event: DomainEvent<Customer, Event>) =
    let getEventName =
        function
        | Updated _ -> "Customer Updated"
        | Subscribed _ -> "Customer Subscribed"
        | Unsubscribed _ -> "Customer Unsubscribed"

    EventStoreDb.saveEvent connectionString encodeCustomerEvent "Customer" getEventName DataError event
    |> Async.map (
        Result.bind (fun _ -> 
            Publisher.publishCustomerEvent event
            |> Result.mapError PublishingError)
    )
