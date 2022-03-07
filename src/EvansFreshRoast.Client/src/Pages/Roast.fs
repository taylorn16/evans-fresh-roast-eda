module Pages.Roast

open AsyncHelpers
open Elmish
open EvansFreshRoast.Dto
open Fable.React
open Fable.React.Props
open System
open Fable.DateFunctions
open EvansFreshRoast.Domain
open Fable.Core

// TODO: this page is chonky, need to maybe add separate routes for full-screen modal interactions?

type IBootstrapModal =
    abstract toggle: unit -> unit 

[<Emit("bootstrap.Modal.getOrCreateInstance(document.querySelector($0), {})")>]
let getModal (selector: string): IBootstrapModal = jsNative

type Tab =
    | Orders
    | Coffees
    | Customers
    static member allTabs = [ Orders; Coffees; Customers ]

type State =
    { RoastId: Guid
      FetchRoast: Deferred<Result<RoastDetails, string>>
      FetchCoffees: Deferred<Result<EvansFreshRoast.Dto.Coffee list, string>>
      FetchCustomers: Deferred<Result<EvansFreshRoast.Dto.Customer list, string>>
      AddCoffees: Deferred<Result<EventAcceptedResponse, string>>
      AddCustomers: Deferred<Result<EventAcceptedResponse, string>>
      SelectedCoffees: Set<Guid>
      SelectedCustomers: Set<Guid>
      Tab: Tab
      OpenRoast: Deferred<Result<EventAcceptedResponse, string>>
      CloseRoast: Deferred<Result<EventAcceptedResponse, string>>
      PayOrderModalCustomerId: Guid option
      PayOrder: Deferred<Result<EventAcceptedResponse, string>>
      SendFollowUp: Deferred<Result<EventAcceptedResponse, string>> }
    
type Msg =
    | FetchRoast of AsyncOperationEvt<Result<RoastDetails, string>>
    | FetchCoffees of AsyncOperationEvt<Result<EvansFreshRoast.Dto.Coffee list, string>>
    | FetchCustomers of AsyncOperationEvt<Result<EvansFreshRoast.Dto.Customer list, string>>
    | TabSelected of Tab
    | SelectCoffeeToggled of coffeeId: Guid
    | SelectedCoffeesCleared
    | SelectCustomerToggled of customerId: Guid
    | CustomersSelected of customerIds: Guid list
    | SelectedCustomersCleared
    | AddCoffees of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | CoffeesAdded of roastId: Guid * coffeeIds: Guid list
    | AddCustomers of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | CustomersAdded of roastId: Guid * coffeeIds: Guid list
    | OpenRoast of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | CloseRoast of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | RoastOpened of roastId: Guid
    | RoastClosed of roastId: Guid
    | PayOrder of customerId: Guid option * AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | OrderPaid of customerId: Guid
    | UpdatePayOrderModalCustomerId of customerId: Guid
    | SendFollowUp of AsyncOperationEvt<Result<EventAcceptedResponse, string>>
    | FollowUpSent of roastId: Guid
    
let init id =
    { RoastId = id
      FetchRoast = NotStarted
      FetchCoffees = NotStarted
      FetchCustomers = NotStarted
      AddCoffees = NotStarted
      AddCustomers = NotStarted
      Tab = Orders
      SelectedCoffees = Set.empty
      SelectedCustomers = Set.empty
      OpenRoast = NotStarted
      CloseRoast = NotStarted
      PayOrder = NotStarted
      PayOrderModalCustomerId = None
      SendFollowUp = NotStarted },
    Cmd.batch [
        Cmd.ofMsg <| FetchRoast Started
        Cmd.ofMsg <| FetchCoffees Started
        Cmd.ofMsg <| FetchCustomers Started
    ]

