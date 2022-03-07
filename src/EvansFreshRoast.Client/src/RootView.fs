module RootView

open EvansFreshRoast.Dto
open Fable.React
open Fable.React.Props
open Routes
open State

let attr name value = HTMLAttr.Custom(name, value)

let header session (dispatch: Msg -> unit) =
    let headerText =
        session
        |> Option.map (fun s -> s.Username)
        |> Option.map (sprintf "Welcome, %s!")
        |> Option.defaultValue "Evan's Fresh Roast"
    
    fragment [] [
        header [ Id "header"; Class "border-bottom bg-white" ] [
            button [
                Class "btn btn-outline-dark border-0"
                attr "data-bs-target" "#main-nav"
                attr "data-bs-toggle" "offcanvas"
            ] [
                i [ Class "bi-list fs-1" ] []
            ]
            h1 [
                Class "fw-light fw-italic fs-5"
            ] [
                str "Evan's Fresh Roast"
            ]
            button [
                Class "btn btn-link link-secondary me-1"
                OnClick(fun _ -> dispatch SignedOut)
            ] [
                str "Sign Out"
            ]
        ]
        aside [
            Id "main-nav"
            Class "offcanvas offcanvas-start"
            TabIndex -1
        ] [
            div [ Class "offcanvas-header" ] [
                h5 [ Class "offcanvas-title" ] [
                    str headerText
                ]
                button [
                    Class "btn-close text-reset"
                    attr "data-bs-dismiss" "offcanvas"
                ] [ ]
            ]
            div [ Class "offcanvas-body" ] [
                ul [ Class "nav flex-column fs-4 nav-pills" ] [
                    li [ Class "nav-item" ] [
                        button [
                            Class "nav-link text-body"
                            attr "data-bs-dismiss" "offcanvas"
                            OnClick(fun _ -> dispatch <| MainNavItemSelected Route.Roasts)
                        ] [
                            str "Roasts"
                        ]
                    ]
                    li [ Class "nav-item" ] [
                        button [
                            Class "nav-link text-body"
                            attr "data-bs-dismiss" "offcanvas"
                            OnClick(fun _ -> dispatch <| MainNavItemSelected Route.Customers)
                        ] [
                            str "Customers"
                        ]
                    ]
                    li [ Class "nav-item" ] [
                        button [
                            Class "nav-link text-body"
                            attr "data-bs-dismiss" "offcanvas"
                            OnClick(fun _ -> dispatch <| MainNavItemSelected Route.Coffees)
                        ] [
                            str "Coffees"
                        ]
                    ]
                ]
            ]
        ]
    ]

let view (state: State) (dispatch: Msg -> unit) =
    let currentView =
        match state.CurrentPage with
        | Login st ->
            Pages.Login.view st (LoginMsg >> dispatch)

        | VerifyOtp st ->
            Pages.VerifyOtp.view st (VerifyOtpMsg >> dispatch)

        | Roasts st ->
            Pages.Roasts.view st (RoastsMsg >> dispatch)

        | Roast st ->
            Pages.Roast.view st (RoastMsg >> dispatch)
        
        | NewRoast st ->
            Pages.NewRoast.view st (NewRoastMsg >> dispatch)
        
        | NewCoffee st ->
            Pages.NewCoffee.view st (NewCoffeeMsg >> dispatch)

        | Coffee st ->
            Pages.Coffee.view st (CoffeeMsg >> dispatch)
        
        | Coffees st ->
            Pages.Coffees.view st (CoffeesMsg >> dispatch)
        
        | NewCustomer st ->
            Pages.NewCustomer.view st (NewCustomerMsg >> dispatch)
            
        | Customers st ->
            Pages.Customers.view st (CustomersMsg >> dispatch)
        
        | NotFound ->
            div [] [ str "Not found" ]
    
    fragment [] [
        header state.Session dispatch
        section [ Id "main-content"; Class "mx-3" ] [
            currentView
        ]
    ]