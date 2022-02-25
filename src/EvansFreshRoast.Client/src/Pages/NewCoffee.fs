module Pages.NewCoffee

open AsyncHelpers
open Elmish
open Fable.React
open Fable.React.Props
open System
open Routes
open EvansFreshRoast.Dto

type State =
    { Coffee: CreateCoffeeRequest
      SaveCoffee: Deferred<Result<EventAcceptedResponse, string>> }

type Msg =
    | NameUpdated of string
    | DescriptionUpdated of string
    | PriceUpdated of decimal
    | WeightUpdated of decimal
    | SaveCoffee of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | CoffeeCreated of id: Guid

let init() =
    let empty =
        { Name = ""
          Description = ""
          PricePerBag = 0m
          WeightPerBag = 0m }

    { Coffee = empty
      SaveCoffee = NotStarted },
    Cmd.none

let update msg state =
    match msg with
    | NameUpdated nm ->
        { state with
            Coffee = { state.Coffee with Name = nm } },
        Cmd.none

    | DescriptionUpdated desc ->
        { state with
            Coffee = { state.Coffee with Description = desc } },
        Cmd.none

    | PriceUpdated pr ->
        { state with
            Coffee = { state.Coffee with PricePerBag = pr } },
        Cmd.none

    | WeightUpdated wt ->
        { state with
            Coffee = { state.Coffee with WeightPerBag = wt } },
        Cmd.none

    | SaveCoffee Started ->
        let task =
            async {
                let! resp = Api.saveCoffee state.Coffee
                return SaveCoffee (Finished resp)
            }

        { state with SaveCoffee = InProgress }, Cmd.OfAsync.result task
        
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

let parseDecimal (s: string) =
    match Decimal.TryParse(s) with
    | true, d -> Some d
    | _ -> None

let parseDecDef =
    parseDecimal
    >> Option.defaultValue 0m

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
                state.Coffee.Name
                "Colombian"
                (fun ev -> dispatch <| NameUpdated ev.Value)
            formInput
                "Description"
                state.Coffee.Description
                "Notes of delicious, tasty, and awesome"
                (fun ev -> dispatch <| DescriptionUpdated ev.Value)
            formInput
                "Weight (oz/per bag)"
                (state.Coffee.WeightPerBag |> emptyIfZero)
                "16.0" // TODO: figure out why these aren't parsing correctly with decimal points
                (fun ev -> dispatch <| WeightUpdated (parseDecDef ev.Value))
            formInput
                "Price (USD/per bag)"
                (state.Coffee.PricePerBag |> emptyIfZero)
                "12.50"
                (fun ev -> dispatch <| PriceUpdated (parseDecDef ev.Value))
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
