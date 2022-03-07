module State

open System
open EvansFreshRoast.Domain
open EvansFreshRoast.Framework
open EvansFreshRoast.Dto
open EvansFreshRoast.Serialization
open EvansFreshRoast.Serialization.Roast
open EvansFreshRoast.Serialization.Coffee
open EvansFreshRoast.Serialization.Customer
open Types
open Routes
open Elmish
open Fable.SignalR
open Fable.SignalR.Elmish
open Thoth.Json
open AsyncHelpers

let decodeEvent: Decoder<Event> =
    Decode.oneOf
        [ Decode.map RoastEvent (DomainEvents.decodeDomainEvent decodeRoastEvent)
          Decode.map CoffeeEvent (DomainEvents.decodeDomainEvent decodeCoffeeEvent)
          Decode.map CustomerEvent (DomainEvents.decodeDomainEvent decodeCustomerEvent) ]

type Page =
    | NotFound
    | Login of Pages.Login.State
    | VerifyOtp of Pages.VerifyOtp.State
    | Roasts of Pages.Roasts.State
    | NewRoast of Pages.NewRoast.State
    | Roast of Pages.Roast.State
    | NewCoffee of Pages.NewCoffee.State
    | Coffee of Pages.Coffee.State
    | Coffees of Pages.Coffees.State
    | NewCustomer of Pages.NewCustomer.State
    | Customers of Pages.Customers.State

type State =
    { Session: Session option
      OtpToken: OtpToken option
      CurrentPage: Page
      Hub: Elmish.Hub<unit, string> option }

type Msg =
    | Noop
    | MainNavItemSelected of Route
    | SignedOut
    | SessionRefresh of AsyncOperationEvt<Result<Session, string>>
    | SignalRHubRegistered of Elmish.Hub<unit, string>
    | SignalRMessageReceived of string
    | LoginMsg of Pages.Login.Msg
    | VerifyOtpMsg of Pages.VerifyOtp.Msg
    | RoastsMsg of Pages.Roasts.Msg
    | NewRoastMsg of Pages.NewRoast.Msg
    | RoastMsg of Pages.Roast.Msg
    | NewCoffeeMsg of Pages.NewCoffee.Msg
    | CoffeeMsg of Pages.Coffee.Msg
    | CoffeesMsg of Pages.Coffees.Msg
    | NewCustomerMsg of Pages.NewCustomer.Msg
    | CustomersMsg of Pages.Customers.Msg

let getRefreshSessionCmd session =
    async {
        let timeSpanUntilExpiration =
            session.Expires.Subtract(DateTimeOffset.Now.AddSeconds(10))
                
        if timeSpanUntilExpiration.TotalMilliseconds > 0 then
            do! Async.Sleep(timeSpanUntilExpiration.Subtract(TimeSpan.FromSeconds(5)))
            return SessionRefresh Started
        else
            return SignedOut
    }
    |> Cmd.OfAsync.result

let getSaveSessionCmd session =
    async {
        Browser.WebStorage.localStorage.setItem(
            "efr.session",
            Encode.toString 2 (Session.encode session))

        return Noop
    }
    |> Cmd.OfAsync.result

let getSignalRConnectCmd session =
    Cmd.SignalR.connect SignalRHubRegistered <| fun hub ->
        hub.withUrl("/api/v1/ws/domain-events")
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Trace)
            .onMessage SignalRMessageReceived

