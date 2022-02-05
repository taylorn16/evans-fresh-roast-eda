module EvansFreshRoast.App

open Feliz
open Elmish
open Elmish.React

type State =
    { Count: int }

type Msg =
    | Increment
    | Decrement

let init () =
    { Count = 0 }, Cmd.none

let update (msg: Msg) (state: State) =
    match msg with
    | Increment ->
        { state with Count = state.Count + 1 }, Cmd.none

    | Decrement ->
        { state with Count = state.Count - 1 }, Cmd.none

let view (state: State) (dispatch: Msg -> unit) =
    Html.div [
        Html.p state.Count
        Html.button [
            prop.onClick (fun _ -> dispatch Increment)
            prop.children [
                Html.text "Click Me!"
            ]
        ]
        Html.button [
            prop.onClick (fun _ -> dispatch Decrement)
            prop.children [
                Html.text "NO, CLICK ME!"
            ]
        ]
    ]

Program.mkProgram init update view
|> Program.withReactBatched "app-root"
|> Program.run
