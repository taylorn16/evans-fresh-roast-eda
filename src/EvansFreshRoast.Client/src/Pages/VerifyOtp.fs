module Pages.VerifyOtp

open AsyncHelpers
open Elmish
open Fable.React
open Fable.React.Props
open Fable.Core.JsInterop

type State =
    { OneTimePassword: string
      LoginRequest: Deferred<Result<unit, string>> }

type GlobalMsg =
    | Noop
    | LoggedIn

type Msg =
    | LoginRequest of AsyncOperationEvt<Result<unit, string>>
    | OtpUpdated of string

let init() =
    { OneTimePassword = ""
      LoginRequest = NotStarted },
    Cmd.none

let update (loginToken: string option) msg state =
    match msg with
    | OtpUpdated otp ->
        { state with OneTimePassword = otp }, Cmd.none, Noop

    | LoginRequest Started ->
        match loginToken with
        | None ->
            state, Cmd.none, Noop

        | Some token ->
            let cmd =
                async {
                    match! Api.login token state.OneTimePassword with
                    | Ok () ->
                        return Finished <| Ok ()

                    | Error e ->
                        return Finished <| Error e
                }
                |> Cmd.OfAsync.result
                |> Cmd.map LoginRequest

            let state = { state with LoginRequest = InProgress }
            
            state, cmd, Noop

    | LoginRequest (Finished (Ok ())) ->
        let state = { state with LoginRequest = Resolved <| Ok () }

        state, Cmd.none, LoggedIn

    | LoginRequest (Finished (Error e)) ->
        printfn "%s" e
        let state = { state with LoginRequest = Resolved <| Error e }

        state, Cmd.none, Noop

let pouroverImgUrl = importDefault "../pourover.jpg"

let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        img [ Src pouroverImgUrl; Class "img-fluid" ]
        h1 [ Class "text-center" ] [ str "Enter Security Code" ]
        div [] [
            div [ Class "mb-3" ] [
                label [ Class "form-label" ] [ str "Security Code" ]
                input [
                    Type "text"
                    Class "form-control form-control-lg"
                    Placeholder "e.g., 000000000"
                    Value state.OneTimePassword
                    OnInput(fun ev -> dispatch <| OtpUpdated ev.Value)
                ]
            ]
            div [ Class "d-flex justify-content-center" ] [
                button [
                    Class "btn btn-primary btn-lg"
                    OnClick(fun ev ->
                        ev.preventDefault()
                        dispatch <| LoginRequest Started)
                ] [ str "Log In" ]
            ]
        ]
    ]
