module EvansFreshRoast.Sms.Roast

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Roast
open EvansFreshRoast.Utils

let rec join separator strs =
    match strs with
    | [] -> ""
    | [s] -> s
    | head::tail ->
        head + separator + join separator tail

let handleEvent
    (sendSms: UsPhoneNumber -> SmsMsg -> Async<Result<unit, string>>)
    (getAllCoffees: unit -> Async<list<Id<Coffee> * Coffee>>)
    (getRoastCustomerIds: Id<Roast> -> Async<option<Id<Customer> list>>)
    (getAllCustomers: unit -> Async<list<Id<Customer> * Customer>>)
    (getCustomer: Id<Customer> -> Async<option<Id<Customer> * Customer>>)
    (event: DomainEvent<Roast, Event>)
    =
    match event.Body with
    | RoastStarted coffeesSummary ->
        async {
            match! getRoastCustomerIds event.AggregateId with
            | None
            | Some [] ->
                return Ok ()

            | Some roastCustomerIds ->
                let! allCustomers = getAllCustomers()

                let subscribedRoastCustomers =
                    allCustomers
                    |> List.filter (fun (_, cust) -> cust.Status = Subscribed)
                    |> List.filter (fun (cId, _) -> roastCustomerIds |> List.exists ((=) cId))
                    |> List.map snd

                let getMsg name =
                    $"Hello, {CustomerName.value name}! There's a new roast happening. "
                    + "Here are the coffees on tap:\n\n"
                    + coffeesSummary + "\n"
                    + "To place an order, reply with each item on a new line in the format [qty][id], "
                    + "e.g., '2A' on the first line and '4C' on the next line."
                    |> SmsMsg.create
                    |> unsafeAssertOk

                return!
                    subscribedRoastCustomers
                    |> List.map (fun c -> sendSms c.PhoneNumber (getMsg c.Name))
                    |> Async.Parallel
                    |> Async.map (
                        (Result.sequence (List.ofSeq >> join "; "))
                        >> Result.map (fun _ -> ())
                    )
        }

    
    | OrderPlaced { CustomerId = customerId; LineItems = lineItems } ->
        async {
            let! allCoffees = getAllCoffees()
            match! getCustomer customerId with
            | Some(_, customer) ->
                let orderReivew =
                    lineItems
                    |> Seq.map (fun kvp ->
                        let coffee = allCoffees |> List.find (fst >> (=) kvp.Key) |> snd
                        $"{Quantity.value kvp.Value} x {CoffeeName.value coffee.Name} @ {(UsdPrice.value coffee.PricePerBag):C2} ea."
                    )
                    |> List.ofSeq
                    |> join "\n"

                return!
                    $"Thanks, {CustomerName.value customer.Name}! Here's what we got:\n\n"
                    + orderReivew + "\n\n"
                    + "If that looks correct to you, please reply 'Confirm Order'. "
                    + "Otherwise, reply 'Cancel Order' to scratch that and try again."
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
                return!
                    $"Alright, {CustomerName.value customer.Name}! Your order is confirmed. The total for your order is "
                    + $"{(UsdInvoiceAmount.value invoiceAmt):C2}."
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
                return!
                    $"No sweat, {CustomerName.value customer.Name}. Your order is cancelled. "
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
        async { return Ok () }

    | ReminderSent ->
        async { return Ok () }

    | _ ->
        async { return Ok () }
