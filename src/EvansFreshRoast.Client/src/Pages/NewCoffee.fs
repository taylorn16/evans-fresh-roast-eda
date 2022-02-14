module Pages.NewCoffee

open AsyncHelpers
open Types
open Elmish
open Fable.React
open Fable.React.Props
open System

type State =
    { Coffee: Coffee
      SaveCoffee: Deferred<Result<AsyncApiEventResponse, string>> }

type Msg =
    | NameUpdated of string
    | DescriptionUpdated of string
    | PriceUpdated of decimal
    | WeightUpdated of decimal
    | SaveCoffee of AsyncOperationEvt<Result<AsyncApiEventResponse, string>>

let init() =
    let empty =
        { Name = CoffeeName.create ""
          Description = CoffeeDescription.create ""
          PricePerBag = UsdPrice.create 0m
          WeightPerBag = OzWeight.create 0m }

    { Coffee = empty
      SaveCoffee = NotStarted },
    Cmd.none

let update msg state =
    match msg with
    | NameUpdated nm ->
        { state with
            Coffee = { state.Coffee with Name = CoffeeName.create nm } },
        Cmd.none

    | DescriptionUpdated desc ->
        { state with
            Coffee = { state.Coffee with Description = CoffeeDescription.create desc } },
        Cmd.none

    | PriceUpdated pr ->
        { state with
            Coffee = { state.Coffee with PricePerBag = UsdPrice.create pr } },
        Cmd.none

    | WeightUpdated wt ->
        { state with
            Coffee = { state.Coffee with WeightPerBag = OzWeight.create wt } },
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

let formInput label' value placeholder dispatch =
    fragment [] [
        label [ Class "form-label" ] [ str label' ]
        input [
            Props.Type "text"
            Class "form-control"
            Placeholder placeholder
            Value value
            OnInput dispatch
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
        div [ Class "mb-3" ] [
            formInput
                "Name"
                (CoffeeName.value state.Coffee.Name)
                "Colombian"
                (fun ev -> dispatch <| NameUpdated ev.Value)
        ]
        div [ Class "mb-3" ] [
            formInput
                "Description"
                (CoffeeDescription.value state.Coffee.Description)
                "Notes of delicious, tasty, and awesome"
                (fun ev -> dispatch <| DescriptionUpdated ev.Value)
        ]
        div [ Class "mb-3" ] [
            formInput
                "Weight (oz/per bag)"
                (OzWeight.value state.Coffee.WeightPerBag |> emptyIfZero)
                "16.0"
                (fun ev -> dispatch <| WeightUpdated (parseDecDef ev.Value))
        ]
        div [ Class "mb-3" ] [
            formInput
                "Price (USD/per bag)"
                (UsdPrice.value state.Coffee.PricePerBag |> emptyIfZero)
                "12.50"
                (fun ev -> dispatch <| PriceUpdated (parseDecDef ev.Value))
        ]
        div [ Class "d-flex justify-content-end" ] [
                button [
                    Class "btn btn-primary btn-lg"
                    if Deferred.isInProgress state.SaveCoffee then
                        Class "btn btn-primary btn-lg disabled"
                    OnClick(fun ev ->
                        ev.preventDefault()
                        dispatch <| SaveCoffee Started)
                ] [
                    if Deferred.isInProgress state.SaveCoffee then
                        span [ Class "spinner-grow spinner-grow-sm ps-2" ] []
                        str "Saving..."
                    else
                        str "Add Coffee"
                ]
            ]
    ]
