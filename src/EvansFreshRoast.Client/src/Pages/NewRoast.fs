module Pages.NewRoast

open AsyncHelpers
open EvansFreshRoast.Dto
open Elmish
open System
open Routes
open Fable.React
open Fable.React.Props

type State =
    { Roast: CreateRoastRequest
      SaveRoast: Deferred<Result<EventAcceptedResponse, string>> }

type Msg =
    | SaveRoast of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | RoastDateUpdated of string
    | OrderByDateUpdated of string
    | RoastCreated of id: Guid
    
let init() =
    { Roast =
        { Name = "<test_roast_name>"
          RoastDate = DateTimeOffset.MinValue
          OrderByDate = DateTimeOffset.MinValue }
      SaveRoast = NotStarted },
    Cmd.none
    
let update msg (state: State) =
    match msg with
    | SaveRoast Started ->
        let cmd =
            Api.saveRoast state.Roast
            |> Cmd.OfAsync.result
            |> Cmd.map (SaveRoast << Finished)
            
        { state with SaveRoast = InProgress }, cmd
        
    | SaveRoast(Finished result) ->
        { state with SaveRoast = Resolved result }, Cmd.none
        
    | RoastDateUpdated dt ->
        let dto =
            match DateTimeOffset.TryParse dt with
            | true, parsed -> parsed
            | _ -> DateTimeOffset.MinValue
        
        { state with
            Roast = { state.Roast with RoastDate = dto } },
        Cmd.none
        
    | OrderByDateUpdated dt ->
        let dto =
            match DateTimeOffset.TryParse dt with
            | true, parsed -> parsed
            | _ -> DateTimeOffset.MinValue
        
        { state with
            Roast = { state.Roast with OrderByDate = dto } },
        Cmd.none
        
    | RoastCreated id ->
        match state.SaveRoast with
        | Resolved(Ok { AggregateId = roastId }) when roastId = id ->
            state, Route.navigateTo (Route.Roast id)
            
        | _ ->
            state, Cmd.none

let formInput label' value placeholder dispatch helptext =
    div [ Class "mb-3" ] [
        label [ Class "form-label" ] [ str label' ]
        input [
            Type "date"
            Class "form-control"
            Placeholder placeholder
            Value value
            OnInput dispatch
            Required true
        ]
        match helptext with
        | Some helptext ->
            p [ Class "form-text" ] [ str helptext ]
            
        | None ->
            fragment [] []
    ]

let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        h1 [ Class "mt-3" ] [ str "Add New Roast" ]
        form [
            OnSubmit <| fun ev ->
                ev.preventDefault()
                dispatch <| SaveRoast Started
        ] [
            formInput
                "Roast Date"
                (state.Roast.RoastDate.ToString("yyyy-MM-dd"))
                "2022-02-22"
                (fun ev -> dispatch <| RoastDateUpdated ev.Value)
                None
            formInput
                "Order By Date"
                (state.Roast.OrderByDate.ToString("yyyy-MM-dd"))
                "2022-02-21"
                (fun ev -> dispatch <| OrderByDateUpdated ev.Value)
                (Some "Must be before the roast date.")
            div [ Class "d-flex justify-content-end" ] [
                button [
                    Type "submit"
                    Class "btn btn-primary btn-lg"
                    if Deferred.isInProgress state.SaveRoast then
                        Class "btn btn-primary btn-lg disabled"
                ] [
                    if Deferred.isInProgress state.SaveRoast then
                        span [ Class "spinner-grow spinner-grow-sm px-2" ] []
                        str "Saving..."
                    else
                        str "Add Roast"
                ]
            ]
        ]
    ]
