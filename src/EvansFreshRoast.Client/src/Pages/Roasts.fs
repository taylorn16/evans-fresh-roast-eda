module Pages.Roasts

open Elmish
open AsyncHelpers
open Fable.React

type State =
    { Roasts: Deferred<Result<string list, string>>
      Coffees: Deferred<Result<string list, string>>
      Customers: Deferred<Result<string list, string>> }

type Msg =
    | GetRoasts of AsyncOperationEvt<Result<string list, string>>
    | GetCoffees of AsyncOperationEvt<Result<string list, string>>
    | GetCustomers of AsyncOperationEvt<Result<string list, string>>

let init() =
    { Roasts = NotStarted
      Coffees = NotStarted
      Customers = NotStarted },
    Cmd.batch
        [ Cmd.ofMsg <| GetRoasts Started
          Cmd.ofMsg <| GetCoffees Started
          Cmd.ofMsg <| GetCustomers Started ]

let update msg state =
    match msg with
    | GetRoasts Started ->
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
            |> Cmd.map (GetRoasts << Finished)

        { state with Roasts = InProgress }, cmd

    | GetRoasts (Finished (Ok rs)) ->
        { state with Roasts = Resolved <| Ok rs }, Cmd.none

    | GetRoasts (Finished (Error e)) ->
        { state with Roasts = Resolved <| Error e }, Cmd.none

let view (state: State) (dispatch: Msg -> unit) =
    match state.Roasts, state.Coffees, state.Customers with
    | Resolved (Ok rs), Resolved (Ok cfs), Resolved (Ok custs) ->
        div [] [ str "Loaded all data (roasts, coffees, customers)! Hooray!" ]

    | Resolved (Error e), _, _
    | _, Resolved (Error e), _
    | _, _, Resolved (Error e) ->
        div [] [ str $"Error loading data. Boo hoo. {e}" ]

    | _ ->
        div [] [ str "Loading Roasts..." ]
