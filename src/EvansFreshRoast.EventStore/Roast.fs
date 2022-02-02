module EvansFreshRoast.EventStore.Roast

open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.EventStore
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils
open RabbitMQ.Client

let loadRoastEvents connectionString =
    Db.loadEvents connectionString decodeRoastEvent DatabaseError SerializationError

let saveRoastEvent connectionString (connectionFactory: IConnectionFactory) (event: DomainEvent<Roast, Event>) =
    let getEventName =
        function
        | Created _ -> "Roast Created"
        | OrderPlaced _ -> "Order Placed"
        | OrderCancelled _ -> "Order Cancelled"
        | OrderConfirmed _ -> "Order Confirmed"
        | CoffeesAdded _ -> "Coffees Added"
        | CustomersAdded _ -> "Customers Added"
        | RoastDatesChanged _ -> "Roast Dates Changed"
        | RoastStarted _ -> "Roast Started"
        | RoastCompleted -> "Roast Completed"
        | CoffeesRemoved _ -> "Coffees Removed"
        | CustomersRemoved _ -> "Customers Removed"
        | InvoicePaid _ -> "Invoice Paid"
        | ReminderSent -> "Reminder Sent"

    Db.saveEvent connectionString encodeRoastEvent "Roast" getEventName DatabaseError event
    |> Async.map (
        Result.bind (fun () ->
            Publisher.publishRoastEvent connectionFactory event
            |> Result.mapError PublishingError)
    )
