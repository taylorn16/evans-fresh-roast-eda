module EvansFreshRoast.App

open Feliz
open Feliz.Bulma
open Elmish
open Elmish.React

type State =
    { IsMainNavOpen: bool }

type MainNavMsg =
    | Opened
    | Closed

type Msg =
    | MainNav of MainNavMsg

let init () =
    { IsMainNavOpen = false }, Cmd.none

let update (msg: Msg) (state: State) =
    match msg with
    | MainNav Opened ->
        { state with IsMainNavOpen = true }, Cmd.none
    
    | MainNav Closed ->
        { state with IsMainNavOpen = false }, Cmd.none

let view (state: State) (dispatch: Msg -> unit) =
    Bulma.navbar [
        Bulma.navbarBrand.div [
            Bulma.navbarBrand.a [
                prop.href "/"
                prop.children [
                    Html.text "Evan's Fresh Roast"
                ]
            ]

            Bulma.navbarBurger [
                if state.IsMainNavOpen then
                    Bulma.navbarBurger.isActive
                prop.onClick (fun _ ->
                    match state.IsMainNavOpen with
                    | true -> dispatch <| MainNav Closed
                    | false -> dispatch <| MainNav Opened)
                prop.children [
                    for _ in 1..3 do
                        Html.span [ prop.ariaHidden true ]
                ]
            ]
        ]
        Bulma.navbarMenu [
            if state.IsMainNavOpen then
                Bulma.navbarMenu.isActive
            prop.children [
                Bulma.navbarStart.div [
                    Bulma.navbarItem.a [
                        prop.text "Roasts"
                    ]
                    Bulma.navbarItem.a [
                        prop.text "Coffees"
                    ]
                    Bulma.navbarItem.a [
                        prop.text "Customers"
                    ]
                ]
            ]
        ]
    ]
    
    // Bulma.block [
    //     Bulma.box [
    //         Html.form [
    //             Bulma.field.div [
    //                 Bulma.label "Phone Number"
    //                 Bulma.control.div [
    //                     Bulma.input.tel [
    //                         prop.placeholder "111-222-3333"
    //                     ]
    //                 ]
    //             ]
    //         ]
    //     ]
    // ]
    

Program.mkProgram init update view
|> Program.withReactBatched "app-root"
|> Program.run
