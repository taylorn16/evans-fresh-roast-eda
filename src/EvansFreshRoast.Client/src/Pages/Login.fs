module Pages.Login

open AsyncHelpers
open Elmish
open Fable.React
open Fable.React.Props
open Fable.Core.JsInterop
open Types

type State =
    { PhoneNumber: string
      AuthCode: Deferred<Result<OtpToken, string>> }

type GlobalMsg =
    | Noop
    | LoginTokenReceived of OtpToken

type Msg =
    | AuthCodeRequest of AsyncOperationEvt<Result<OtpToken, string>>
    | PhoneNumberUpdated of string 

let init() =
    { PhoneNumber = ""
      AuthCode = NotStarted },
    Cmd.none

let update msg state =
    match msg with
    | AuthCodeRequest Started ->
        match state.AuthCode with
        | InProgress ->
            state, Cmd.none, Noop

        | _ ->
            let cmd =
                async {
                    match! Api.getAuthCode state.PhoneNumber with
                    | Ok token ->
                        return Finished <| Ok token

                    | Error e ->
                        return Finished <| Error e
                }
                |> Cmd.OfAsync.result
                |> Cmd.map AuthCodeRequest

            { state with AuthCode = InProgress }, cmd, Noop

    | AuthCodeRequest (Finished(Ok token)) ->
        let state = { state with AuthCode = Resolved <| Ok token }

        state, Cmd.none, LoginTokenReceived token

    | AuthCodeRequest (Finished(Error e)) ->
        printfn "%s" e
        let state = { state with AuthCode = Resolved <| Error e }

        state, Cmd.none, Noop

    | PhoneNumberUpdated phn ->
        let state = { state with PhoneNumber = phn }

        state, Cmd.none, Noop

let espressoImgUrl: string = importDefault "../espresso.jpg"

let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        img [ Src espressoImgUrl; Class "img-fluid" ]
        h1 [ Class "text-center" ] [ str "Log In" ]
        form [
            OnSubmit(fun ev ->
                ev.preventDefault()
                dispatch <| AuthCodeRequest Started)
        ] [
            div [ Class "mb-3" ] [
                label [ Class "form-label" ] [ str "Phone Number" ]
                input [
                    Type "tel"
                    if Deferred.didFail state.AuthCode then
                        Class "form-control form-control-lg is-invalid"
                    else
                        Class "form-control form-control-lg"
                    Placeholder "111-222-3333"
                    Value state.PhoneNumber
                    OnInput(fun ev -> dispatch <| PhoneNumberUpdated ev.Value)
                ]
                if Deferred.didFail state.AuthCode then
                    div [ Class "invalid-feedback" ] [
                        str "Please try again."
                    ]
            ]
            div [ Class "d-flex justify-content-end" ] [
                button [
                    Class "btn btn-primary btn-lg"
                    if Deferred.isInProgress state.AuthCode then
                        Class "btn btn-primary btn-lg disabled"
                ] [
                    if Deferred.isInProgress state.AuthCode then
                        span [ Class "spinner-grow spinner-grow-sm" ] []
                        str "Loading..."
                    else
                        str "Continue"
                        i [ Class "bi-arrow-right-square px-2" ] []
                ]
            ]
        ]
    ]