let update msg state =
    match msg with
    | FetchRoast Started ->
        let cmd =
            Api.getRoast state.RoastId
            |> Cmd.OfAsync.result
            |> Cmd.map (FetchRoast << Finished)
        
        { state with FetchRoast = InProgress }, cmd
        
    | FetchRoast(Finished result) ->
        { state with FetchRoast = Resolved result }, Cmd.none
    
    | FetchCoffees Started ->
        let cmd =
            Api.getCoffees
            |> Cmd.OfAsync.result
            |> Cmd.map (FetchCoffees << Finished)
            
        { state with FetchCoffees = InProgress }, cmd
        
    | FetchCoffees(Finished result) ->
        { state with FetchCoffees = Resolved result }, Cmd.none
        
    | FetchCustomers Started ->
        let cmd =
            Api.getCustomers
            |> Cmd.OfAsync.result
            |> Cmd.map (FetchCustomers << Finished)
            
        { state with FetchCustomers = InProgress }, cmd
        
    | FetchCustomers(Finished result) ->
        { state with FetchCustomers = Resolved result }, Cmd.none
        
    | TabSelected tab ->
        { state with Tab = tab }, Cmd.none
        
    | SelectCoffeeToggled coffeeId ->
        let newSet =
            if state.SelectedCoffees |> Set.contains coffeeId then
                Set.difference state.SelectedCoffees (Set.singleton coffeeId)
            else
                Set.add coffeeId state.SelectedCoffees
                
        { state with SelectedCoffees = newSet }, Cmd.none
        
    | SelectedCoffeesCleared ->
        { state with SelectedCoffees = Set.empty }, Cmd.none

    | SelectCustomerToggled customerId ->
        let newSet =
            if state.SelectedCustomers |> Set.contains customerId then
                Set.difference state.SelectedCustomers (Set.singleton customerId)
            else
                Set.add customerId state.SelectedCustomers
                
        { state with SelectedCustomers = newSet }, Cmd.none
        
    | CustomersSelected customerIds ->
        { state with SelectedCustomers = state.SelectedCustomers |> Set.union (Set.ofList customerIds) },
        Cmd.none
        
    | SelectedCustomersCleared ->
        { state with SelectedCustomers = Set.empty }, Cmd.none
    
    | AddCoffees Started ->
        let cmd =
            Api.putRoastCoffees state.RoastId (List.ofSeq state.SelectedCoffees)
            |> Cmd.OfAsync.result
            |> Cmd.map (AddCoffees << Finished)
            
        { state with AddCoffees = InProgress }, cmd
        
    | AddCoffees(Finished result) ->
        { state with AddCoffees = Resolved result }, Cmd.none
    
    | CoffeesAdded(roastId, coffeeIds) ->
        match state.FetchRoast with
        | Resolved(Ok roast) when roast.Id = roastId ->
            let modal = getModal "#addCoffeesModal"
            modal.toggle()
            
            { state with FetchRoast = Resolved(Ok { roast with Coffees = roast.Coffees @ coffeeIds }) },
            Cmd.none
            
        | _ ->
            state, Cmd.none
    
    | AddCustomers Started ->
        let cmd =
            Api.putRoastCustomers state.RoastId (List.ofSeq state.SelectedCustomers)
            |> Cmd.OfAsync.result
            |> Cmd.map (AddCustomers << Finished)
            
        { state with AddCustomers = InProgress }, cmd
        
    | AddCustomers(Finished result) ->
        { state with AddCustomers = Resolved result }, Cmd.none
        
    | CustomersAdded(roastId, customerIds) ->
        match state.FetchRoast with
        | Resolved(Ok roast) when roast.Id = roastId ->
            let modal = getModal "#addCustomersModal"
            modal.toggle()
            
            { state with FetchRoast = Resolved(Ok { roast with Customers = roast.Customers @ customerIds }) },
            Cmd.none
            
        | _ ->
            state, Cmd.none
            
    | OpenRoast Started ->
        let cmd =
            Api.postOpenRoast state.RoastId
            |> Cmd.OfAsync.result
            |> Cmd.map (OpenRoast << Finished)
            
        { state with OpenRoast = InProgress }, cmd
        
    | OpenRoast(Finished result) ->
        { state with OpenRoast = Resolved result }, Cmd.none
     
    | CloseRoast Started ->
        let cmd =
            Api.postRoastComplete state.RoastId
            |> Cmd.OfAsync.result
            |> Cmd.map (CloseRoast << Finished)
            
        { state with CloseRoast = InProgress }, cmd
        
    | CloseRoast(Finished result) ->
        { state with CloseRoast = Resolved result }, Cmd.none
        
    | RoastOpened roastId ->
        match state.FetchRoast with
        | Resolved(Ok roast) when roast.Id = roastId ->
            let modal = getModal "#confirmOpenRoastModal"
            modal.toggle()
            
            { state with FetchRoast = Resolved(Ok { roast with Status = Open }) },
            Cmd.none
            
        | _ ->
            state, Cmd.none
            
    | RoastClosed roastId ->
        match state.FetchRoast with
        | Resolved(Ok roast) when roast.Id = roastId ->
            let modal = getModal "#confirmCloseRoastModal"
            modal.toggle()
            
            { state with FetchRoast = Resolved(Ok { roast with Status = Closed }) },
            Cmd.none
            
        | _ ->
            state, Cmd.none
            
    | PayOrder(Some customerId, Started) ->
        let cmd =
            Api.postOrderPaid state.RoastId customerId
            |> Cmd.OfAsync.result
            |> Cmd.map (fun result -> PayOrder(None, Finished result))
            
        { state with PayOrder = InProgress; PayOrderModalCustomerId = None }, cmd
        
    | PayOrder(None, Started) ->
        state, Cmd.none
        
    | PayOrder(_, Finished result) ->
        { state with PayOrder = Resolved result }, Cmd.none
        
    | OrderPaid customerId ->
        match state.FetchRoast with
        | Resolved(Ok roast) ->
            let updatedOrder =
                roast.Orders
                |> List.tryFind (fun ord -> ord.CustomerId = customerId)
                |> Option.map (fun ord ->
                    match ord.Invoice with
                    | Some invoice ->
                        { ord with Invoice = Some { invoice with PaymentMethod = Some "Unknown" } }
                        
                    | None -> ord)
                
            match updatedOrder with
            | Some order ->
                let modal = getModal "#confirmInvoicePaidModal"
                modal.toggle()
                
                let updatedRoast =
                    { roast with
                        Orders = roast.Orders
                                 |> List.filter (fun ord -> ord.CustomerId <> customerId)
                                 |> List.append [ order ] }
                
                { state with FetchRoast = Resolved(Ok updatedRoast) }, Cmd.none
                
            | None ->
                state, Cmd.none
                
        | _ ->
            state, Cmd.none
            
    | UpdatePayOrderModalCustomerId customerId ->
        { state with PayOrderModalCustomerId = Some customerId }, Cmd.none
        
    | SendFollowUp Started ->
        let cmd =
            Api.postFollowUp state.RoastId
            |> Cmd.OfAsync.result
            |> Cmd.map (SendFollowUp << Finished)
            
        { state with SendFollowUp = InProgress }, cmd
            
    | SendFollowUp(Finished result) ->
        { state with SendFollowUp = Resolved result }, Cmd.none
        
    | FollowUpSent roastId ->
        match state.FetchRoast with
        | Resolved(Ok roast) when roast.Id = roastId ->
            let modal = getModal "#confirmSendFollowUpModal"
            modal.toggle()
            
            { state with FetchRoast = Resolved(Ok { roast with SentRemindersCount = roast.SentRemindersCount + 1 }) },
            Cmd.none
            
        | _ ->
            state, Cmd.none

