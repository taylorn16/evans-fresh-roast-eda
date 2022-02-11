module Pages.Login

open AsyncHelpers
open Elmish
open Fable.React
open Fable.React.Props
open Fable.Core.JsInterop

type State =
    { PhoneNumber: string
      AuthCodeRequest: Deferred<Result<string, string>> }

type GlobalMsg =
    | Noop
    | LoginTokenReceived of string

type Msg =
    | AuthCodeRequest of AsyncOperationEvt<Result<string, string>>
    | PhoneNumberUpdated of string 

let init() =
    { PhoneNumber = ""
      AuthCodeRequest = NotStarted },
    Cmd.none

let update msg state =
    match msg with
    | AuthCodeRequest Started ->
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

        { state with AuthCodeRequest = InProgress }, cmd, Noop

    | AuthCodeRequest (Finished(Ok token)) ->
        let state = { state with AuthCodeRequest = Resolved <| Ok token }

        state, Cmd.none, LoginTokenReceived token

    | AuthCodeRequest (Finished(Error e)) ->
        printfn "%s" e
        let state = { state with AuthCodeRequest = Resolved <| Error e }

        state, Cmd.none, Noop

    | PhoneNumberUpdated phn ->
        let state = { state with PhoneNumber = phn }

        state, Cmd.none, Noop

let espressoImgUrl: string = importDefault "../espresso.jpg"

let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        img [ Src espressoImgUrl; Class "img-fluid" ]
        h1 [ Class "text-center" ] [ str "Log In" ]
        div [] [
            div [ Class "mb-3" ] [
                label [ Class "form-label" ] [ str "Phone Number" ]
                input [
                    Type "tel"
                    Class "form-control form-control-lg"
                    Placeholder "e.g., 111-222-3333"
                    Value state.PhoneNumber
                    OnInput(fun ev -> dispatch <| PhoneNumberUpdated ev.Value)
                ]
            ]
            div [ Class "d-flex justify-content-center" ] [
                button [
                    Class "btn btn-primary btn-lg"
                    OnClick(fun ev ->
                        ev.preventDefault()
                        dispatch <| AuthCodeRequest Started)
                ] [
                    str "Continue"
                    i [ Class "bi-arrow-right-square ps-2" ] []
                ]
            ]
        ]
    ]
