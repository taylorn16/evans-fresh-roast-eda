module EvansFreshRoast.EventStore.Customer

open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.EventStore
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils

type CustomerEventStoreError =
    | DataError of EventStoreDbError
    | SerializationError of string
    | PublishingError of exn

let loadCustomerEvents connectionString =
    Db.loadEvents connectionString decodeCustomerEvent DataError SerializationError

let saveCustomerEvent connectionString (event: DomainEvent<Customer, Event>) =
    let getEventName =
        function
        | Created _ -> "Customer Created"
        | Updated _ -> "Customer Updated"
        | Subscribed _ -> "Customer Subscribed"
        | Unsubscribed _ -> "Customer Unsubscribed"

    Db.saveEvent connectionString encodeCustomerEvent "Customer" getEventName DataError event
    |> Async.map (
        Result.bind (fun _ -> 
            Publisher.publishCustomerEvent event
            |> Result.mapError PublishingError)
    )
