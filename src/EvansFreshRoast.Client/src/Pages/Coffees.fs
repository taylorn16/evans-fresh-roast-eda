module Pages.Coffees

open AsyncHelpers
open EvansFreshRoast.Dto
open Elmish
open Fable.React
open Fable.React.Props
open Routes

type State =
    { Coffees: Deferred<Result<Coffee list, string>> }

type Msg =
    | FetchCoffees of AsyncOperationEvt<Result<Coffee list, string>>

let init() =
    { Coffees = NotStarted }, Cmd.ofMsg <| FetchCoffees Started

let update (msg: Msg) (state: State) =
    match msg with
    | FetchCoffees Started ->
        let cmd =
            Api.getCoffees
            |> Cmd.OfAsync.result
            |> Cmd.map (FetchCoffees << Finished)

        { state with Coffees = InProgress }, cmd
        
    | FetchCoffees(Finished result) ->
        { state with Coffees = Resolved result }, Cmd.none
        
let view (state: State) (_: Msg -> unit) =
    match state.Coffees with
    | Resolved(Ok []) ->
        fragment [] [
            div [
                Class "mt-3 rounded border border-3 border-dashed d-flex align-items-center"
                Style [ MinHeight "8rem" ]
            ] [
                p [ Class "flex-grow-1 mb-0 text-center" ] [
                    str "No Coffees Yet. "
                    a [ Href <| Route.toHash Route.NewCoffee ] [ str "Add one." ]
                ]
            ]
        ]
    
    | Resolved(Ok coffees) ->
        fragment [] [
            div [ Class "my-4 d-flex justify-content-between align-items-center" ] [
                h2 [ Class "my-0" ] [ str "Coffees" ]
                a [
                    Class "btn btn-primary"
                    Href <| Route.toHash Route.NewCoffee
                ] [ str "New Coffee" ]
            ]
            fragment [] (coffees |> List.map(fun coffee ->
                div [ Class "card mt-3" ] [
                    div [ Class "card-body" ] [
                        h5 [ Class "card-title" ] [ str coffee.Name ]
                        p [ Class "card-text" ] [
                            if coffee.IsActive then
                                span [ Class "badge bg-success fw-light text-uppercase me-2" ] [ str "Active" ]
                            else
                                span [ Class "badge bg-secondary fw-light text-uppercase me-2" ] [ str "Inactive" ]
                            span [ Class "text-muted" ] [
                                str $"""${coffee.PricePerBag.ToString("0.00")} / {coffee.WeightPerBag.ToString("0.0")} oz"""
                            ]
                            br []
                            span [ Class "d-block pt-2s" ] [ str coffee.Description ]
                        ]
                        a [
                            Class "btn btn-outline-primary"
                            Href <| Route.toHash (Route.Coffee coffee.Id)
                        ] [
                            str "See Details"
                        ]
                    ]
                ]))
        ]

    | _ ->
        div [
            Class "h-fullpage position-relative d-flex justify-content-center align-items-center"
        ] [
            div [ Class "spinner-grow text-primary" ] [
                span [ Class "visually-hidden" ] [ str "Loading..." ]
            ]
        ]