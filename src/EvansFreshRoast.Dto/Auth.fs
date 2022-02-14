namespace EvansFreshRoast.Dto

[<CLIMutable>]
type GetAuthCodeRequest =
    { PhoneNumber: string }

[<CLIMutable>]
type LoginRequest =
    { LoginCode: string
      LoginToken: System.Guid }
