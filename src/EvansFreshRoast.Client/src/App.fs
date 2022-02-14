module App

open Elmish
open Elmish.Navigation
open Elmish.React
open Fable.React
open Fable.React.Props
open Fable.Core.JsInterop
open Routes
open Types
open Thoth.Json
open Fable.SignalR
open Fable.SignalR.Elmish

importSideEffects "./styles.scss"

let attr name value = HTMLAttr.Custom(name, value)

type Page =
    | NotFound
    | Login of Pages.Login.State
    | VerifyOtp of Pages.VerifyOtp.State
    | Roasts of Pages.Roasts.State
    | NewCoffee of Pages.NewCoffee.State

type State =
    { Session: Session option
      OtpToken: OtpToken option
      CurrentPage: Page
      Hub: Elmish.Hub<string, string> option }

type Msg =
    | Noop
    | SignalRHubRegistered of Elmish.Hub<string, string>
    | SignalRMessageReceived of string
    | LoginMsg of Pages.Login.Msg
    | VerifyOtpMsg of Pages.VerifyOtp.Msg
    | RoastsMsg of Pages.Roasts.Msg
    | NewCoffeeMsg of Pages.NewCoffee.Msg

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

        | Some _, Some Route.NewCoffee ->
            let st, cmd = Pages.NewCoffee.init()

            { state with CurrentPage = NewCoffee st },
            cmd |> Cmd.map NewCoffeeMsg

        | _ ->
            { state with CurrentPage = NotFound }, Cmd.none

let init maybeSession maybeRoute =
    let state, cmd =
        { Session = maybeSession
          OtpToken = None
          CurrentPage = NotFound
          Hub = None }
        |> setCurrentPage maybeRoute

    let signalRConnectCmd =
        maybeSession
        |> Option.map (fun _ ->
            Cmd.SignalR.connect SignalRHubRegistered (fun hub ->
                hub.withUrl("/api/ws")
                    .withAutomaticReconnect()
                    .configureLogging(LogLevel.Debug)
                    .onMessage SignalRMessageReceived))
        |> Option.defaultValue Cmd.none

    state, Cmd.batch [ cmd; signalRConnectCmd ]

let update (msg: Msg) (state: State) =
    match msg with
    | Noop ->
        state, Cmd.none

    | SignalRHubRegistered hub ->
        { state with Hub = Some hub }, Cmd.none

    | SignalRMessageReceived msg ->
        // TODO: do something here!
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

        | _ ->
            state, Cmd.none

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

    | NewCoffeeMsg msg ->
        match state.CurrentPage with
        | NewCoffee st ->
            let newSt, cmd = Pages.NewCoffee.update msg st

            { state with CurrentPage = NewCoffee newSt },
            Cmd.map NewCoffeeMsg cmd

        | _ -> state, Cmd.none

let header session =
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
        | Login st ->
            Pages.Login.view st (LoginMsg >> dispatch)

        | VerifyOtp st ->
            Pages.VerifyOtp.view st (VerifyOtpMsg >> dispatch)

        | Roasts st ->
            Pages.Roasts.view st (RoastsMsg >> dispatch)

        | NewCoffee st ->
            Pages.NewCoffee.view st (NewCoffeeMsg >> dispatch)

        | NotFound ->
            div [] [ str "Not found" ]
    
    fragment [] [
        header state.Session
        section [ Id "main-content"; Class "mx-3" ] [
            currentView
        ]
    ]
    
let getLocalStorageSession() =
    Browser.WebStorage.localStorage.getItem "efr.session"
    |> Option.ofObj
    |> Option.bind (
        Decode.fromString Session.decoder
        >> function
            | Ok a -> Some a
            | Error _ -> None)

Program.mkProgram (init <| getLocalStorageSession()) update view
|> Program.toNavigable (UrlParser.parseHash Route.parse) setCurrentPage 
|> Program.withReactSynchronous "app-root"
|> Program.withConsoleTrace
|> Program.run
