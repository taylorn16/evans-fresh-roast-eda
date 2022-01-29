namespace EvansFreshRoast.Api.Composition.Leaves

open EvansFreshRoast.Framework
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.EventStore
open EvansFreshRoast.EventStore.Roast
open EvansFreshRoast.ReadModels
open EvansFreshRoast.ReadModels.RoastRepository

type RoastWorkflowDependencies =
    { LoadEvents: LoadEvents<Roast, Event, EventStoreError>
      SaveEvent: SaveEvent<Roast, Event, EventStoreError>
      GetRoast: Id<Roast> -> Async<option<RoastDetailedView>>
      GetAllRoasts: LoadAllAggregates<Coffee> -> Async<list<RoastSummaryView>> }

module Roasts =
    let compose eventStoreConnectionString readStoreConnectionString =
        { LoadEvents = loadRoastEvents eventStoreConnectionString
          SaveEvent = saveRoastEvent eventStoreConnectionString
          GetRoast = getRoast readStoreConnectionString
          GetAllRoasts = getAllRoasts readStoreConnectionString }
