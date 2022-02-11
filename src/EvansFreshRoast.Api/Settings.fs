namespace EvansFreshRoast.Api
open EvansFreshRoast.Auth

[<CLIMutable>]
type Twilio =
    { AccountSid: string
      AuthToken: string
      FromPhoneNumber: string }

[<CLIMutable>]
type RabbitMq =
    { Hostname: string
      Port: int
      Username: string
      Password: string }

[<CLIMutable>]
type ConnectionStringParams =
    { Host: string
      Port: int
      Username: string
      Password: string
      Database: string }

[<CLIMutable>]
type ConnectionStrings =
    { EventStore: ConnectionStringParams
      ReadStore: ConnectionStringParams }

[<CLIMutable>]
type Settings =
    { ConnectionStrings: ConnectionStrings
      RabbitMq: RabbitMq
      Twilio: Twilio
      Jwt: JwtConfig }
