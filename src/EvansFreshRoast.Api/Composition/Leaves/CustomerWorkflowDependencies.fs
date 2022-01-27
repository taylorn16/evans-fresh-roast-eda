namespace EvansFreshRoast.Api.Composition.Leaves

open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.EventStore
open EvansFreshRoast.EventStore.Customer
open EvansFreshRoast.ReadModels.CustomerRepository

type CustomerWorkflowDependencies =
    { LoadEvents: LoadEvents<Customer, Event, EventStoreError>
      SaveEvent: SaveEvent<Customer, Event, EventStoreError>
      GetCustomer: LoadAggregate<Customer>
      GetAllCustomers: LoadAllAggregates<Customer> }

module Customers =
    let compose eventStoreConnectionString readStoreConnectionString =
        { LoadEvents = loadCustomerEvents eventStoreConnectionString
          SaveEvent = saveCustomerEvent eventStoreConnectionString
          GetCustomer = getCustomer readStoreConnectionString
          GetAllCustomers = fun () -> getAllCustomers readStoreConnectionString }
