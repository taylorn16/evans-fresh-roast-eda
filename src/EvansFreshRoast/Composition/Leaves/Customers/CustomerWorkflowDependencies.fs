namespace EvansFreshRoast.Composition.Leaves

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.EventStore.Customer
open EvansFreshRoast.ReadModels.CustomerRepository

type CustomerWorkflowDependencies =
    { LoadEvents: Id<Customer> -> Async<Result<DomainEvent<Customer, Event> list, CustomerEventStoreError>>
      SaveEvent: DomainEvent<Customer, Event> -> Async<Result<unit, CustomerEventStoreError>>
      GetCustomer: Id<Customer> -> Async<option<Id<Customer> * Customer>>
      GetAllCustomers: Async<list<Id<Customer> * Customer>> }

module Customers =
    let compose eventStoreConnectionString readStoreConnectionString =
        { LoadEvents = loadCustomerEvents eventStoreConnectionString
          SaveEvent = saveCustomerEvent eventStoreConnectionString
          GetCustomer = getCustomer readStoreConnectionString
          GetAllCustomers = getAllCustomers readStoreConnectionString }
