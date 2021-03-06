module EvansFreshRoast.Sms.Customer

open EvansFreshRoast.Framework
open EvansFreshRoast.Domain
open EvansFreshRoast.Domain.Customer
open EvansFreshRoast.Utils

let handleEvent
    (sendSms: UsPhoneNumber -> SmsMsg -> Async<Result<unit, exn>>)
    (getCustomer: Id<Customer> -> Async<option<Id<Customer> * Customer>>)
    (event: DomainEvent<Customer, Event>)
    =
    let asyncOk = Async.lift <| Ok ()

    match event.Body with
    | Created { Name = name; PhoneNumber = phoneNumber } ->
        $"Hey, {CustomerName.value name}! Evan's Fresh Roast here. "
        + "You've been added to the text list to get updates and order coffee. "
        + "Please reply 'Opt In' to start receiving updates!"
        |> SmsMsg.create
        |> unsafeAssertOk
        |> sendSms phoneNumber

    | Subscribed ->
        async {
            let! customer = getCustomer event.AggregateId
            return! customer
            |> Option.map (fun (_, cust) ->
                $"Alright, {CustomerName.value cust.Name}! You're officially signed up to receive updates. "
                + "Just text me 'Opt Out' at any time to opt-out of receiving further texts."
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
                $"Ok, {CustomerName.value cust.Name}. You will not receive any texts for future roasts. "
                + "If you change your mind, just text me 'Opt In' at any time."
                |> SmsMsg.create
                |> unsafeAssertOk
                |> sendSms cust.PhoneNumber)
            |> Option.defaultValue asyncOk
        }

    | _ ->
        asyncOk 
