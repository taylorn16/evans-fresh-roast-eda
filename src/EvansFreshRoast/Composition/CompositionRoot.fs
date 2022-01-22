namespace EvansFreshRoast

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.EventStore

type CompositionRoot =
    { LoadCustomerEvents: Id<Customer> -> Async<Result<DomainEvent<Customer, Customer.Event> list, Customer.CustomerEventStoreError>>
      SaveCustomerEvent: DomainEvent<Customer, Customer.Event> -> Async<Result<unit, Customer.CustomerEventStoreError>>
      GetCustomer: Id<Customer> -> Async<option<Id<Customer> * Customer>>
      GetAllCustomers: Async<list<Id<Customer> * Customer>> }

module CompositionRoot =
    let compose (settings: Settings) =
        let eventStoreConnectionString =
            ConnectionString.create settings.ConnectionStrings.EventStore
        
        let readStoreConnectionString =
            ConnectionString.create settings.ConnectionStrings.ReadStore

        let customersWorkflow =
            Composition.Leaves.Customers.compose
                eventStoreConnectionString
                readStoreConnectionString
        
        { LoadCustomerEvents = customersWorkflow.LoadEvents
          SaveCustomerEvent = customersWorkflow.SaveEvent
          GetCustomer = customersWorkflow.GetCustomer
          GetAllCustomers = customersWorkflow.GetAllCustomers }