let attr name value = HTMLAttr.Custom(name, value)
        
let statusBadge status =
    let baseClasses = "badge text-uppercase py-2 fw-light fs-6 float-end mt-1"
    
    match status with
    | NotPublished ->
        span [ Class $"{baseClasses} bg-secondary" ] [ str "Not Published" ]
    | Open ->
        span [ Class $"{baseClasses} bg-success" ] [ str "Open" ]
    | Closed ->
        span [ Class $"{baseClasses} bg-danger" ] [ str "Closed" ]

let getCoffeeName (coffees: EvansFreshRoast.Dto.Coffee list) coffeeId =
    coffees
    |> List.tryFind (fun c -> c.Id = coffeeId)
    |> Option.map (fun c -> c.Name)
    |> Option.defaultValue "???"
    
let getCoffeePrice (coffees: EvansFreshRoast.Dto.Coffee list) coffeeId =
    coffees
    |> List.tryFind (fun c -> c.Id = coffeeId)
    |> Option.map (fun c -> c.PricePerBag)
    |> Option.defaultValue 0m
    
let getOrderTotal (coffees: EvansFreshRoast.Dto.Coffee list) (lineItems: Map<Guid, int>) =
    lineItems
    |> Map.toList
    |> List.sumBy (fun (coffeeId, qty) ->
        let price = getCoffeePrice coffees coffeeId
        price * (decimal qty))
    
let individualOrderTable
    (customers: EvansFreshRoast.Dto.Customer list)
    (coffees: EvansFreshRoast.Dto.Coffee list)
    (dispatch: Msg -> unit)
    (order: RoastDetailsOrder)
    =
    let customerName =
        customers
        |> List.tryFind (fun c -> c.Id = order.CustomerId)
        |> Option.map (fun c -> c.Name)
        |> Option.defaultValue "???"
        
    let orderTotal = getOrderTotal coffees order.LineItems
        
    let isPaid =
        order.Invoice
        |> Option.map (fun ivc -> Option.isSome ivc.PaymentMethod)
        |> Option.defaultValue false
        
    div [ Class "card mb-3" ] [
        div [ Class "card-header" ] [
            div [ Class "d-flex justify-content-between align-items-center" ] [
                span [] [ str customerName ]
                div [ Class "border rounded py-1 px-2" ] [
                    div [ Class "form-check" ] [
                        if isPaid then
                            input [
                                Class "form-check-input"
                                Props.Type "checkbox"
                                Checked true
                                ReadOnly true
                            ]
                        else
                            input [
                                Class "form-check-input"
                                Props.Type "checkbox"
                                OnChange(fun _ -> dispatch <| UpdatePayOrderModalCustomerId order.CustomerId)
                                attr "data-bs-toggle" "modal"
                                attr "data-bs-target" "#confirmInvoicePaidModal"
                            ]
                        label [ Class "form-check-label" ] [ str "Paid" ]
                    ]
                ]
            ]
        ]
        div [ Class "card-body" ] [
            table [ Class "table align-middle table-sm mb-0" ] [
                thead [] [
                    tr [] [
                        th [ Scope "col" ] [ str "Coffee" ]
                        th [ Scope "col"; Class "text-end" ] [ str "Price" ]
                        th [ Scope "col"; Class "text-end" ] [ str "Qty" ]
                        th [ Scope "col"; Class "text-end" ] [ str "Subtotal" ]
                    ]
                ]
                tbody [] [
                    fragment [] (Map.toList order.LineItems |> List.map (fun (coffeeId, qty) ->
                        let name = getCoffeeName coffees coffeeId
                        let price = getCoffeePrice coffees coffeeId
                        let subtotal = price * (decimal qty)
                        
                        tr [] [
                            td [] [ str name ]
                            td [ Class "text-end" ] [ str $"""${price.ToString("0.00")}""" ]
                            td [ Class "text-end" ] [ str <| string qty ]
                            td [ Class "text-end" ] [ str $"""${subtotal.ToString("0.00")}""" ]
                        ]))
                    
                    tr [] [
                        td [ ColSpan 3; Class "text-end fw-bold" ] [ str "Total" ]
                        td [ Class "text-end" ] [ str $"""${orderTotal.ToString("0.00")}""" ]
                    ]
                ]
            ]
        ]
    ]
      
