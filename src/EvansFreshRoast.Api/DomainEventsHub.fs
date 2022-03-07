namespace EvansFreshRoast.Api

open Microsoft.AspNetCore.SignalR

type DomainEventsHub () =
    inherit Hub()

    member __.PushMessageToClients(msg: string) =
        task {
            do! __.Clients.All.SendAsync("MessageSent", msg)
        }
