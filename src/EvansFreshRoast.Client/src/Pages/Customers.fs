module Pages.Customers

open AsyncHelpers
open EvansFreshRoast.Dto
open Elmish
open Fable.React
open Fable.React.Props
open Routes

type State =
    { Customers: Deferred<Result<Customer list, string>> }

type Msg =
    | FetchCustomers of AsyncOperationEvt<Result<Customer list, string>>
    
let init() =
    { Customers = NotStarted },
    Cmd.ofMsg <| FetchCustomers Started

let update msg (state: State) =
    match msg with
    | FetchCustomers Started ->
        let cmd =
            Api.getCustomers
            |> Cmd.OfAsync.result
            |> Cmd.map (FetchCustomers << Finished)
            
        { state with Customers = InProgress }, cmd

    | FetchCustomers(Finished result) ->
        { state with Customers = Resolved result }, Cmd.none
        
let view (state: State) (dispatch: Msg -> unit) =
    fragment [] [
        match state.Customers with
        | Resolved(Ok []) ->
            fragment [] [
                div [
                    Class "mt-3 rounded border border-3 border-dashed d-flex align-items-center"
                    Style [ MinHeight "8rem" ]
                ] [
                    p [ Class "flex-grow-1 mb-0 text-center" ] [
                        str "No Coffees Yet. "
                        a [ Href <| Route.toHash Route.NewCustomer ] [ str "Add one." ]
                    ]
                ]
            ]
        
        | Resolved(Ok customers) ->
            table [ Class "table" ] [
                thead [] [
                    tr [] [
                        th [ Scope "col" ] [ str "Name" ]
                        th [ Scope "col" ] [ str "Phone Number" ]
                        th [ Scope "col" ] [ str "Status" ]
                    ]
                ]
                tbody [] (customers |> List.map (fun customer ->
                    tr [] [
                        td [] [ str customer.Name ]
                        td [] [ str customer.PhoneNumber ]
                        td [] [ str "<status>" ]
                    ]))
            ]
            
        | Resolved(Error _) ->
            str "Error!"
            
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