let confirmedOrderTotalsTable
    (coffees: EvansFreshRoast.Dto.Coffee list)
    (orders: RoastDetailsOrder list)
    =
    let merge =
        Map.fold
            (fun merged key value ->
                merged
                |> Map.change key (fun x ->
                    match x with
                    | Some q -> Some (q + value)
                    | None -> Some value))
            
    let combinedLineItems =
        orders
        |> List.map (fun o -> o.LineItems)
        |> List.fold merge Map.empty
       
    let combinedTotal = getOrderTotal coffees combinedLineItems
       
    table [ Class "table align-middle table-sm mb-0" ] [
        thead [] [
            tr [] [
                th [ Scope "col" ] [ str "Coffee" ]
                th [ Scope "col"; Class "text-end" ] [ str "Price" ]
                th [ Scope "col"; Class "text-end" ] [ str "Qty" ]
                th [ Scope "col"; Class "text-end" ] [ str "Subtotal" ]
            ]
        ]
        tbody [] [
            fragment [] (Map.toList combinedLineItems |> List.map (fun (coffeeId, qty) ->
                let name = getCoffeeName coffees coffeeId
                let price = getCoffeePrice coffees coffeeId
                let subtotal = price * (decimal qty)
                
                tr [] [
                    td [] [ str name ]
                    td [ Class "text-end" ] [ str $"""${price.ToString("0.00")}""" ]
                    td [ Class "text-end" ] [ str <| string qty ]
                    td [ Class "text-end" ] [ str $"""${subtotal.ToString("0.00")}""" ]
                ]))
            
            tr [] [
                td [ ColSpan 3; Class "text-end fw-bold" ] [ str "Total" ]
                td [ Class "text-end" ] [ str $"""${combinedTotal.ToString("0.00")}""" ]
            ]
        ]
    ]
        
