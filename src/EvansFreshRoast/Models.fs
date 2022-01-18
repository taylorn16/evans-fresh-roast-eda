namespace EvansFreshRoast.Models

open NodaTime

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
