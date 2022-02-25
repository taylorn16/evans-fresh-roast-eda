namespace EvansFreshRoast.Domain

open EvansFreshRoast.Framework
open System.Collections.Generic
open EvansFreshRoast.Utils
open System.Text.RegularExpressions
open System

type UsdInvoiceAmount = private UsdInvoiceAmount of decimal

module UsdInvoiceAmount =
    let create amt =
        match amt with
        | a when a < 0m -> Error <| DomainTypeError InvoiceAmountIsNegative
        | a when a > 1000m -> Error <| DomainTypeError InvoiceAmountExceeds1000
        | _ -> Ok(UsdInvoiceAmount amt)

    let apply f (UsdInvoiceAmount amt) = f amt

    let value = apply id

type Quantity = private Quantity of int

module Quantity =
    let create qty =
        match qty with
        | q when q < 0 -> Error <| DomainTypeError QuantityIsNegative
        | q when q > 50 -> Error <| DomainTypeError QuantityExceeds50Bags
        | _ -> Ok(Quantity qty)

    let apply f (Quantity qty) = f qty

    let value = apply id

type CoffeeReferenceId = private CoffeeReferenceId of string

module CoffeeReferenceId =
    let create ref =
        Regex.IsMatch(ref, "^[A-Z]$")
        |> function
            | true -> Ok <| CoffeeReferenceId ref
            | false -> Error <| DomainTypeError ReferenceIdMustBeAtoZ

    let apply f (CoffeeReferenceId ref) = f ref

    let value = apply id

type PaymentMethod =
    | Unknown
    | Venmo
    | Cash
    | Check

type Invoice =
    | PaidInvoice of UsdInvoiceAmount * PaymentMethod
    | UnpaidInvoice of UsdInvoiceAmount

module Invoice =
    let getAmount =
        function
        | PaidInvoice (amt, _) -> amt
        | UnpaidInvoice amt -> amt

type OrderLineItem =
    { OrderReferenceId: CoffeeReferenceId
      Quantity: Quantity }

type OrderDetails =
    { CustomerId: Id<Customer>
      Timestamp: DateTimeOffset
      LineItems: IDictionary<Id<Coffee>, Quantity> }

type Order =
    | UnconfirmedOrder of OrderDetails
    | ConfirmedOrder of OrderDetails * Invoice

module Order =
    let getCustomerId =
        function
        | UnconfirmedOrder details -> details.CustomerId
        | ConfirmedOrder(details, _) -> details.CustomerId

    let getLineItems =
        function
        | UnconfirmedOrder details -> details.LineItems
        | ConfirmedOrder(details, _) -> details.LineItems

type RoastStatus =
    | NotPublished
    | Open
    | Closed

type RoastName = private RoastName of String100

module RoastName =
    let create s = RoastName <!> String100.create s

    let apply f (RoastName name) = String100.apply f name

    let value = apply id

type Roast =
    { Name: RoastName
      Customers: Id<Customer> list
      RoastDate: DateTime
      OrderByDate: DateTime
      Orders: Order list
      Coffees: IDictionary<CoffeeReferenceId, Id<Coffee>>
      Status: RoastStatus
      SentRemindersCount: NonNegativeInt }
    static member Empty =
        { Name = RoastName.create "<empty>" |> unsafeAssertOk
          Customers = List.empty
          RoastDate = DateTime.MinValue.AddDays(1)
          OrderByDate = DateTime.MinValue
          Orders = List.empty
          Coffees = dict List.empty
          Status = NotPublished
          SentRemindersCount = NonNegativeInt.zero }

type RoastCreated =
    { Name: RoastName
      RoastDate: DateTime
      OrderByDate: DateTime }

