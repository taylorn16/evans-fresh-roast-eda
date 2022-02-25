namespace EvansFreshRoast.Dto

[<CLIMutable>]
type CreateCoffeeRequest =
    { Name: string
      Description: string
      PricePerBag: decimal
      WeightPerBag: decimal }

[<CLIMutable>]
type Coffee =
    { Id: System.Guid
      Name: string
      Description: string
      PricePerBag: decimal
      WeightPerBag: decimal
      IsActive: bool }