// TODO: move to separate file
let setCurrentPage maybeRoute state =
    match state.Session, maybeRoute with
        | None, None ->
            state, Route.navigateTo Route.Login

        | _, Some Route.Login ->
            let loginState, loginCmd = Pages.Login.init()

            { state with CurrentPage = Login loginState},
            loginCmd |> Cmd.map LoginMsg
        
        | _, Some Route.VerifyOtp ->
            match state.OtpToken with
            | Some _ ->
                let verifyOtpState, verifyOtpCmd = Pages.VerifyOtp.init()

                { state with CurrentPage = VerifyOtp verifyOtpState },
                verifyOtpCmd |> Cmd.map VerifyOtpMsg

            | None ->
                state, Route.navigateTo Route.Login

        | None, _ ->
            state, Route.navigateTo Route.Login

        | Some _, Some Route.Roasts ->
            let roastsState, roastsCmd = Pages.Roasts.init()

            { state with CurrentPage = Roasts roastsState },
            roastsCmd |> Cmd.map RoastsMsg

        | Some _, Some Route.NewCoffee ->
            let st, cmd = Pages.NewCoffee.init()

            { state with CurrentPage = NewCoffee st },
            cmd |> Cmd.map NewCoffeeMsg

        | Some _, Some(Route.Coffee id) ->
            let st, cmd = Pages.Coffee.init id
            
            { state with CurrentPage = Coffee st },
            cmd |> Cmd.map CoffeeMsg
            
        | Some _, Some Route.Coffees ->
            let st, cmd = Pages.Coffees.init()
            
            { state with CurrentPage = Coffees st },
            cmd |> Cmd.map CoffeesMsg
            
        | Some _, Some Route.NewCustomer ->
            let st, cmd = Pages.NewCustomer.init()
            
            { state with CurrentPage = NewCustomer st },
            cmd |> Cmd.map NewCustomerMsg
        
        | Some _, Some Route.Customers ->
            let st, cmd = Pages.Customers.init()
            
            { state with CurrentPage = Customers st },
            cmd |> Cmd.map CustomersMsg
            
        | Some _, Some Route.NewRoast ->
            let st, cmd = Pages.NewRoast.init()
            
            { state with CurrentPage = NewRoast st },
            cmd |> Cmd.map NewRoastMsg
            
        | Some _, Some(Route.Roast id) ->
            let st, cmd = Pages.Roast.init id
            
            { state with CurrentPage = Roast st },
            cmd |> Cmd.map RoastMsg
        
        | _ ->
            { state with CurrentPage = NotFound }, Cmd.none

let init maybeSession maybeRoute =
    let state, initCmd =
        { Session = maybeSession
          OtpToken = None
          CurrentPage = NotFound
          Hub = None }
        |> setCurrentPage maybeRoute
    
    let signalRConnectCmd =
        maybeSession
        |> Option.map getSignalRConnectCmd
        |> Option.defaultValue Cmd.none
        
    let refreshSessionCmd =
        maybeSession
        |> Option.map getRefreshSessionCmd
        |> Option.defaultValue Cmd.none

    state, Cmd.batch [ initCmd
                       signalRConnectCmd
                       refreshSessionCmd ]

