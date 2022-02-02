module EvansFreshRoast.EventStore.Customer

open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.EventStore
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open RabbitMQ.Client

let loadCustomerEvents connectionString =
    Db.loadEvents connectionString decodeCustomerEvent DatabaseError SerializationError

let saveCustomerEvent connectionString (connectionFactory: IConnectionFactory) (event: DomainEvent<Customer, Event>) =
    let getEventName =
        function
        | Created _ -> "Customer Created"
        | Updated _ -> "Customer Updated"
        | Subscribed _ -> "Customer Subscribed"
        | Unsubscribed _ -> "Customer Unsubscribed"

    Db.saveEvent connectionString encodeCustomerEvent "Customer" getEventName DatabaseError event
    |> Async.map (
        Result.bind (fun _ -> 
            Publisher.publishCustomerEvent connectionFactory event
            |> Result.mapError PublishingError)
    )
