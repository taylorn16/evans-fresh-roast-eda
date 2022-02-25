namespace EvansFreshRoast.EventStore

open System

type DomainEvent =
    { Id: Guid
      AggregateId: Guid
      AggregateName: string
      Version: int64
      EventName: string
      Payload: string
      Timestamp: DateTimeOffset }

type EventStoreDbError =
    | ErrorSavingEvent of DomainEvent * exn
    | ErrorLoadingEvents of Guid * exn

type EventStoreError =
    | DatabaseError of EventStoreDbError
    | SerializationError of string
    | PublishingError of exn
