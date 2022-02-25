module Routes

open System
open Elmish.UrlParser
open Elmish.Navigation

type Route =
    | Login
    | VerifyOtp
    | Roasts
    | NewCoffee
    | Coffee of Guid
    | Coffees
    | NewCustomer
    | Customers
    static member parse: Parser<Route -> Route, Route>  =
        let guid =
            custom "GUID" <| fun segment ->
                match Guid.TryParse(segment) with
                | true, g -> Ok g
                | _ -> Error "Not a Guid."
        
        oneOf
            [ map Login (s "login")
              map VerifyOtp (s "verifyotp")
              map Roasts (s "roasts")
              map NewCoffee (s "coffees" </> s "new")
              map Coffee (s "coffees" </> guid)
              map Coffees (s "coffees")
              map NewCustomer (s "customers" </> s "new")
              map Customers (s "customers")
              map Roasts top ]

    static member toHash route =
        match route with
        | Login -> "login"
        | VerifyOtp -> "verifyotp"
        | Roasts -> "roasts"
        | NewCoffee -> "coffees/new"
        | Coffee id -> $"coffees/{id}"
        | Coffees -> "coffees"
        | NewCustomer -> "customers/new"
        | Customers -> $"customers"
        |> sprintf "#/%s"

    static member navigateTo route =
        Route.toHash route
        |> Navigation.newUrl