let addCoffeesModal
    (roast: RoastDetails)
    (coffees: EvansFreshRoast.Dto.Coffee list)
    (state: State)
    (dispatch: Msg -> unit)
    =
    let activeCoffeesNotAlreadyIncludedInRoast =
        coffees
        |> List.filter (fun coffee -> coffee.IsActive)
        |> List.filter (fun coffee -> (roast.Coffees |> List.tryFind (fun rcId -> rcId = coffee.Id)) = None)
    
    div [ Class "modal fade"; Id "addCoffeesModal"; TabIndex -1 ] [
        div [ Class "modal-dialog modal-fullscreen" ] [
            div [ Class "modal-content" ] [
                div [ Class "modal-header" ] [
                    h5 [ Class "modal-title" ] [
                        str "Select Coffees"
                        span [ Class "d-block fs-6 fw-light card-subtitle" ] [ str "Only active coffees are shown" ]
                    ]
                    button [
                        Class "btn-close"
                        attr "data-bs-dismiss" "modal"
                        OnClick(fun _ -> dispatch SelectedCoffeesCleared)
                    ] []
                ]
                div [ Class "modal-body" ] [
                    match activeCoffeesNotAlreadyIncludedInRoast with
                    | [] ->
                        p [] [ str "There are no active coffees to add to this roast." ]
                        
                    | selectableCoffees ->
                        fragment []  (selectableCoffees |> List.map (fun coffee ->
                            div [
                                if state.SelectedCoffees |> Set.contains coffee.Id then
                                    Class "card mb-3 border-primary"
                                else
                                    Class "card mb-3"
                                OnClick(fun _ -> dispatch <| SelectCoffeeToggled coffee.Id)
                            ] [
                                div [ Class "card-body" ] [
                                    div [ Class "d-flex align-items-center" ] [
                                        input [
                                            Props.Type "checkbox"
                                            Class "flex-shrink-0 form-check-input"
                                            Checked (state.SelectedCoffees |> Set.contains coffee.Id)
                                        ]
                                        div [ Class "card-right ps-3" ] [
                                            h6 [ Class "fw-bold" ] [ str coffee.Name ]
                                            p [ Class "mb-1" ] [ str coffee.Description ]
                                            p [ Class "text-muted mb-0" ] [
                                                str $"""${coffee.PricePerBag.ToString("0.00")} / {coffee.WeightPerBag.ToString("0.0")} oz"""
                                            ]
                                        ]
                                    ]
                                ]
                            ]))
                ]
                    
                div [ Class "modal-footer" ] [
                    if activeCoffeesNotAlreadyIncludedInRoast.Length > 0 then
                        fragment [] [
                            button [
                                Class "btn btn-secondary"
                                if Deferred.isInProgress state.AddCoffees then
                                    Class "btn btn-secondary disabled"
                                    Disabled true
                                attr "data-bs-dismiss" "modal"
                                OnClick(fun _ -> dispatch SelectedCoffeesCleared)
                            ] [
                                str "Cancel"
                            ]
                            button [
                                Class "btn btn-primary"
                                if Deferred.isInProgress state.AddCoffees then
                                    Class "btn btn-primary disabled"
                                    Disabled true
                                OnClick(fun _ -> dispatch <| AddCoffees Started)
                            ] [
                                if Deferred.isInProgress state.AddCoffees then
                                    span [ Class "spinner-grow spinner-grow-sm" ] []
                                    str " Loading..."
                                else
                                    str "Add Coffees To Roast"
                            ]
                        ]
                    else
                        button [
                            Class "btn btn-secondary"
                            attr "data-bs-dismiss" "modal"
                            OnClick(fun _ -> dispatch SelectedCoffeesCleared)
                        ] [ str "Close" ]
                ]
            ]
        ]
    ]
        
let addCustomersModal
    (roast: RoastDetails)
    (customers: EvansFreshRoast.Dto.Customer list)
    (state: State)
    (dispatch: Msg -> unit)
    =
    let subscribedCustomersNotAlreadyIncludedInRoast =
        customers
        |> List.filter (fun cust -> cust.Status = Subscribed)
        |> List.filter (fun cust -> (roast.Customers |> List.tryFind (fun rcId -> rcId = cust.Id)) = None)
    
    div [ Class "modal fade"; Id "addCustomersModal"; TabIndex -1 ] [
        div [ Class "modal-dialog modal-fullscreen" ] [
            div [ Class "modal-content" ] [
                div [ Class "modal-header" ] [
                    h5 [ Class "modal-title" ] [
                        str "Select Customers"
                        span [ Class "d-block fs-6  fw-light card-subtitle" ] [ str "Only subscribed customers are shown" ]
                    ]
                    button [
                        Class "btn-close"
                        attr "data-bs-dismiss" "modal"
                        OnClick(fun _ -> dispatch SelectedCustomersCleared)
                    ] []
                ]
                div [ Class "modal-body" ] [
                    match subscribedCustomersNotAlreadyIncludedInRoast with
                    | [] ->
                        p [] [ str "There are no available (subscribed) customers to add to this roast." ]
                        
                    | selectableCustomers ->
                        fragment [] [
                            button [
                                Class "mb-3 btn btn-outline-secondary"
                                OnClick(fun _ -> dispatch <| CustomersSelected(selectableCustomers |> List.map (fun c -> c.Id)))
                            ] [ str "Select All" ]
                            button [
                                Class "mb-3 ms-3 btn btn-outline-danger"
                                OnClick(fun _ -> dispatch SelectedCustomersCleared)
                            ] [ str "Clear" ]
                            fragment []  (selectableCustomers |> List.map (fun customer ->
                                div [
                                    if state.SelectedCustomers |> Set.contains customer.Id then
                                        Class "card mb-3 border-primary"
                                    else
                                        Class "card mb-3"
                                    OnClick(fun _ -> dispatch <| SelectCustomerToggled customer.Id)
                                ] [
                                    div [ Class "p-2" ] [
                                        div [ Class "d-flex align-items-center" ] [
                                            input [
                                                Props.Type "checkbox"
                                                Class "flex-shrink-0 form-check-input"
                                                Checked (state.SelectedCustomers |> Set.contains customer.Id)
                                            ]
                                            div [ Class "card-right ps-2" ] [
                                                h6 [ Class "fw-bold mb-0" ] [ str customer.Name ]
                                                p [ Class "text-muted mb-0" ] [ str customer.PhoneNumber ]
                                            ]
                                        ]
                                    ]
                                ]))
                        ]
                        
                ]
                    
                div [ Class "modal-footer" ] [
                    if subscribedCustomersNotAlreadyIncludedInRoast.Length > 0 then
                        fragment [] [
                            button [
                                Class "btn btn-secondary"
                                if Deferred.isInProgress state.AddCustomers then
                                    Class "btn btn-secondary disabled"
                                    Disabled true
                                attr "data-bs-dismiss" "modal"
                                OnClick(fun _ -> dispatch SelectedCustomersCleared)
                            ] [ str "Cancel" ]
                            button [
                                Class "btn btn-primary"
                                if Deferred.isInProgress state.AddCustomers then
                                    Class "btn btn-primary disabled"
                                    Disabled true
                                OnClick(fun _ -> dispatch <| AddCustomers Started)
                            ] [
                                if Deferred.isInProgress state.AddCustomers then
                                    span [ Class "spinner-grow spinner-grow-sm" ] []
                                    str " Loading..."
                                else
                                    str "Add Customers To Roast"
                            ]
                        ]
                    else
                        button [
                            Class "btn btn-secondary"
                            attr "data-bs-dismiss" "modal"
                            OnClick(fun _ -> dispatch SelectedCustomersCleared)
                        ] [ str "Close" ]
                ]
            ]
        ]
    ]
        
