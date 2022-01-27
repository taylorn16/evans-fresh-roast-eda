namespace EvansFreshRoast.Api.Composition

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.EventStore
open EvansFreshRoast.Api
open NodaTime
open RabbitMQ.Client

type CompositionRoot =
    { LoadCustomerEvents: LoadEvents<Customer, Customer.Event, EventStoreError>
      SaveCustomerEvent: SaveEvent<Customer, Customer.Event, EventStoreError>
      GetCustomer: LoadAggregate<Customer>
      GetAllCustomers: LoadAllAggregates<Customer>
      LoadCoffeeEvents: LoadEvents<Coffee, Coffee.Event, EventStoreError>
      SaveCoffeeEvent: SaveEvent<Coffee, Coffee.Event, EventStoreError>
      GetCoffee: LoadAggregate<Coffee>
      GetAllCoffees: LoadAllAggregates<Coffee>
      LoadRoastEvents: LoadEvents<Roast, Roast.Event, EventStoreError>
      SaveRoastEvent: SaveEvent<Roast, Roast.Event, EventStoreError>
      GetRoast: LoadAggregate<Roast>
      GetAllRoasts: LoadAllAggregates<Roast>
      GetToday: unit -> LocalDate
      RabbitMqConnectionFactory: IConnectionFactory }
    member this.CustomerCommandHandler with get () =
        Aggregate.createHandler
            Customer.aggregate
            this.LoadCustomerEvents
            this.SaveCustomerEvent

    member this.CoffeeCommandHandler with get () =
        Aggregate.createHandler
            Coffee.aggregate
            this.LoadCoffeeEvents
            this.SaveCoffeeEvent

    member this.CreateRoastCommandHandler() = async {
        let! allCustomers = this.GetAllCustomers()
        let! allCoffees = this.GetAllCoffees()
        let today = this.GetToday()
        
        return
            Aggregate.createHandler
                (Roast.createAggregate allCustomers allCoffees today)
                this.LoadRoastEvents
                this.SaveRoastEvent 
    }

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
        
        let roastsWorkflow =
            Leaves.Roasts.compose
                eventStoreConnectionString
                readStoreConnectionString

        let rabbitMqConnectionFactory = ConnectionFactory(
            HostName = settings.RabbitMq.Hostname,
            UserName = settings.RabbitMq.Username,
            Password = settings.RabbitMq.Password,
            Port = settings.RabbitMq.Port,
            AutomaticRecoveryEnabled = true)

        { LoadCustomerEvents = customersWorkflow.LoadEvents
          SaveCustomerEvent = customersWorkflow.SaveEvent
          GetCustomer = customersWorkflow.GetCustomer
          GetAllCustomers = customersWorkflow.GetAllCustomers
          LoadCoffeeEvents = coffeesWorkflow.LoadEvents
          SaveCoffeeEvent = coffeesWorkflow.SaveEvent
          GetCoffee = coffeesWorkflow.GetCoffee
          GetAllCoffees = coffeesWorkflow.GetAllCoffees
          LoadRoastEvents = roastsWorkflow.LoadEvents
          SaveRoastEvent = roastsWorkflow.SaveEvent
          GetRoast = roastsWorkflow.GetRoast
          GetAllRoasts = roastsWorkflow.GetAllRoasts
          GetToday = fun _ -> LocalDate.FromDateTime(System.DateTime.Today)
          RabbitMqConnectionFactory = rabbitMqConnectionFactory }
