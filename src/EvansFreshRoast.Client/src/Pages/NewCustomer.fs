module Pages.NewCustomer

open System
open AsyncHelpers
open EvansFreshRoast.Dto
open Elmish
open Fable.React
open Fable.React.Props
open Routes

type State =
    { Customer: CreateCustomerRequest
      SaveCustomer: Deferred<Result<EventAcceptedResponse, string>> }

type Msg =
    | SaveCustomer of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | NameUpdated of string
    | PhoneNumberUpdated of string
    | CustomerCreated of id: Guid

let init() =
    { Customer =
        { Name = ""
          PhoneNumber = "" }
      SaveCustomer = NotStarted },
    Cmd.none

let update msg state =
    match msg with
    | NameUpdated nm ->
        { state with Customer = { state.Customer with Name = nm } }, Cmd.none
        
    | PhoneNumberUpdated phn ->
        { state with Customer = { state.Customer with PhoneNumber = phn } }, Cmd.none

    | SaveCustomer Started ->
        let cmd =
            Api.saveCustomer state.Customer
            |> Cmd.OfAsync.result
            |> Cmd.map (SaveCustomer << Finished)
            
        { state with SaveCustomer = InProgress }, cmd
        
    | SaveCustomer(Finished result) ->
        { state with SaveCustomer = Resolved result }, Cmd.none
        
    | CustomerCreated id ->
        printfn $"%A{state}"
        
        match state.SaveCustomer with
        | Resolved(Ok { AggregateId = customerId }) when customerId = id ->
            state, Route.navigateTo Route.Customers
            
        | _ ->
            state, Cmd.none
        
let formInput type' label' value placeholder dispatch =
    div [ Class "mb-3" ] [
        label [ Class "form-label" ] [ str label' ]
        input [
            Type (type' |> Option.defaultValue "text")
            Class "form-control"
            Placeholder placeholder
            Value value
            OnInput dispatch
            Required true
        ]
    ]
        
let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        h1 [ Class "mt-3" ] [ str "Add New Customer" ]
        form [
            OnSubmit(fun ev ->
                ev.preventDefault()
                dispatch <| SaveCustomer Started)
        ] [
            formInput
                None
                "Name"
                state.Customer.Name
                "John Doe"
                (fun ev -> dispatch <| NameUpdated ev.Value)
            formInput
                (Some "tel")
                "Phone Number"
                state.Customer.PhoneNumber
                "1112223333"
                (fun ev -> dispatch <| PhoneNumberUpdated ev.Value)
            div [ Class "d-flex justify-content-end" ] [
                button [
                    Type "submit"
                    Class "btn btn-primary btn-lg"
                    if Deferred.isInProgress state.SaveCustomer then
                        Class "btn btn-primary btn-lg disabled"
                ] [
                    if Deferred.isInProgress state.SaveCustomer then
                        span [ Class "spinner-grow spinner-grow-sm px-2" ] []
                        str "Saving..."
                    else
                        str "Add Customer"
                ]
            ]
        ]
    ]
