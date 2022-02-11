module Pages.Roasts

open Elmish
open AsyncHelpers
open Fable.React

type State =
    { Roasts: Deferred<Result<string list, string>> }

type Msg =
    | GetRoastsRequest of AsyncOperationEvt<Result<string list, string>>

let init() =
    { Roasts = NotStarted }, Cmd.ofMsg <| GetRoastsRequest Started

let update msg state =
    match msg with
    | GetRoastsRequest Started ->
        let cmd =
            async {
                match! Api.getRoasts() with
                | Ok () ->
                    return Ok []

                | Error e ->
                    printfn "%s" e
                    return Error e
            }
            |> Cmd.OfAsync.result
            |> Cmd.map (GetRoastsRequest << Finished)

        { state with Roasts = InProgress }, cmd

    | GetRoastsRequest (Finished (Ok rs)) ->
        { state with Roasts = Resolved <| Ok rs }, Cmd.none

    | GetRoastsRequest (Finished (Error e)) ->
        { state with Roasts = Resolved <| Error e }, Cmd.none

let view (state: State) (dispatch: Msg -> unit) =
    match state.Roasts with
    | NotStarted
    | InProgress ->
        div [] [ str "Loading Roasts..." ]

    | Resolved (Ok _) ->
        div [] [ str "Loaded roasts! Hooray!" ]

    | Resolved (Error e) ->
        div [] [ str $"Error loading roasts. Boo hoo. {e}" ]
