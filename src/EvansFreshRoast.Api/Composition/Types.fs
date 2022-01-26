namespace EvansFreshRoast.Api.Composition

open EvansFreshRoast.Framework

type LoadEvents<'Aggregate, 'Event, 'Error> =
    Id<'Aggregate> -> Async<Result<DomainEvent<'Aggregate, 'Event> list, 'Error>>

type SaveEvent<'Aggregate, 'Event, 'Error> =
    DomainEvent<'Aggregate, 'Event> -> Async<Result<unit, 'Error>>

type LoadAggregate<'Aggregate> =
    Id<'Aggregate> -> Async<option<Id<'Aggregate> * 'Aggregate>>
    
type LoadAllAggregates<'Aggregate> =
    unit -> Async<list<Id<'Aggregate> * 'Aggregate>>