let confirmOpenRoastModal (state: State) (dispatch: Msg -> unit) =
    div [ Class "modal fade"; Id "confirmOpenRoastModal"; TabIndex -1 ] [
        div [ Class "modal-dialog modal-dialog-centered" ] [
            div [ Class "modal-content" ] [
                div [ Class "modal-header" ] [
                    h5 [ Class "modal-title" ] [ str "Open Roast" ]
                    button [
                        Class "btn-close"
                        attr "data-bs-dismiss" "modal"
                    ] []
                ]
                div [ Class "modal-body" ] [
                    p [] [
                        str "Opening this roast will send a text notification to all subscribed customers. "
                        str "It will prevent you from making further changes to the customers or coffees that are part of this roast. "
                        str "Are you sure you want to do this right now? "
                        str "Please confirm or cancel below."
                    ]
                ]
                div [ Class "modal-footer" ] [
                    button [
                        Class "btn btn-secondary"
                        if Deferred.isInProgress state.OpenRoast then
                            Class "btn btn-secondary disabled"
                            Disabled true
                        attr "data-bs-dismiss" "modal"
                    ] [ str "Cancel" ]
                    button [
                        Class "btn btn-primary"
                        if Deferred.isInProgress state.OpenRoast then
                            Class "btn btn-primary disabled"
                            Disabled true
                        OnClick(fun _ -> dispatch <| OpenRoast Started)
                    ] [
                        if Deferred.isInProgress state.OpenRoast then
                            span [ Class "spinner-grow spinner-grow-sm" ] []
                            str " Loading..."
                        else
                            str "Open Roast"
                    ]
                ]
            ]
        ]
    ]

let confirmCloseRoastModal (state: State) (dispatch: Msg -> unit) =
    div [ Class "modal fade"; Id "confirmCloseRoastModal"; TabIndex -1 ] [
        div [ Class "modal-dialog modal-dialog-centered" ] [
            div [ Class "modal-content" ] [
                div [ Class "modal-header" ] [
                    h5 [ Class "modal-title" ] [ str "Close Roast" ]
                    button [
                        Class "btn-close"
                        attr "data-bs-dismiss" "modal"
                    ] []
                ]
                div [ Class "modal-body" ] [
                    p [] [
                        str "Closing this roast will send a text notification to all subscribed customers. "
                        str "It will prevent any customers from placing further orders and will notify "
                        str "them that their coffee is ready for pickup. "
                        str "Are you sure you want to do this right now? "
                        str "Please confirm or cancel below."
                    ]
                ]
                div [ Class "modal-footer" ] [
                    button [
                        Class "btn btn-secondary"
                        if Deferred.isInProgress state.CloseRoast then
                            Class "btn btn-secondary disabled"
                            Disabled true
                        attr "data-bs-dismiss" "modal"
                    ] [ str "Cancel" ]
                    button [
                        Class "btn btn-primary"
                        if Deferred.isInProgress state.CloseRoast then
                            Class "btn btn-primary disabled"
                            Disabled true
                        OnClick(fun _ -> dispatch <| CloseRoast Started)
                    ] [
                        if Deferred.isInProgress state.CloseRoast then
                            span [ Class "spinner-grow spinner-grow-sm" ] []
                            str " Loading..."
                        else
                            str "Close Roast"
                    ]
                ]
            ]
        ]
    ]
   