let update (msg: Msg) (state: State) =
    match msg with
    | Noop ->
        state, Cmd.none

    | MainNavItemSelected route ->
        state, Route.navigateTo route
    
    | SignedOut ->
         Browser.WebStorage.localStorage.removeItem("efr.session")
         state, Route.navigateTo Route.Login
    
    | SessionRefresh Started ->
        let cmd =
            async {
                match! Api.refreshToken with
                | Ok newExpirationDt ->
                    match state.Session with
                    | Some sess ->
                        return Ok { sess with Expires = newExpirationDt }
                        
                    | None ->
                        return Error "No existing session."
                        
                | Error e ->
                    return Error $"Error refreshing token: {e}"
            }
            |> Cmd.OfAsync.result
            |> Cmd.map (SessionRefresh << Finished)
            
        state, cmd

    | SessionRefresh(Finished(Ok session)) ->
        let refreshSessionCmd = getRefreshSessionCmd session
        let saveSessionCmd = getSaveSessionCmd session
            
        state, Cmd.batch [ refreshSessionCmd
                           saveSessionCmd ]
    
    | SessionRefresh(Finished(Error e)) ->
        Browser.Dom.console.error(e)
        state, Cmd.none
    
    | SignalRHubRegistered hub ->
        match state.Hub with
        | Some _ ->
            state, Cmd.none
            
        | None ->
            { state with Hub = Some hub }, Cmd.none

    // TODO: this should be a separate file & function
    | SignalRMessageReceived encoded ->
        match Decode.fromString decodeEvent encoded with
        | Ok evt ->
            match evt with
            | CoffeeEvent { AggregateId = coffeeId; Body = coffeeEvt } ->
                match coffeeEvt with
                | Coffee.Created _ ->
                    match state.CurrentPage with
                    | NewCoffee newCoffeeState ->
                        let newSt, cmd =
                            Pages.NewCoffee.update
                                (Pages.NewCoffee.Msg.CoffeeCreated <| Id.value coffeeId)
                                newCoffeeState
                        
                        { state with CurrentPage = NewCoffee newSt },
                        Cmd.map NewCoffeeMsg cmd
                        
                    | _ -> state, Cmd.none
                    
                | _ -> state, Cmd.none
            
            | CustomerEvent { AggregateId = customerId; Body = customerEvt } ->
                printfn $"%A{customerId}"
                
                match customerEvt with
                | Customer.Created customer ->
                    match state.CurrentPage with
                    | NewCustomer newCustomerState ->
                        let newSt, cmd =
                            Pages.NewCustomer.update
                                (Pages.NewCustomer.Msg.CustomerCreated <| Id.value customerId)
                                newCustomerState
                                
                        { state with CurrentPage = NewCustomer newSt },
                        Cmd.map NewCustomerMsg cmd
                        
                    | Customers customersState ->
                        let customer =
                            { Id = Id.value customerId
                              Name = CustomerName.value customer.Name
                              PhoneNumber = UsPhoneNumber.value customer.PhoneNumber
                              Status = Unconfirmed }
                        
                        let newSt, cmd =
                            Pages.Customers.update
                                (Pages.Customers.Msg.CustomerAdded customer)
                                customersState
                                
                        { state with CurrentPage = Customers newSt },
                        Cmd.map CustomersMsg cmd
                        
                    | _ -> state, Cmd.none
                
                | Customer.Subscribed _ ->
                    match state.CurrentPage with
                    | Customers customerState ->
                        let newSt, cmd =
                            Pages.Customers.update
                                (Pages.Customers.Msg.CustomerStatusChanged(Id.value customerId, Subscribed))
                                customerState
                                
                        { state with CurrentPage = Customers newSt },
                        Cmd.map CustomersMsg cmd
                        
                    | _ -> state, Cmd.none
                   
                | Customer.Unsubscribed _ ->
                    match state.CurrentPage with
                    | Customers customerState ->
                        let newSt, cmd =
                            Pages.Customers.update
                                (Pages.Customers.Msg.CustomerStatusChanged(Id.value customerId, Unsubscribed))
                                customerState
                                
                        { state with CurrentPage = Customers newSt },
                        Cmd.map CustomersMsg cmd
                        
                    | _ -> state, Cmd.none
                   
                | _ -> state, Cmd.none
            
            | RoastEvent { AggregateId = roastId; Body = roastEvt } ->
                let applyRoastPageEvent msg =
                    match state.CurrentPage with
                    | Roast roastState ->
                        let newSt, cmd = Pages.Roast.update msg roastState
                        
                        { state with CurrentPage = Roast newSt },
                        Cmd.map RoastMsg cmd
                        
                    | _ -> state, Cmd.none
                
                match roastEvt with
                | Roast.Created _ ->
                    match state.CurrentPage with
                    | NewRoast newRoastState ->
                        let newSt, cmd =
                            Pages.NewRoast.update
                                (Pages.NewRoast.Msg.RoastCreated <| Id.value roastId)
                                newRoastState
                                
                        { state with CurrentPage = NewRoast newSt },
                        Cmd.map NewRoastMsg cmd
                        
                    | _ -> state, Cmd.none
                    
                | Roast.CoffeesAdded coffeeIds ->
                    applyRoastPageEvent <| Pages.Roast.Msg.CoffeesAdded(Id.value roastId, coffeeIds |> List.map Id.value)
                    
                | Roast.CustomersAdded customerIds ->
                    applyRoastPageEvent <| Pages.Roast.Msg.CustomersAdded(Id.value roastId, customerIds |> List.map Id.value)
                    
                | Roast.RoastStarted _ ->
                    applyRoastPageEvent <| Pages.Roast.Msg.RoastOpened(Id.value roastId)
                    
                | Roast.RoastCompleted ->
                    applyRoastPageEvent <| Pages.Roast.Msg.RoastClosed(Id.value roastId)
                    
                | Roast.InvoicePaid(customerId, _) ->
                    applyRoastPageEvent <| Pages.Roast.Msg.OrderPaid(Id.value customerId)
                    
                | Roast.ReminderSent ->
                    applyRoastPageEvent <| Pages.Roast.Msg.FollowUpSent(Id.value roastId)
                    
                | _ -> state, Cmd.none
            
        | Error _ ->
            state, Cmd.none

    | LoginMsg loginMsg ->
        match state.CurrentPage with
        | Login loginState ->
            let newLoginState, loginCmd, globalMsg = Pages.Login.update loginMsg loginState

            let otpToken, routeCmd =
                match globalMsg with
                | Pages.Login.Noop ->
                    None, Cmd.none

                | Pages.Login.LoginTokenReceived token ->
                    Some token, Route.navigateTo Route.VerifyOtp

            { state with
                CurrentPage = Login newLoginState
                OtpToken = otpToken },
            Cmd.batch
                [ loginCmd |> Cmd.map LoginMsg 
                  routeCmd ]

        | _ ->
            state, Cmd.none

    | VerifyOtpMsg verifyOtpMsg ->
        match state.CurrentPage with
        | VerifyOtp verifyOtpState ->
            let newVerifyOtpState, verifyOtpCmd, globalMsg =
                Pages.VerifyOtp.update state.OtpToken verifyOtpMsg verifyOtpState

            let session, routeCmd =
                match globalMsg with
                | Pages.VerifyOtp.Noop ->
                    None, Cmd.none

                | Pages.VerifyOtp.LoggedIn session ->
                    let refreshSessionCmd = getRefreshSessionCmd session
                    let signalRConnectCmd = getSignalRConnectCmd session
                    let saveSessionCmd = getSaveSessionCmd session

                    Some session,
                    Cmd.batch
                        [ Route.navigateTo Route.Roasts
                          saveSessionCmd
                          refreshSessionCmd
                          signalRConnectCmd ]

            { state with
                CurrentPage = VerifyOtp newVerifyOtpState
                Session = session
                OtpToken =
                    if Option.isSome session then
                        None
                    else
                        state.OtpToken },
            Cmd.batch
                [ verifyOtpCmd |> Cmd.map VerifyOtpMsg
                  routeCmd ]

        | _ -> state, Cmd.none

    | RoastsMsg roastsMsg ->
        match state.CurrentPage with
        | Roasts roastsState ->
            let newState, cmd = Pages.Roasts.update roastsMsg roastsState

            { state with CurrentPage = Roasts newState },
            Cmd.map RoastsMsg cmd

        | _ -> state, Cmd.none

    | RoastMsg roastMsg ->
        match state.CurrentPage with
        | Roast roastState ->
            let newState, cmd = Pages.Roast.update roastMsg roastState

            { state with CurrentPage = Roast newState },
            Cmd.map RoastMsg cmd

        | _ -> state, Cmd.none
    
    | NewRoastMsg newRoastMsg ->
        match state.CurrentPage with
        | NewRoast newRoastState ->
            let newState, cmd = Pages.NewRoast.update newRoastMsg newRoastState

            { state with CurrentPage = NewRoast newState },
            Cmd.map NewRoastMsg cmd

        | _ -> state, Cmd.none
    
    | NewCoffeeMsg msg ->
        match state.CurrentPage with
        | NewCoffee st ->
            let newSt, cmd = Pages.NewCoffee.update msg st

            { state with CurrentPage = NewCoffee newSt },
            Cmd.map NewCoffeeMsg cmd

        | _ -> state, Cmd.none
        
    | CoffeeMsg msg ->
        match state.CurrentPage with
        | Coffee st ->
            let newSt, cmd = Pages.Coffee.update msg st
            
            { state with CurrentPage = Coffee newSt },
            Cmd.map CoffeeMsg cmd
            
        | _ -> state, Cmd.none
        
    | CoffeesMsg msg ->
        match state.CurrentPage with
        | Coffees st ->
            let newSt, cmd = Pages.Coffees.update msg st
            
            { state with CurrentPage = Coffees newSt },
            Cmd.map CoffeesMsg cmd
            
        | _ -> state, Cmd.none

    | NewCustomerMsg msg ->
        match state.CurrentPage with
        | NewCustomer st ->
            let newSt, cmd = Pages.NewCustomer.update msg st
            
            { state with CurrentPage = NewCustomer newSt },
            Cmd.map NewCustomerMsg cmd
            
        | _ -> state, Cmd.none

    | CustomersMsg msg ->
        match state.CurrentPage with
        | Customers st ->
            let newSt, cmd = Pages.Customers.update msg st
            
            { state with CurrentPage = Customers newSt },
            Cmd.map CustomersMsg cmd
            
        | _ -> state, Cmd.none
        