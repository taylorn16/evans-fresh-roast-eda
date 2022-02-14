namespace EvansFreshRoast.Api

open Microsoft.AspNetCore.SignalR

type TestHub () =
    inherit Hub()

    member __.PushMessageToClients(msg: string) =
        task {
            do! __.Clients.All.SendAsync("MessageSent", msg)
        }
