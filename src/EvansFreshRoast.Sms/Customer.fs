module EvansFreshRoast.Sms.Customer

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Utils

let handleEvent
    (sendSms: UsPhoneNumber -> SmsMsg -> Async<Result<unit, string>>)
    (getCustomer: Id<Customer> -> Async<option<Id<Customer> * Customer>>)
    (event: DomainEvent<Customer, Event>)
    =
    let asyncOk = Async.lift <| Ok ()

    match event.Body with
    | Created { Name = name; PhoneNumber = phoneNumber } ->
        $"Hey, {CustomerName.value name}! Evan's Fresh Roast here. "
        + "You've been added to the text list to get updates and order coffee. "
        + "Please reply SUBSCRIBE to start receiving updates!"
        |> SmsMsg.create
        |> unsafeAssertOk
        |> sendSms phoneNumber

    | Subscribed ->
        async {
            let! customer = getCustomer event.AggregateId
            return! customer
            |> Option.map (fun (_, cust) ->
                $"Alright, {cust.Name}! You're officially signed up to receive updates. "
                + "Just text me UNSUBSCRIBE at any time to opt-out of receiving further texts."
                |> SmsMsg.create
                |> unsafeAssertOk
                |> sendSms cust.PhoneNumber)
            |> Option.defaultValue asyncOk
        }
        
    | Unsubscribed ->
        async {
            let! customer = getCustomer event.AggregateId
            return! customer
            |> Option.map (fun (_, cust) ->
                $"Ok, {CustomerName.value cust.Name}. You will no longer receive text updates. "
                + "If you change your mind, just text me SUBSCRIBE at any time."
                |> SmsMsg.create
                |> unsafeAssertOk
                |> sendSms cust.PhoneNumber)
            |> Option.defaultValue asyncOk
        }

    | _ ->
        asyncOk 
