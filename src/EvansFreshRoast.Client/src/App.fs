﻿module App

open Elmish
open Elmish.Navigation
open Elmish.React
open Fable.React
open Fable.React.Props
open Fable.Core.JsInterop
open Routes
open Types
open Thoth.Json

importSideEffects "./styles.scss"

let attr name value = HTMLAttr.Custom(name, value)

type Page =
    | NotFound
    | Login of Pages.Login.State
    | VerifyOtp of Pages.VerifyOtp.State
    | Roasts of Pages.Roasts.State

type State =
    { Session: Session option
      OtpToken: OtpToken option
      CurrentPage: Page }

type Msg =
    | Noop
    | LoginMsg of Pages.Login.Msg
    | VerifyOtpMsg of Pages.VerifyOtp.Msg
    | RoastsMsg of Pages.Roasts.Msg

let setCurrentPage maybeRoute state =
    match state.Session, maybeRoute with
        | None, None ->
            state, Route.navigateTo Route.Login

        | _, Some Route.Login ->
            let loginState, loginCmd = Pages.Login.init()

            { state with CurrentPage = Login loginState},
            loginCmd |> Cmd.map LoginMsg
        
        | _, Some Route.VerifyOtp ->
            match state.OtpToken with
            | Some _ ->
                let verifyOtpState, verifyOtpCmd = Pages.VerifyOtp.init()

                { state with CurrentPage = VerifyOtp verifyOtpState },
                verifyOtpCmd |> Cmd.map VerifyOtpMsg

            | None ->
                state, Route.navigateTo Route.Login

        | None, _ ->
            state, Route.navigateTo Route.Login

        | Some _, Some Route.Roasts ->
            let roastsState, roastsCmd = Pages.Roasts.init()

            { state with CurrentPage = Roasts roastsState },
            roastsCmd |> Cmd.map RoastsMsg

        | _ ->
            { state with CurrentPage = NotFound }, Cmd.none

let init maybeSession maybeRoute =
    { Session = maybeSession
      OtpToken = None
      CurrentPage = NotFound }
    |> setCurrentPage maybeRoute

let update (msg: Msg) (state: State) =
    match msg with
    | Noop ->
        state, Cmd.none

    | LoginMsg loginMsg ->
        match state.CurrentPage with
        | Login loginState ->
            let newLoginState, loginCmd, globalMsg = Pages.Login.update loginMsg loginState

            let otpToken, routeCmd =
                match globalMsg with
                | Pages.Login.Noop ->
                    None, Cmd.none

                | Pages.Login.LoginTokenReceived token ->
                    Some token, Route.navigateTo Route.VerifyOtp

            { state with
                CurrentPage = Login newLoginState
                OtpToken = otpToken },
            Cmd.batch
                [ loginCmd |> Cmd.map LoginMsg 
                  routeCmd ]

        | _ -> state, Cmd.none

    | VerifyOtpMsg verifyOtpMsg ->
        match state.CurrentPage with
        | VerifyOtp verifyOtpState ->
            let newVerifyOtpState, verifyOtpCmd, globalMsg =
                Pages.VerifyOtp.update state.OtpToken verifyOtpMsg verifyOtpState

            let session, routeCmd =
                match globalMsg with
                | Pages.VerifyOtp.Noop ->
                    None, Cmd.none

                | Pages.VerifyOtp.LoggedIn session ->
                    let saveSessionCmd =
                        async {
                            Browser.WebStorage.localStorage.setItem(
                                "efr.session",
                                Encode.toString 2 (Session.encode session))

                            return Noop
                        }
                        |> Cmd.OfAsync.result

                    Some session,
                    Cmd.batch
                        [ Route.navigateTo Route.Roasts
                          saveSessionCmd ]

            { state with
                CurrentPage = VerifyOtp newVerifyOtpState
                Session = session
                OtpToken =
                    if Option.isSome session then
                        None
                    else
                        state.OtpToken },
            Cmd.batch
                [ verifyOtpCmd |> Cmd.map VerifyOtpMsg
                  routeCmd ]

        | _ -> state, Cmd.none

    | RoastsMsg roastsMsg ->
        match state.CurrentPage with
        | Roasts roastsState ->
            let newState, cmd = Pages.Roasts.update roastsMsg roastsState

            { state with CurrentPage = Roasts newState },
            Cmd.map RoastsMsg cmd

        | _ -> state, Cmd.none

let header =
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

let view (state: State) (dispatch: Msg -> unit) =
    let currentView =
        match state.CurrentPage with
        | Login loginState ->
            Pages.Login.view loginState (LoginMsg >> dispatch)

        | VerifyOtp verifyOtpState ->
            Pages.VerifyOtp.view verifyOtpState (VerifyOtpMsg >> dispatch)

        | Roasts roastsState ->
            Pages.Roasts.view roastsState (RoastsMsg >> dispatch)

        | NotFound ->
            div [] [ str "Not found" ]
    
    fragment [] [
        header
        section [ Id "main-content"; Class "mx-3" ] [
            currentView
        ]
    ]
    
let localStorageSession =
    Browser.WebStorage.localStorage.getItem "efr.session"
    |> Option.ofObj
    |> Option.bind (
        Decode.fromString Session.decoder
        >> function
            | Ok a -> Some a
            | Error _ -> None)

Program.mkProgram (init localStorageSession) update view
|> Program.toNavigable (UrlParser.parseHash Route.parse) setCurrentPage 
|> Program.withReactSynchronous "app-root"
|> Program.withConsoleTrace
|> Program.run
