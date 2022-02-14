namespace EvansFreshRoast.Dto

[<CLIMutable>]
type CreateCustomerRequest =
    { Name: string
      PhoneNumber: string }

[<CLIMutable>]
type Customer =
    { Id: System.Guid
      Name: string
      PhoneNumber: string }
