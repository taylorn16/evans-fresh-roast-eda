namespace EvansFreshRoast.Api.Composition.Leaves

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.EventStore
open EvansFreshRoast.EventStore.Coffee
open EvansFreshRoast.ReadModels.CoffeeRepository

type CoffeeWorkflowDependencies =
    { LoadEvents: Id<Coffee> -> Async<Result<DomainEvent<Coffee, Event> list, EventStoreError>>
      SaveEvent: DomainEvent<Coffee, Event> -> Async<Result<unit, EventStoreError>>
      GetCoffee: Id<Coffee> -> Async<option<Id<Coffee> * Coffee>>
      GetAllCoffees: Async<list<Id<Coffee> * Coffee>> }

module Coffees =
    let compose eventStoreConnectionString readStoreConnectionString =
        { LoadEvents = loadCoffeeEvents eventStoreConnectionString
          SaveEvent = saveCoffeeEvent eventStoreConnectionString
          GetCoffee = getCoffee readStoreConnectionString
          GetAllCoffees = getAllCoffees readStoreConnectionString }
