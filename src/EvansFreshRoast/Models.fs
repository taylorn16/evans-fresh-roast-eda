namespace EvansFreshRoast.Models

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