module Roast =
    type Event =
        | OrderPlaced of OrderDetails
        | OrderCancelled of Id<Customer>
        | OrderConfirmed of Id<Customer> * UsdInvoiceAmount
        | CoffeesAdded of Id<Coffee> list
        | CoffeesRemoved of Id<Coffee> list
        | CustomersAdded of Id<Customer> list
        | CustomersRemoved of Id<Customer> list
        | RoastDatesChanged of roastDate: DateTime * orderByDate: DateTime
        | RoastStarted of summary: string
        | RoastCompleted
        | Created of RoastCreated
        | ReminderSent
        | InvoicePaid of Id<Customer> * PaymentMethod

    type Command =
        | PlaceOrder of customerId: Id<Customer> * lineItems: OrderLineItem list * timestamp: DateTimeOffset
        | CancelOrder of Id<Customer>
        | ConfirmOrder of Id<Customer>
        | AddCoffees of Id<Coffee> list
        | RemoveCoffees of Id<Coffee> list
        | AddCustomers of Id<Customer> list
        | RemoveCustomers of Id<Customer> list
        | UpdateRoastDates of roastDate: DateTime * orderByDate: DateTime
        | StartRoast
        | CompleteRoast
        | Create of RoastCreated
        | SendReminder
        | PayInvoice of Id<Customer> * PaymentMethod

    type Error =
        | OrderByDateAfterRoastDate
        | RoastDatesInPast
        | RoastDatesCannotBeChangedOnceStarted
        | CustomerHasAlreadyPlacedOrder
        | CustomerNotIncludedInRoast
        | AtLeastOneInvalidCoffeeQuantityInOrder // TODO: make this more specific?
        | InvalidCoffeeReferenceIdsInOrder of CoffeeReferenceId list
        | MoreThanTwentySixCoffeesInRoast
        | OrderDoesNotExist
        | OrderAlreadyConfirmed
        | OrderWouldResultInInvalidInvoiceAmount
        | RoastAlreadyStarted
        | RoastAlreadyCompleted
        | RoastNotYetStarted
        | RoastAlreadyCreated
        | OrderNotConfirmed
        | AnotherRoastIsOpen

    let getCustomerId =
        function
        | UnconfirmedOrder details -> details.CustomerId
        | ConfirmedOrder (details, _) -> details.CustomerId

    let getInvoiceAmt (allCoffees: seq<Id<Coffee> * Coffee>) { LineItems = lineItems } =
        lineItems
        |> Seq.sumBy (fun kvp ->
            let price =
                allCoffees
                |> Seq.find (fst >> (=) kvp.Key)
                |> fun (_, coffee) -> coffee.PricePerBag
                |> UsdPrice.value

            let qty = Quantity.value kvp.Value |> decimal

            price * qty)
        |> UsdInvoiceAmount.create

    let withReferenceIds (coffeeIds: Id<Coffee> seq) =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
        |> Seq.map (
            string
            >> CoffeeReferenceId.create
            >> unsafeAssertOk
        )
        |> Seq.zip coffeeIds
        |> Seq.map (fun (id, refId) -> refId, id)

    let onlyIfRoastIsOpen roast =
        roast.Status
        |> function
            | Open -> Ok roast
            | Closed -> Error RoastAlreadyCompleted
            | NotPublished -> Error RoastNotYetStarted

    let getOfferedCoffeeSummary (allCoffees: seq<Id<Coffee> * Coffee>) roast =
        let getSummary (referenceId, (_, coffee: Coffee)) =
            let name = CoffeeName.value coffee.Name
            let price = UsdPrice.value coffee.PricePerBag
            let weight = OzWeight.value coffee.WeightPerBag

            let description = CoffeeDescription.value coffee.Description

            $"{CoffeeReferenceId.value referenceId}: {name} ({price:C2}/{weight:N} oz) - {description}"

        roast.Coffees
        |> Seq.sortBy (fun kvp -> CoffeeReferenceId.value kvp.Key)
        |> Seq.map (
            (fun kvp -> kvp.Key, allCoffees |> Seq.find (fst >> ((=) kvp.Value)))
            >> getSummary
        )
        |> Seq.fold (fun a b -> a + b + "\n") ""

    let execute
        (allRoasts: seq<Id<Roast> * RoastStatus>)
        (allCustomers: seq<Id<Customer> * Customer>)
        (allCoffees: seq<Id<Coffee> * Coffee>)
        (today: DateTime)
        (roast: Roast)
        cmd
        =
        match cmd with
        | Create fields ->
            if RoastName.value roast.Name <> "<empty>" then
                Error RoastAlreadyCreated
            else
                Ok <| Created fields

        | UpdateRoastDates (roastDate, orderByDate) ->
            if orderByDate >= roastDate then
                Error OrderByDateAfterRoastDate
            else if orderByDate <= today || roastDate <= today then
                Error RoastDatesInPast
            else if roast.Status <> NotPublished then
                Error RoastDatesCannotBeChangedOnceStarted
            else
                Ok <| RoastDatesChanged(roastDate, orderByDate)

        | PlaceOrder (customerId, lineItems, timestamp) ->
            let onlyIfCustomerDidNotAlreadyOrder roast =
                roast.Orders
                |> List.tryFind (getCustomerId >> (=) customerId)
                |> function
                    | Some _ -> Error CustomerHasAlreadyPlacedOrder
                    | None -> Ok roast

            let onlyIfCustomerIsPartOfRoast roast =
                roast.Customers
                |> Seq.tryFind ((=) customerId)
                |> function
                    | Some _ -> Ok roast
                    | None -> Error CustomerNotIncludedInRoast

            let onlyIfAllCoffeesAreValidChoices roast =
                let foldResults results =
                    let folder acc next =
                        match (acc, next) with
                        | Ok _, Error e -> Error [e]
                        | Error errs, Error e -> Error(e :: errs)
                        | Ok oks, Ok ok -> Ok(ok :: oks)
                        | Error errs, Ok _ -> Error errs

                    results |> Seq.fold folder (Ok List.empty)

                lineItems
                |> Seq.map (fun li ->
                    roast.Coffees.Keys
                    |> Seq.tryFind ((=) li.OrderReferenceId)
                    |> Result.ofOption li.OrderReferenceId)
                |> foldResults
                |> Result.mapError InvalidCoffeeReferenceIdsInOrder
                |> Result.map (fun _ -> roast)

            let createOrder lineItems roast =
                let normalizeLineItems lineItems =
                    let idQuantityMappings =
                        lineItems
                        |> List.filter (fun li ->
                            roast.Coffees.Keys
                            |> Seq.exists ((=) li.OrderReferenceId))
                        |> List.map (fun li ->
                            roast.Coffees
                            |> Seq.find ((fun kvp -> kvp.Key) >> ((=) li.OrderReferenceId))
                            |> fun a -> a.Value, li.Quantity)
                        |> List.groupBy (fun (coffeeId, _) -> coffeeId)
                        |> List.map (fun (coffeeId, quantities) ->
                            (coffeeId,
                             quantities
                             |> List.sumBy (fun (_, qty) -> Quantity.value qty)
                             |> Quantity.create))

                    let errantQuantities =
                        idQuantityMappings
                        |> List.filter (not << (snd >> isOk))

                    if List.length errantQuantities > 0 then
                        Error AtLeastOneInvalidCoffeeQuantityInOrder
                    else
                        idQuantityMappings
                        |> List.map (fun (coffeeId, quantity) -> (coffeeId, unsafeAssertOk quantity))
                        |> dict
                        |> Ok

                normalizeLineItems lineItems
                |> Result.map (fun dict ->
                    { CustomerId = customerId
                      Timestamp = timestamp
                      LineItems = dict })

            Ok roast
            |> Result.bind onlyIfRoastIsOpen
            |> Result.bind onlyIfCustomerDidNotAlreadyOrder
            |> Result.bind onlyIfCustomerIsPartOfRoast
            |> Result.bind onlyIfAllCoffeesAreValidChoices
            |> Result.bind (createOrder lineItems)
            |> Result.map OrderPlaced

        | AddCoffees coffeeIds ->
            if roast.Status = NotPublished then
                let validCoffeeIds =
                    allCoffees
                    |> Seq.filter (fun (id, _) -> coffeeIds |> Seq.exists ((=) id))
                    |> Seq.map fst
                    |> Seq.except roast.Coffees.Values
                    |> Seq.toList

                if roast.Coffees.Count + Seq.length validCoffeeIds > 26 then
                    Error MoreThanTwentySixCoffeesInRoast
                else
                    Ok <| CoffeesAdded validCoffeeIds
            else
                Error RoastAlreadyStarted

        | RemoveCoffees coffeeIds ->
            if roast.Status = NotPublished then
                let validCoffeeIds =
                    coffeeIds
                    |> Seq.distinct
                    |> Seq.filter (fun id ->
                        roast.Coffees.Values |> Seq.exists ((=) id))
                    |> List.ofSeq

                Ok <| CoffeesRemoved validCoffeeIds
            else
                Error RoastAlreadyStarted

        | CancelOrder customerId ->
            Ok roast
            |> Result.bind onlyIfRoastIsOpen
            |> Result.bind (fun _ ->
                roast.Orders
                |> Seq.tryFind (getCustomerId >> ((=) customerId))
                |> function
                    | Some _ -> Ok <| OrderCancelled customerId
                    | None -> Error OrderDoesNotExist)

        | ConfirmOrder customerId ->
            Ok roast
            |> Result.bind onlyIfRoastIsOpen
            |> Result.bind (fun _ ->
                roast.Orders
                |> Seq.tryFind (getCustomerId >> ((=) customerId))
                |> function
                    | Some order ->
                        match order with
                        | ConfirmedOrder _ -> Error OrderAlreadyConfirmed
                        | UnconfirmedOrder details ->
                            match getInvoiceAmt allCoffees details with
                            | Ok invoiceAmt -> Ok <| OrderConfirmed(customerId, invoiceAmt)
                            | Error _ -> Error OrderWouldResultInInvalidInvoiceAmount
                    | None -> Error OrderDoesNotExist)

        | AddCustomers customerIds ->
            if roast.Status = NotPublished then
                let validCustomerIds =
                    allCustomers
                    |> Seq.filter (fun (id, _) -> customerIds |> Seq.exists ((=) id))
                    |> Seq.map fst
                    |> Seq.except roast.Customers
                    |> Seq.distinct
                    |> Seq.toList

                Ok <| CustomersAdded validCustomerIds
            else
                Error RoastAlreadyStarted

        | RemoveCustomers customerIds ->
            if roast.Status = NotPublished then
                let validCustomerIds =
                    customerIds
                    |> Seq.distinct
                    |> Seq.filter (fun id -> roast.Customers |> Seq.exists ((=) id))
                    |> List.ofSeq

                Ok <| CustomersRemoved validCustomerIds
            else
                Error RoastAlreadyStarted

        | StartRoast ->
            if allRoasts |> Seq.exists (snd >> (=) Open) then
                Error AnotherRoastIsOpen
            else
                match roast.Status with
                | NotPublished ->
                    let summary = getOfferedCoffeeSummary allCoffees roast
                    
                    Ok <| RoastStarted summary
                | Open -> Error RoastAlreadyStarted
                | Closed -> Error RoastAlreadyCompleted

        | CompleteRoast ->
            match roast.Status with
            | Open -> Ok RoastCompleted
            | NotPublished -> Error RoastNotYetStarted
            | Closed -> Error RoastAlreadyCompleted

        | SendReminder ->
            match roast.Status with
            | NotPublished -> Error RoastNotYetStarted
            | Open -> Ok ReminderSent
            | Closed -> Error RoastAlreadyCompleted

        | PayInvoice (customerId, paymentMethod) ->
            roast.Orders
            |> List.tryFind (getCustomerId >> (=) customerId)
            |> Result.ofOption OrderDoesNotExist
            |> Result.bind (
                function
                | UnconfirmedOrder _ -> Error OrderNotConfirmed
                | ConfirmedOrder _ -> Ok <| InvoicePaid(customerId, paymentMethod)
            )

    let apply roast event =
        match event with
        | Created fields ->
            { roast with
                Roast.Name = fields.Name
                RoastDate = fields.RoastDate
                OrderByDate = fields.OrderByDate }

        | OrderPlaced details ->
            { roast with
                Orders = UnconfirmedOrder details :: roast.Orders }

        | OrderCancelled customerId ->
            { roast with
                Orders =
                    roast.Orders
                    |> List.filter (getCustomerId >> (<>) customerId) }

        | OrderConfirmed(customerId, invoiceAmt) ->
            let confirmedOrder =
                roast.Orders
                |> List.find (getCustomerId >> (=) customerId)
                |> function
                    | UnconfirmedOrder details ->
                        ConfirmedOrder(details, UnpaidInvoice invoiceAmt)
                    | ConfirmedOrder(details, invoice) ->
                        ConfirmedOrder(details, invoice)

            { roast with
                Orders =
                    roast.Orders
                    |> List.filter (getCustomerId >> (<>) customerId)
                    |> List.append [ confirmedOrder ] }

        | CoffeesAdded coffeeIds ->
            let newCoffeesDict =
                roast.Coffees.Values
                |> Seq.append coffeeIds
                |> Seq.distinct
                |> withReferenceIds
                |> dict

            { roast with Coffees = newCoffeesDict }

        | CoffeesRemoved coffeeIds ->
            let newCoffeesDict =
                roast.Coffees.Values
                |> Seq.except coffeeIds
                |> Seq.distinct
                |> withReferenceIds
                |> dict

            { roast with Coffees = newCoffeesDict }

        | CustomersAdded customerIds ->
            { roast with
                Customers =
                    roast.Customers
                    |> Seq.append customerIds
                    |> Seq.distinct
                    |> Seq.toList }

        | CustomersRemoved customerIds ->
            { roast with
                Customers =
                    roast.Customers
                    |> List.filter (fun id -> customerIds |> (List.tryFind ((=) id)) = None) }

        | RoastDatesChanged (roastDate, orderByDate) ->
            { roast with
                RoastDate = roastDate
                OrderByDate = orderByDate }

        | RoastStarted _ -> { roast with Status = Open }

        | RoastCompleted -> { roast with Status = Closed }

        | ReminderSent ->
            { roast with
                SentRemindersCount = NonNegativeInt.increment roast.SentRemindersCount }

        | InvoicePaid (customerId, paymentMethod) ->
            let paidOrder =
                roast.Orders
                |> List.find (getCustomerId >> (=) customerId)
                |> function
                    | ConfirmedOrder(details, invoice) ->
                        ConfirmedOrder(details, PaidInvoice(Invoice.getAmount invoice, paymentMethod))
                    | UnconfirmedOrder _ ->
                        failwith "This should never, ever happen."

            { roast with
                Orders = roast.Orders
                         |> List.filter (getCustomerId >> (<>) customerId)
                         |> List.append [ paidOrder ] }

    let createAggregate allRoasts allCustomers allCoffees today =
        { Empty = Roast.Empty
          Apply = apply
          Execute = execute allRoasts allCustomers allCoffees today }
