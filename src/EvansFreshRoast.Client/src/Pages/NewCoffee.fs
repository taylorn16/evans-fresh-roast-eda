module Pages.NewCoffee

open AsyncHelpers
open Elmish
open Fable.React
open Fable.React.Props
open System
open Routes
open EvansFreshRoast.Dto

type State =
    { Name: string
      Description: string
      PricePerBag: string
      WeightPerBag: string
      SaveCoffee: Deferred<Result<EventAcceptedResponse, string>> }

type Msg =
    | NameUpdated of string
    | DescriptionUpdated of string
    | PriceUpdated of string
    | WeightUpdated of string
    | SaveCoffee of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | CoffeeCreated of id: Guid

let init() =
    { Name = ""
      Description = ""
      PricePerBag = ""
      WeightPerBag = ""
      SaveCoffee = NotStarted },
    Cmd.none

let parseDecimal (s: string) =
    match Decimal.TryParse(s) with
    | true, d -> Some d
    | _ -> None

let update msg (state: State) =
    match msg with
    | NameUpdated nm ->
        { state with Name = nm },
        Cmd.none

    | DescriptionUpdated desc ->
        { state with Description = desc },
        Cmd.none

    | PriceUpdated pr ->
        { state with PricePerBag = pr },
        Cmd.none

    | WeightUpdated wt ->
        { state with WeightPerBag = wt },
        Cmd.none

    | SaveCoffee Started ->
        let price = parseDecimal state.PricePerBag
        let weight = parseDecimal state.WeightPerBag
        
        let cmd =
            Option.map2
                (fun pr wt ->
                    { Name = state.Name
                      Description = state.Description
                      PricePerBag = pr
                      WeightPerBag = wt })
                price
                weight
            |> Option.map (fun request ->
                async {
                    let! resp = Api.saveCoffee request
                    return SaveCoffee <| Finished resp
                }
                |> Cmd.OfAsync.result)
        
        if Option.isSome cmd then
            { state with SaveCoffee = InProgress }, cmd.Value
        else
            { state with
                WeightPerBag = ""
                PricePerBag = "" }, Cmd.none
        
    | SaveCoffee (Finished resp) ->
        { state with SaveCoffee = Resolved resp }, Cmd.none
        
    | CoffeeCreated id ->
        match state.SaveCoffee with
        | Resolved(Ok { AggregateId = coffeeId }) when coffeeId = id ->
            state, Route.navigateTo (Route.Coffee id)
                
        | _ -> state, Cmd.none

let formInput label' value placeholder dispatch =
    div [ Class "mb-3" ] [
        label [ Class "form-label" ] [ str label' ]
        input [
            Props.Type "text"
            Class "form-control"
            Placeholder placeholder
            Value value
            OnInput dispatch
            Required true
        ]
    ]

let emptyIfZero: decimal -> string =
    function
    | 0m -> ""
    | x -> string x

let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        h1 [ Class "mt-3" ] [ str "Add New Coffee" ]
        form [
            OnSubmit(fun ev ->
                ev.preventDefault()
                dispatch <| SaveCoffee Started)
        ] [
            formInput
                "Name"
                state.Name
                "Colombian"
                (fun ev -> dispatch <| NameUpdated ev.Value)
            formInput
                "Description"
                state.Description
                "Notes of delicious, tasty, and awesome"
                (fun ev -> dispatch <| DescriptionUpdated ev.Value)
            formInput
                "Weight (oz/per bag)"
                state.WeightPerBag
                "16.0"
                (fun ev -> dispatch <| WeightUpdated ev.Value)
            formInput
                "Price (USD/per bag)"
                state.PricePerBag
                "12.50"
                (fun ev -> dispatch <| PriceUpdated ev.Value)
            div [ Class "d-flex justify-content-end" ] [
                button [
                    Props.Type "submit"
                    Class "btn btn-primary btn-lg"
                    if Deferred.isInProgress state.SaveCoffee then
                        Class "btn btn-primary btn-lg disabled"
                ] [
                    if Deferred.isInProgress state.SaveCoffee then
                        span [ Class "spinner-grow spinner-grow-sm px-2" ] []
                        str "Saving..."
                    else
                        str "Add Coffee"
                ]
            ]
        ]
    ]
