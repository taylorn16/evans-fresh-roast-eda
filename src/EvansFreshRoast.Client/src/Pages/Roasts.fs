module Pages.Roasts

open Elmish
open AsyncHelpers
open EvansFreshRoast.Domain
open EvansFreshRoast.Dto
open Fable.React
open Fable.React.Props
open Routes
open Fable.DateFunctions

type State =
    { Roasts: Deferred<Result<RoastSummary list, string>>
      Coffees: Deferred<Result<Coffee list, string>>
      Customers: Deferred<Result<Customer list, string>> }

type Msg =
    | GetRoasts of AsyncOperationEvt<Result<RoastSummary list, string>>
    | GetCoffees of AsyncOperationEvt<Result<Coffee list, string>>
    | GetCustomers of AsyncOperationEvt<Result<Customer list, string>>

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
            Api.getRoasts
            |> Cmd.OfAsync.result
            |> Cmd.map (GetRoasts << Finished)

        { state with Roasts = InProgress }, cmd

    | GetRoasts (Finished result) ->
        { state with Roasts = Resolved result }, Cmd.none

    | GetCoffees Started ->
        let cmd =
            Api.getCoffees
            |> Cmd.OfAsync.result
            |> Cmd.map (GetCoffees << Finished)

        { state with Coffees = InProgress }, cmd

    | GetCoffees (Finished result) ->
        { state with Coffees = Resolved result }, Cmd.none

    | GetCustomers Started ->
        let cmd =
            Api.getCustomers
            |> Cmd.OfAsync.result
            |> Cmd.map (GetCustomers << Finished)

        { state with Customers = InProgress }, cmd

    | GetCustomers (Finished result) ->
        { state with Customers = Resolved result }, Cmd.none


let view (state: State) (dispatch: Msg -> unit) =
    match state.Roasts, state.Coffees, state.Customers with
    | Resolved (Ok roasts), Resolved (Ok coffees), Resolved (Ok customers) ->
        match roasts with
        | [] ->
            fragment [] [
                div [
                    Class "mt-3 rounded border border-3 border-dashed d-flex align-items-center"
                    Style [ MinHeight "8rem" ]
                ] [
                    p [ Class "flex-grow-1 mb-0 text-center" ] [
                        str "No Roasts Yet. "
                        a [ Href <| Route.toHash Route.NewRoast ] [ str "Add one." ]
                    ]
                ]
            ]
            
        | roasts ->
            fragment [] [
                div [ Class "my-4 d-flex justify-content-between align-items-center" ] [
                    h2 [ Class "my-0" ] [ str "Roasts" ]
                    a [
                        Class "btn btn-primary"
                        Href <| Route.toHash Route.NewRoast
                    ] [ str "New Roast" ]
                ]
                fragment [] (roasts |> List.map (fun roast ->
                    div [ Class "card mt-3 position-relative" ] [
                        div [ Class "card-body" ] [
                            if roast.RoastStatus = RoastStatus.Open then
                                span [
                                    Class "fw-light text-light text-uppercase position-absolute top-0 start-100 bg-success p-2 border border-light rounded"
                                    Style [ Transform "translate(-80%, -30%)"; FontSize "0.75rem"; LetterSpacing "0.05rem" ]
                                ] [
                                    str "Open"
                                ]
                            div [ Class "d-flex" ] [
                                div [ Class "card-left flex-grow-1" ] [
                                    h5 [ Class "card-title" ] [
                                        str $"""{roast.RoastDate.Format("MMM dd, yyyy")}"""
                                    ]
                                    h6 [ Class "card-subtitle mb-2 text-muted" ] [
                                        str $"""Order By {roast.OrderByDate.Format("MMM dd, yyyy")}"""
                                    ]
                                    p [ Class "card-text d-flex align-items-center" ] [
                                        span [ Class "d-flex align-items-center me-4" ] [
                                            span [ Class "fs-1 pe-2" ] [ str <| string roast.OrdersCount ]
                                            span [] [ str "Order(s)" ]
                                        ]
                                        span [ Class "d-flex align-items-center" ] [
                                            span [ Class "fs-1 pe-2" ] [ str <| string roast.Coffees.Length ]
                                            span [] [ str "Coffee(s)" ]
                                        ]
                                    ]
                                ]
                                div [ Class "card-right d-flex align-items-center" ] [
                                    a [
                                        Class "btn btn-outline-primary"
                                        Href <| Route.toHash (Route.Roast roast.Id)
                                    ] [ str "Details" ]
                                ]
                            ]
                        ]
                    ]))
            ]

    | Resolved (Error e), _, _
    | _, Resolved (Error e), _
    | _, _, Resolved (Error e) ->
        div [] [ str "Error loading data." ]

    | _ ->
        div [
            Class "h-fullpage position-relative d-flex justify-content-center align-items-center"
        ] [
            div [ Class "spinner-grow text-primary" ] [
                span [ Class "visually-hidden" ] [ str "Loading..." ]
            ]
        ]
