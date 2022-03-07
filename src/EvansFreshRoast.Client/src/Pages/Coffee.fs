module Pages.Coffee

open System
open Elmish
open EvansFreshRoast.Dto
open Fable.React
open Fable.React.Props
open AsyncHelpers
open Routes

type State =
    { CoffeeId: Guid
      Coffee: Deferred<Result<Coffee, string>> }

type Msg =
    | FetchCoffee of AsyncOperationEvt<Result<Coffee, string>>

let init coffeeId =
    { CoffeeId = coffeeId
      Coffee = NotStarted },
    Cmd.ofMsg <| FetchCoffee Started

let update msg state =
    match msg with
    | FetchCoffee Started ->
        let cmd =
            Api.getCoffee state.CoffeeId
            |> Cmd.OfAsync.result
            |> Cmd.map (FetchCoffee << Finished)
        
        { state with Coffee = InProgress }, cmd

    | FetchCoffee(Finished result) ->
        { state with Coffee = Resolved result }, Cmd.none
        
let view (state: State) (_: Msg -> unit) =
    fragment [] [
        a [
            Href <| Route.toHash Route.Coffees
            Class "btn btn-outline-dark mt-3"
        ] [
            i [ Class "bi-arrow-left pe-1" ] []
            str "Back to All Coffees"
        ]
        match state.Coffee with
        | Resolved(Ok coffee) ->
            h2 [ Class "mt-3" ] [ str coffee.Name ]
            p [ Class "text-muted d-flex align-items-center" ] [
                span [] [
                    str $"""${coffee.PricePerBag.ToString("0.00")} / {coffee.WeightPerBag.ToString("0.0")} oz"""
                ]
                if coffee.IsActive then
                    span [ Class "badge bg-success fw-light ms-2 text-uppercase" ] [ str "Active" ]
                else
                    span [ Class "badge bg-secondary fw-light ms-2 text-uppercase" ] [ str "Inactive" ]
            ]
            p [ Class "lead" ] [ str coffee.Description ]
            button [ Class "btn btn-primary" ] [ str "Edit" ]
           
        | Resolved(Error _) ->
            p [ Class "mt-3" ] [ str "Sorry, there was a problem loading the information for this coffee." ]
         
        | NotStarted
        | InProgress ->
            div [
                Class "h-fullpage position-relative d-flex justify-content-center align-items-center"
            ] [
                div [ Class "spinner-grow text-primary" ] [
                    span [ Class "visually-hidden" ] [ str "Loading..." ]
                ]
            ]
    ]
