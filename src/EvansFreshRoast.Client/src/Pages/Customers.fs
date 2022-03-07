module Pages.Customers

open System
open AsyncHelpers
open EvansFreshRoast.Domain
open EvansFreshRoast.Dto
open Elmish
open Fable.React
open Fable.React.Props
open Routes

type State =
    { Customers: Deferred<Result<Customer list, string>> }

type Msg =
    | FetchCustomers of AsyncOperationEvt<Result<Customer list, string>>
    | CustomerStatusChanged of id: Guid * status: CustomerStatus
    | CustomerAdded of Customer
    
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
        
    | CustomerStatusChanged(id, status) ->
        match state.Customers with
        | Resolved(Ok customers) ->
            let updatedCustomer =
                customers
                |> List.tryFind (fun c -> c.Id = id)
                |> Option.map (fun c -> { c with Status = status })
            
            let allOtherCustomers =
                customers
                |> List.filter (fun c -> c.Id <> id)
                
            let newCustomers =
                match updatedCustomer with
                | Some c ->
                    c :: allOtherCustomers
                
                | None ->
                    allOtherCustomers
            
            { state with Customers = Resolved(Ok newCustomers) }, Cmd.none
            
        | _ ->
            state, Cmd.none
            
    | CustomerAdded customer ->
        match state.Customers with
        | Resolved(Ok customers) ->
            let newCustomers =
                customers
                |> List.filter (fun c -> c.Id <> customer.Id)
                |> List.append [ customer ]
                
            { state with Customers = Resolved(Ok newCustomers) }, Cmd.none
            
        | _ ->
            state, Cmd.none
        
let view (state: State) (_: Msg -> unit) =
    match state.Customers with
    | Resolved(Ok []) ->
        fragment [] [
            div [
                Class "mt-3 rounded border border-3 border-dashed d-flex align-items-center"
                Style [ MinHeight "8rem" ]
            ] [
                p [ Class "flex-grow-1 mb-0 text-center" ] [
                    str "No Customers Yet. "
                    a [ Href <| Route.toHash Route.NewCustomer ] [ str "Add one." ]
                ]
            ]
        ]
    
    | Resolved(Ok customers) ->
        fragment [] [
            div [ Class "mt-4 d-flex justify-content-between align-items-center" ] [
                h2 [ Class "my-0" ] [ str "Customers" ]
                a [
                    Class "btn btn-primary"
                    Href <| Route.toHash Route.NewCustomer
                ] [ str "New Customer" ]
            ]
            table [ Class "table mt-3" ] [
                thead [] [
                    tr [] [
                        th [ Scope "col" ] [ str "Name" ]
                        th [ Scope "col" ] [ str "Phone Number" ]
                        th [ Scope "col" ] [ str "Status" ]
                    ]
                ]
                tbody [] (customers |> List.sortBy (fun c -> c.Name) |> List.map (fun customer ->
                    tr [] [
                        td [] [ str customer.Name ]
                        td [] [ str customer.PhoneNumber ]
                        td [] [ str <| string customer.Status ]
                    ]))
            ]
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
