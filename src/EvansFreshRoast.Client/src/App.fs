module EvansFreshRoast.App

open Elmish
open Elmish.React
open Fable.React
open Fable.React.Props
open Fable.React.Standard
open Fable.React.Helpers
open Fable.Core.JsInterop

importSideEffects "./styles.scss"

let attr name value = HTMLAttr.Custom(name, value)

type Deferred<'a> =
    | NotStarted
    | InProgress
    | Resolved of 'a

module Deferred =
    let isInProgress =
        function
        | InProgress -> true
        | _ -> false

type AsyncOperationMsg<'a> =
    | Started
    | Finished of 'a

type State =
    { IsMainNavOpen: bool
      LoginCodeRequest: Deferred<Result<string, exn>> }

type MainNavMsg =
    | Opened
    | Closed

type Msg =
    | MainNav of MainNavMsg
    | LoginCodeRequest of AsyncOperationMsg<Result<string, exn>>

let init () =
    { IsMainNavOpen = false
      LoginCodeRequest = NotStarted }, Cmd.none

let update (msg: Msg) (state: State) =
    match msg with
    | MainNav Opened ->
        { state with IsMainNavOpen = true }, Cmd.none
    
    | MainNav Closed ->
        { state with IsMainNavOpen = false }, Cmd.none

    | LoginCodeRequest Started ->
        let getLoginCode = async {
            do! Async.Sleep 1000
            return LoginCodeRequest(Finished(Ok "code"))
        }

        { state with LoginCodeRequest = InProgress }, Cmd.OfAsync.result getLoginCode

    | LoginCodeRequest (Finished resp) ->
        { state with LoginCodeRequest = Resolved resp }, Cmd.none

// let mainNav (state: State) (dispatch: Msg -> unit) =
//     Bulma.navbar [
//         navbar.isFixedTop
//         prop.children [
//             Bulma.navbarBrand.div [
//                 Bulma.navbarBrand.a [
//                     prop.href "/"
//                     prop.children [
//                         Html.text "Evan's Fresh Roast"
//                     ]
//                 ]
//                 Bulma.navbarBurger [
//                     if state.IsMainNavOpen then
//                         navbarBurger.isActive
//                     prop.onClick (fun _ ->
//                         match state.IsMainNavOpen with
//                         | true -> dispatch <| MainNav Closed
//                         | false -> dispatch <| MainNav Opened)
//                     prop.children [
//                         for _ in 1..3 do
//                             Html.span [ prop.ariaHidden true ]
//                     ]
//                 ]
//             ]
//             Bulma.navbarMenu [
//                 if state.IsMainNavOpen then
//                     navbarMenu.isActive
//                 prop.children [
//                     Bulma.navbarStart.div [
//                         Bulma.navbarItem.a [
//                             prop.text "Roasts"
//                         ]
//                         Bulma.navbarItem.a [
//                             prop.text "Coffees"
//                         ]
//                         Bulma.navbarItem.a [
//                             prop.text "Customers"
//                         ]
//                     ]
//                 ]
//             ]
//         ]
//     ]

// let loginForm (state: State) (dispatch: Msg -> unit) =
//     Bulma.box [
//         spacing.mt4
//         prop.children [
//             Html.h2 [
//                 prop.classes [ "is-size-3"; "mb-2" ]
//                 prop.text "Log In"
//             ]
//             Html.form [
//                 Bulma.field.div [
//                     Bulma.label "Phone Number"
//                     Bulma.control.div [
//                         Bulma.input.tel [ prop.placeholder "111-222-3333" ]
//                     ]
//                 ]
//                 Bulma.field.div [
//                     Bulma.control.div [
//                         helpers.isFlex
//                         helpers.isFlexDirectionRowReverse
//                         prop.children [
//                             Bulma.button.button [
//                                 color.isPrimary
//                                 prop.onClick(fun ev ->
//                                     ev.preventDefault()
//                                     dispatch <| LoginCodeRequest Started)
//                                 if Deferred.isInProgress state.LoginCodeRequest then
//                                     button.isLoading
//                                 prop.text "Continue"
//                             ]
//                         ]
//                     ]
//                 ]
//             ]
//         ]
//     ]

let view (state: State) (dispatch: Msg -> unit) =
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
            a [
                Href "#"
                Class "link-secondary"
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
                    str "Evan's Fresh Roast"
                ]
                button [
                    Class "btn-close text-reset"
                    attr "data-bs-dismiss" "offcanvas"
                ] [ ]
            ]
            div [ Class "offcanvas-body" ] [
                ul [ Class "nav flex-column fs-4 nav-pills" ] [
                    li [ Class "nav-item" ] [
                        a [ Class "nav-link text-body"; Href "#/" ] [ str "Roasts" ]
                    ]
                    li [ Class "nav-item" ] [
                        a [ Class "nav-link text-body"; Href "#/" ] [ str "Customers" ]
                    ]
                    li [ Class "nav-item" ] [
                        a [ Class "nav-link text-body"; Href "#/" ] [ str "Coffees" ]
                    ]
                ]
            ]
        ]
    ]
    

Program.mkProgram init update view
|> Program.withReactBatched "app-root"
|> Program.run
