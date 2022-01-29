namespace EvansFreshRoast.Api.Models

open NodaTime
open System

[<CLIMutable>]
type Message = { Text: string }

[<CLIMutable>]
type CreateRoastDto =
    { RoastDate: LocalDate
      OrderByDate: LocalDate }

[<CLIMutable>]
type CreateCoffeeDto =
    { Name: string
      Description: string
      PricePerBag: decimal
      WeightPerBag: decimal }

[<CLIMutable>]
type CreateCustomerDto =
    { Name: string
      PhoneNumber: string }

[<CLIMutable>]
type CustomerDto =
    { Id: Guid
      Name: string
      PhoneNumber: string }

[<CLIMutable>]
type CoffeeDto =
    { Id: Guid
      Name: string
      Description: string
      PricePerBag: decimal
      WeightPerBag: decimal }

[<CLIMutable>]
type RoastSummaryDto =
    { Id: Guid
      Name: string
      RoastDate: string
      OrderByDate: string
      CustomersCount: int
      Coffees: list<Guid * string>
      RoastStatus: string
      OrdersCount: int }

[<CLIMutable>]
type RoastDetailedOrderInvoiceDto =
    { Amount: decimal
      PaymentMethod: string option }

[<CLIMutable>]
type RoastDetailedOrderDto =
    { CustomerId: Guid
      Timestamp: string
      LineItems: list<Guid * int>
      Invoice: RoastDetailedOrderDto option }

[<CLIMutable>]
type RoastDetailedDto =
    { Id: Guid
      Name: string
      RoastDate: string
      OrderByDate: string
      Customers: Guid list
      Coffees: Guid list
      Orders: RoastDetailedOrderDto list
      Status: string
      SentRemindersCount: int }
