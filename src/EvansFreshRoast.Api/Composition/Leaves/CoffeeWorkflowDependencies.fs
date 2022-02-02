namespace EvansFreshRoast.Api.Composition.Leaves

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Coffee
open EvansFreshRoast.EventStore
open EvansFreshRoast.EventStore.Coffee
open EvansFreshRoast.ReadModels.CoffeeRepository

type CoffeeWorkflowDependencies =
    { LoadEvents: LoadEvents<Coffee, Event, EventStoreError>
      SaveEvent: SaveEvent<Coffee, Event, EventStoreError>
      GetCoffee: LoadAggregate<Coffee>
      GetAllCoffees: LoadAllAggregates<Coffee> }

module Coffees =
    let compose rabbitMqConnectionFactory eventStoreConnectionString readStoreConnectionString =
        { LoadEvents = loadCoffeeEvents eventStoreConnectionString
          SaveEvent = saveCoffeeEvent eventStoreConnectionString rabbitMqConnectionFactory
          GetCoffee = getCoffee readStoreConnectionString
          GetAllCoffees = fun () -> getAllCoffees readStoreConnectionString }
