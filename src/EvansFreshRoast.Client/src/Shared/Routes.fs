module Routes

open Elmish.UrlParser
open Elmish.Navigation

type Route =
    | Login
    | VerifyOtp
    | Roasts
    static member parse: Parser<Route -> Route, Route>  =
        oneOf
            [ map Login (s "login")
              map VerifyOtp (s "verifyotp")
              map Roasts (s "roasts")
              map Roasts top ]

    static member toHash route =
        match route with
        | Login -> "login"
        | VerifyOtp -> "verifyotp"
        | Roasts -> "roasts"
        |> sprintf "#/%s"

    static member navigateTo route =
        Route.toHash route
        |> Navigation.newUrl
