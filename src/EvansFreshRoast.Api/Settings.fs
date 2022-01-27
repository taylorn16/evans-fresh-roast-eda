namespace EvansFreshRoast.Api

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
type ConnectionStrings =
    { EventStore: string
      ReadStore: string }

[<CLIMutable>]
type Settings =
    { ConnectionStrings: ConnectionStrings
      RabbitMq: RabbitMq
      Twilio: Twilio }
