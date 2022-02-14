namespace EvansFreshRoast.Dto

open System

[<CLIMutable>]
type CreateRoastRequest =
    { RoastDate: string
      OrderByDate: string }

[<CLIMutable>]
type RoastSummaryCoffee =
    { Id: Guid
      Name: string }

[<CLIMutable>]
type RoastSummary =
    { Id: Guid
      Name: string
      RoastDate: string
      OrderByDate: string
      CustomersCount: int
      Coffees: RoastSummaryCoffee list
      RoastStatus: string
      OrdersCount: int }

[<CLIMutable>]
type RoastDetailsOrderInvoice =
  { Amount: decimal
    PaymentMethod: string option }

[<CLIMutable>]
type RoastDetailsOrder =
    { CustomerId: Guid
      Timestamp: string
      LineItems: Map<Guid, int>
      Invoice: RoastDetailsOrderInvoice option } 

[<CLIMutable>]
type RoastDetails =
    { Id: Guid
      Name: string
      RoastDate: string
      OrderByDate: string
      Customers: Guid list
      Coffees: Guid list
      Orders: RoastDetailsOrder list
      Status: string
      SentRemindersCount: int }
