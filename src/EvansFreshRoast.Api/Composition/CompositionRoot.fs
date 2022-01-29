namespace EvansFreshRoast.Api.Composition

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.EventStore
open EvansFreshRoast.Api
open EvansFreshRoast.Utils
open NodaTime
open RabbitMQ.Client
open EvansFreshRoast.ReadModels
open EvansFreshRoast.Utils

type CompositionRoot =
    { LoadCustomerEvents: LoadEvents<Customer, Customer.Event, EventStoreError>
      SaveCustomerEvent: SaveEvent<Customer, Customer.Event, EventStoreError>
      GetCustomer: LoadAggregate<Customer>
      GetCustomerByPhoneNumber: UsPhoneNumber -> Async<option<Id<Customer> * Customer>>
      GetAllCustomers: LoadAllAggregates<Customer>
      LoadCoffeeEvents: LoadEvents<Coffee, Coffee.Event, EventStoreError>
      SaveCoffeeEvent: SaveEvent<Coffee, Coffee.Event, EventStoreError>
      GetCoffee: LoadAggregate<Coffee>
      GetAllCoffees: LoadAllAggregates<Coffee>
      LoadRoastEvents: LoadEvents<Roast, Roast.Event, EventStoreError>
      SaveRoastEvent: SaveEvent<Roast, Roast.Event, EventStoreError>
      GetRoast: Id<Roast> -> Async<option<RoastDetailedView>>
      GetAllRoasts: unit -> Async<list<RoastSummaryView>>
      GetToday: unit -> LocalDate
      GetNow: unit -> OffsetDateTime
      RabbitMqConnectionFactory: IConnectionFactory
      TwilioFromPhoneNumber: UsPhoneNumber
      ReadStoreConnectionString: ConnectionString }
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

    member this.GetRoastCommandHandler() = async {
        let! allRoasts =
            this.GetAllRoasts()
            |> Async.map (List.map (fun rv -> rv.Id, rv.RoastStatus))
        let! allCustomers = this.GetAllCustomers()
        let! allCoffees = this.GetAllCoffees()
        let today = this.GetToday()
        
        return
            Aggregate.createHandler
                (Roast.createAggregate allRoasts allCustomers allCoffees today)
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
          GetCustomerByPhoneNumber = customersWorkflow.GetCustomerByPhoneNumber
          LoadCoffeeEvents = coffeesWorkflow.LoadEvents
          SaveCoffeeEvent = coffeesWorkflow.SaveEvent
          GetCoffee = coffeesWorkflow.GetCoffee
          GetAllCoffees = coffeesWorkflow.GetAllCoffees
          LoadRoastEvents = roastsWorkflow.LoadEvents
          SaveRoastEvent = roastsWorkflow.SaveEvent
          GetRoast = roastsWorkflow.GetRoast
          GetAllRoasts = fun _ -> roastsWorkflow.GetAllRoasts coffeesWorkflow.GetAllCoffees
          GetToday = fun _ -> LocalDate.FromDateTime(System.DateTime.Today)
          GetNow = fun _ -> OffsetDateTime.FromDateTimeOffset(System.DateTimeOffset.Now)
          RabbitMqConnectionFactory = rabbitMqConnectionFactory
          TwilioFromPhoneNumber = UsPhoneNumber.create settings.Twilio.FromPhoneNumber |> unsafeAssertOk
          ReadStoreConnectionString = readStoreConnectionString }
