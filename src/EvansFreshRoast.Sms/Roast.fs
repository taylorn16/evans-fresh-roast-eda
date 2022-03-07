module EvansFreshRoast.Sms.Roast

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils
open EvansFreshRoast.ReadModels
open System.Collections.Generic

let rec join separator strs =
    match strs with
    | [] -> ""
    | [s] -> s
    | head::tail ->
        head + separator + join separator tail

let getOrderReivew (allCoffees: list<Id<Coffee> * Coffee>) (lineItems: IDictionary<Id<Coffee>, Quantity>) =
    lineItems
    |> Seq.map (fun kvp ->
        let coffee = allCoffees |> List.find (fst >> (=) kvp.Key) |> snd
        let qty = Quantity.value kvp.Value
        let name = CoffeeName.value coffee.Name
        let price = $"${(UsdPrice.value coffee.PricePerBag):N2}"

        $"{qty} x {name} @ {price} ea."
    )
    |> List.ofSeq
    |> join "\n"

let handleEvent
    (sendSms: UsPhoneNumber -> SmsMsg -> Async<Result<unit, exn>>)
    (getAllCoffees: unit -> Async<list<Id<Coffee> * Coffee>>)
    (getRoast: Id<Roast> -> Async<option<RoastDetailedView>>)
    (getAllCustomers: unit -> Async<list<Id<Customer> * Customer>>)
    (getCustomer: Id<Customer> -> Async<option<Id<Customer> * Customer>>)
    (event: DomainEvent<Roast, Event>)
    =
    match event.Body with
    | RoastStarted coffeesSummary ->
        async {
            match! getRoast event.AggregateId with
            | None ->
                // TODO: add error logging around "never happen" scenarios
                // This should never happen
                return Ok ()

            | Some roast ->
                let! allCustomers = getAllCustomers()

                let subscribedRoastCustomers =
                    allCustomers
                    |> List.choose (fun (cId, cust) ->
                        let isCustomerInRoast = roast.Customers |> List.exists ((=) cId)
                        
                        match cust.Status, isCustomerInRoast with
                        | Subscribed, true -> Some cust
                        | _ -> None)

                let roastDate = roast.RoastDate.ToString("dddd, MMMM dd", null)
                let orderByDate = roast.OrderByDate.ToString("dddd, MMMM dd", null)

                let getMsg name =
                    $"Hey, {CustomerName.value name}! Evan is roasting on {roastDate}. "
                    + "Here are the coffees on tap:\n\n"
                    + coffeesSummary + "\n"
                    + "To place an order, reply with each item on a new line in the format [qty][id], "
                    + "e.g., '2A' on the first line and '4C' on the next line.\n\n"
                    + $"You have until {orderByDate} to place your order."
                    |> SmsMsg.create
                    |> unsafeAssertOk

                return!
                    subscribedRoastCustomers
                    |> List.map (fun c -> sendSms c.PhoneNumber (getMsg c.Name))
                    |> Async.Parallel
                    |> Async.map (
                        (Result.sequence Seq.head)
                        >> Result.map (fun _ -> ())
                    )
                    // TODO: this block is repetitive
        }
    
    | OrderPlaced { CustomerId = customerId; LineItems = lineItems } ->
        async {
            let! allCoffees = getAllCoffees()
            match! getCustomer customerId with
            | Some(_, customer) ->
                let orderReview = getOrderReivew allCoffees lineItems

                return!
                    $"Thanks, {CustomerName.value customer.Name}! Here's what we got:\n\n"
                    + $"{orderReview}\n\n"
                    + "If that looks correct to you, please reply 'Confirm Order'. "
                    + "Your order is not final until is confirmed.\n\n"
                    + "If there is a problem with your order, please reply 'Cancel Order' "
                    + "to scratch that and try again."
                    |> SmsMsg.create
                    |> unsafeAssertOk
                    |> sendSms customer.PhoneNumber

            | None ->
                // This should never happen!
                return Ok ()
        }

    | OrderConfirmed(customerId, invoiceAmt) ->
        async {
            match! getCustomer customerId with
            | Some(_, customer) ->
                let name = CustomerName.value customer.Name
                let totalPrice = $"${(UsdInvoiceAmount.value invoiceAmt):N2}"

                return!
                    $"Alright, {name}! Your order is confirmed. The total for your order is {totalPrice}."
                    |> SmsMsg.create
                    |> unsafeAssertOk
                    |> sendSms customer.PhoneNumber

            | None ->
                // This should never happen!
                return Ok ()
        }

    | OrderCancelled customerId ->
        async {
            match! getCustomer customerId with
            | Some(_, customer) ->
                let name = CustomerName.value customer.Name

                return!
                    $"No sweat, {name}. Your order is cancelled. "
                    + "To place a new order, reply with each item on a new line in the format [qty][id], "
                    + "e.g., 2A on the first line and 4C on the next line."
                    |> SmsMsg.create
                    |> unsafeAssertOk
                    |> sendSms customer.PhoneNumber

            | None ->
                // This should never happen!
                return Ok ()
        }

    | RoastCompleted ->
        async {
            match! getRoast event.AggregateId with
            | None ->
                // This should never happen!
                return Ok ()

            | Some roast ->
                let! allCustomers = getAllCustomers()
                let! allCoffees = getAllCoffees()
                
                let getMsg (cust: Customer) details invoice =
                    let orderReview = getOrderReivew allCoffees details.LineItems

                    let baseMsg =
                        $"Great news, {CustomerName.value cust.Name}. Your coffee is roasted and ready for pickup! "
                        + "Just in case you forgot, here's what you ordered:\n\n"
                        + $"{orderReview}"

                    let msg = 
                        match invoice with
                        | PaidInvoice _ ->
                            baseMsg
                        | UnpaidInvoice _ ->
                            baseMsg + "\n\nIf you haven't paid yet, please do so soon!"

                    msg |> SmsMsg.create |> unsafeAssertOk, cust.PhoneNumber
                

                return! roast.Orders
                |> List.choose (fun ord ->
                    match ord with
                    | ConfirmedOrder(details, invoice) ->
                        allCustomers
                        |> List.tryFind (fst >> (=) details.CustomerId)
                        |> Option.map (fun (_, cust) -> getMsg cust details invoice)
                    | _ ->
                        None)
                |> List.map (fun (msg, phn) -> sendSms phn msg)
                |> Async.Parallel
                |> Async.map (
                    (Result.sequence Seq.head)
                    >> Result.map (fun _ -> ())
                )
        }

    | ReminderSent ->
        async {
            match! getRoast event.AggregateId with
            | None ->
                // This should never happen!
                return Ok ()

            | Some roast ->
                let! allCustomers = getAllCustomers()
                let orderByDate = roast.OrderByDate.ToString("dddd, MMMM dd", null)

                let subscribedCustomersWithoutAnOrderMsgs =
                    allCustomers
                    |> List.choose (fun (cId, cust) ->
                        let isCustomerInRoast = roast.Customers |> List.exists ((=) cId)
                        let customerHasOrder = roast.Orders |> List.exists (Order.getCustomerId >> (=) cId)
                        
                        match cust.Status, isCustomerInRoast, customerHasOrder with
                        | Subscribed, true, false -> Some cust
                        | _ -> None)
                    |> List.map (fun cust ->
                        let name = CustomerName.value cust.Name

                        $"Hey, {name}- Just following up. If you'd like to place an order, "
                        + $"you have until {orderByDate}. Thanks!"
                        , cust.PhoneNumber)

                let customersWithUnconfirmedOrderMsgs =
                    roast.Orders
                    |> List.choose (fun ord ->
                        match ord with
                        | UnconfirmedOrder details ->
                            allCustomers
                            |> List.tryFind (fst >> (=) details.CustomerId)
                            |> Option.map snd
                        | ConfirmedOrder _ ->
                            None)
                    |> List.map (fun cust ->
                        let name = CustomerName.value cust.Name
                        
                        $"Hey, {name}- Just following up. Looks like you never confirmed your order. "
                        + $"If you want to confirm your order you have until {orderByDate}. Thanks!"
                        , cust.PhoneNumber)

                return!
                    subscribedCustomersWithoutAnOrderMsgs @ customersWithUnconfirmedOrderMsgs
                    |> List.map (fun (msg, phn) ->
                        msg
                        |> SmsMsg.create
                        |> unsafeAssertOk
                        |> sendSms phn)
                    |> Async.Parallel
                    |> Async.map (
                        (Result.sequence Seq.head)
                        >> Result.map (fun _ -> ())
                    )
        }

    | _ ->
        async { return Ok () }
