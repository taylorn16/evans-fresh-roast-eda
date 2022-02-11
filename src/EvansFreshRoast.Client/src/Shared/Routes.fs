module Routes

open Feliz.Router

type Route =
    | Login
    | VerifyOtp
    | NotFound
    | Roasts
    static member fromSegments segments =
        match segments with
        | [] -> Login
        | [ "login" ] -> Login
        | [ "verifyotp" ] -> VerifyOtp
        | [ "roasts" ] -> Roasts
        | _ -> NotFound

    static member toNavigateCmd route: Elmish.Cmd<'a> =
        match route with
        | Login -> Cmd.navigate(Router.formatPath [ "login" ])
        | VerifyOtp -> Cmd.navigate(Router.formatPath [ "verifyotp" ])
        | NotFound -> Cmd.navigate(Router.formatPath [ "notfound" ])
        | Roasts -> Cmd.navigate(Router.formatPath [ "roasts" ])