let confirmInvoicePaidModal (state: State) (dispatch: Msg -> unit) =
    div [ Class "modal fade"; Id "confirmInvoicePaidModal"; TabIndex -1 ] [
        div [ Class "modal-dialog modal-dialog-centered" ] [
            div [ Class "modal-content" ] [
                div [ Class "modal-header" ] [
                    h5 [ Class "modal-title" ] [ str "Mark Order Paid" ]
                    button [
                        Class "btn-close"
                        attr "data-bs-dismiss" "modal"
                    ] []
                ]
                div [ Class "modal-body" ] [
                    p [] [
                        str "Are you sure you want to mark this order paid? "
                        str "This action cannot be undone. "
                        str "The customer will not be notified. "
                    ]
                ]
                div [ Class "modal-footer" ] [
                    button [
                        Class "btn btn-secondary"
                        if Deferred.isInProgress state.PayOrder then
                            Class "btn btn-secondary disabled"
                            Disabled true
                        attr "data-bs-dismiss" "modal"
                    ] [ str "Cancel" ]
                    button [
                        Class "btn btn-primary"
                        if Deferred.isInProgress state.PayOrder then
                            Class "btn btn-primary disabled"
                            Disabled true
                        OnClick(fun _ -> dispatch <| PayOrder(state.PayOrderModalCustomerId, Started))
                    ] [
                        if Deferred.isInProgress state.PayOrder then
                            span [ Class "spinner-grow spinner-grow-sm" ] []
                            str " Loading..."
                        else
                            str "Mark Order Paid"
                    ]
                ]
            ]
        ]
    ]
        
let confirmSendFollowUpModal (roast: RoastDetails) (state: State) (dispatch: Msg -> unit) =
    div [ Class "modal fade"; Id "confirmSendFollowUpModal"; TabIndex -1 ] [
        div [ Class "modal-dialog modal-dialog-centered" ] [
            div [ Class "modal-content" ] [
                div [ Class "modal-header" ] [
                    h5 [ Class "modal-title" ] [ str "Send Follow-Up Reminder" ]
                    button [
                        Class "btn-close"
                        attr "data-bs-dismiss" "modal"
                    ] []
                ]
                div [ Class "modal-body" ] [
                    p [] [
                        str "Sending a follow-up reminder will only send a text notification to customers "
                        str "of this roast who do not yet have a "
                        em [] [ str "confirmed "  ]
                        str "order. You have already sent "
                        strong [] [ str $"{roast.SentRemindersCount} " ]
                        str "reminder(s). "
                        br []
                        br []
                        str "Are you sure you want to send a follow-up reminder?"
                    ]
                ]
                div [ Class "modal-footer" ] [
                    button [
                        Class "btn btn-secondary"
                        if Deferred.isInProgress state.SendFollowUp then
                            Class "btn btn-secondary disabled"
                            Disabled true
                        attr "data-bs-dismiss" "modal"
                    ] [ str "Cancel" ]
                    button [
                        Class "btn btn-primary"
                        if Deferred.isInProgress state.SendFollowUp then
                            Class "btn btn-primary disabled"
                            Disabled true
                        OnClick(fun _ -> dispatch <| SendFollowUp Started)
                    ] [
                        if Deferred.isInProgress state.SendFollowUp then
                            span [ Class "spinner-grow spinner-grow-sm" ] []
                            str " Loading..."
                        else
                            str "Send Follow-Up"
                    ]
                ]
            ]
        ]
    ]
        
