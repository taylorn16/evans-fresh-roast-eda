namespace EvansFreshRoast.Composition

open EvansFreshRoast
open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.EventStore

type CompositionRoot =
    { LoadCustomerEvents: LoadEvents<Customer, Customer.Event, Customer.EventStoreError>
      SaveCustomerEvent: SaveEvent<Customer, Customer.Event, Customer.EventStoreError>
      GetCustomer: LoadAggregate<Customer>
      GetAllCustomers: LoadAllAggregates<Customer>
      LoadCoffeeEvents: LoadEvents<Coffee, Coffee.Event, Coffee.EventStoreError>
      SaveCoffeeEvent: SaveEvent<Coffee, Coffee.Event, Coffee.EventStoreError>
      GetCoffee: LoadAggregate<Coffee>
      GetAllCoffees: LoadAllAggregates<Coffee> }

module CompositionRoot =
    let compose (settings: Settings) =
        let eventStoreConnectionString =
            ConnectionString.create settings.ConnectionStrings.EventStore
        
        let readStoreConnectionString =
            ConnectionString.create settings.ConnectionStrings.ReadStore

        let customersWorkflow =
            Leaves.Customers.compose
                eventStoreConnectionString
                readStoreConnectionString

        let coffeesWorkflow =
            Leaves.Coffees.compose
                eventStoreConnectionString
                readStoreConnectionString
        
        { LoadCustomerEvents = customersWorkflow.LoadEvents
          SaveCustomerEvent = customersWorkflow.SaveEvent
          GetCustomer = customersWorkflow.GetCustomer
          GetAllCustomers = fun () -> customersWorkflow.GetAllCustomers
          LoadCoffeeEvents = coffeesWorkflow.LoadEvents
          SaveCoffeeEvent = coffeesWorkflow.SaveEvent
          GetCoffee = coffeesWorkflow.GetCoffee
          GetAllCoffees = fun () -> coffeesWorkflow.GetAllCoffees }