let view (state: State) (dispatch: Msg -> unit) =
    let isActiveTab tab = tab = state.Tab
    
    match state.FetchRoast, state.FetchCoffees, state.FetchCustomers with
    | Resolved(Ok roast), Resolved(Ok coffees), Resolved(Ok customers) ->
        fragment [] [
            h1 [ Class "mt-3" ] [
                str $"""{roast.RoastDate.Format("MMM dd, yyyy")}"""
                statusBadge roast.Status
            ]
            p [ Class "text-muted" ] [ str $"""Order by {roast.OrderByDate.Format("MMM dd, yyyy")}""" ]
            ul [ Class "nav nav-tabs mb-3" ] (Tab.allTabs |> List.map (fun tab ->
                li [ Class "nav-item" ] [
                     button [
                        if isActiveTab tab then
                            Class "nav-link active"
                        else
                            Class "nav-link"
                        OnClick(fun _ -> dispatch <| TabSelected tab)
                    ] [ str <| string tab ]
                ]
            ))
            match state.Tab with
            | Orders ->
                div [ Class "d-flex justify-content-between align-items-center mb-3" ] [
                    h3 [ Class "my-0" ] [ str "Orders" ]
                    match roast.Status with
                    | NotPublished ->
                        button [
                            Class "btn btn-primary"
                            attr "data-bs-toggle" "modal"
                            attr "data-bs-target" "#confirmOpenRoastModal"
                        ] [ str "Open Roast" ]
                        
                    | Open ->
                        button  [
                            Class "btn btn-outline-primary"
                            attr "data-bs-toggle" "modal"
                            attr "data-bs-target" "#confirmSendFollowUpModal"
                        ] [ str "Send Follow-Up" ]
                        button [
                            Class "btn btn-danger"
                            attr "data-bs-toggle" "modal"
                            attr "data-bs-target" "#confirmCloseRoastModal"
                        ] [ str "Close Roast" ]
                        
                    | _ ->
                        fragment [] []
                ]
                
                match roast.Orders with
                | [] ->
                    p [] [
                        str "Waiting on a customer to place the first order for this roast. "
                        str "Nothing to see here just yet. "
                    ]
                    
                | orders ->
                    let confirmedOrders =
                        orders |> List.filter (fun o -> Option.isSome o.Invoice)
                        
                    let unconfirmedOrders =
                        orders |> List.filter (fun o -> Option.isNone o.Invoice)
                        
                    let unpaidOrders =
                        confirmedOrders
                        |> List.filter (fun co -> Option.isNone co.Invoice.Value.PaymentMethod)
                    
                    fragment [] [
                        h4 [] [ str "Roast Totals" ]
                        confirmedOrderTotalsTable coffees confirmedOrders
                        ul [ Class "mt-2" ] [
                            li [] [
                                str $"{unconfirmedOrders.Length} "
                                span [ Class "fw-bold" ] [ str "unconfirmed " ]
                                str "orders."
                            ]
                            li [] [
                                str $"{unpaidOrders.Length} "
                                span [ Class "fw-bold" ] [ str "unpaid " ]
                                str "orders."
                            ]
                        ]
                        
                        h4 [ Class "mt-4" ] [ str "Customer Order Details" ]
                        fragment [] (confirmedOrders |> List.map (individualOrderTable customers coffees dispatch))
                    ]
                
            | Coffees ->
                div [ Class "d-flex justify-content-between align-items-center mb-3" ] [
                    h3 [ Class "my-0" ] [ str "Coffees" ]
                    button [
                        Class "btn btn-primary"
                        if roast.Status <> NotPublished then
                            Class "btn btn-primary disabled"
                            Disabled true
                        attr "data-bs-toggle" "modal"
                        attr "data-bs-target" "#addCoffeesModal"
                    ] [ str "Add Coffees" ]
                ]
                
                match roast.Coffees with
                | [] ->
                    p [] [
                        str "No coffees are offered as a part of this roast yet."
                    ]
                    
                | roastCoffeeIds ->
                    let roastCoffees =
                        coffees
                        |> List.filter (fun c -> roastCoffeeIds |> Seq.exists ((=) c.Id))
                    
                    fragment [] (roastCoffees |> List.map (fun coffee ->
                        div [ Class "card mb-3" ] [
                            div [ Class "card-body" ] [
                                h6 [ Class "fw-bold" ] [ str coffee.Name ]
                                div [ Class "d-flex align-items-center" ] [
                                    p [ Class "flex-grow-1 mb-0" ] [ str coffee.Description ]
                                    p [ Class "mb-0 text-center text-muted fw-bold"; Style [ MinWidth "75px" ] ] [
                                        str $"""${coffee.PricePerBag.ToString("0.00")}"""
                                        br []
                                        str  $"""{coffee.WeightPerBag.ToString("0.0")} oz"""
                                    ]
                                ]
                            ]
                        ]))
                
            | Customers ->
                div [ Class "d-flex justify-content-between align-items-center mb-3" ] [
                    h3 [ Class "my-0" ] [ str "Customers" ]
                    button [
                        Class "btn btn-primary"
                        if roast.Status <> NotPublished then
                            Class "btn btn-primary disabled"
                            Disabled true
                        attr "data-bs-toggle" "modal"
                        attr "data-bs-target" "#addCustomersModal"
                    ] [ str "Add Customers" ]
                ]
                
                match roast.Customers with
                | [] ->
                    p [] [
                        str "No customers will be notified about this roast yet."
                    ]
                    
                | roastCustomerIds ->
                    let roastCustomers =
                        customers
                        |> List.filter (fun cust -> roastCustomerIds |> Seq.exists (fun rcId -> rcId = cust.Id))
                    
                    fragment [] (roastCustomers |> List.map (fun customer ->
                        div [ Class "card mb-2" ] [
                            div [ Class "p-2" ] [
                                h6 [ Class "mb-0" ] [ str customer.Name ]
                                p [ Class "text-muted mb-0" ] [ str customer.PhoneNumber ]
                            ]
                        ]))
                
            addCoffeesModal roast coffees state dispatch
            addCustomersModal roast customers state dispatch
            confirmOpenRoastModal state dispatch
            confirmCloseRoastModal state dispatch
            confirmInvoicePaidModal state dispatch
            confirmSendFollowUpModal roast state dispatch
        ]
        
    | _ ->
        div [
            Class "h-fullpage position-relative d-flex justify-content-center align-items-center"
        ] [
            div [ Class "spinner-grow text-primary" ] [
                span [ Class "visually-hidden" ] [ str "Loading..." ]
            ]
        ]
