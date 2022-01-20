namespace EvansFreshRoast.Domain

open EvansFreshRoast.Framework
open NodaTime
open System.Collections.Generic
open EvansFreshRoast.Utils
open System.Text.RegularExpressions

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

type OrderLineItem =
    { OrderReferenceId: CoffeeReferenceId
      Quantity: Quantity }

type OrderDetails =
    { CustomerId: Id<Customer>
      Timestamp: OffsetDateTime
      LineItems: IDictionary<Id<Coffee>, Quantity> }

type Order =
    | UnconfirmedOrder of OrderDetails
    | ConfirmedOrder of OrderDetails * Invoice

type RoastStatus =
    | NotStarted
    | InProcess
    | Complete

type Roast =
    { Customers: Id<Customer> list
      RoastDate: LocalDate
      OrderByDate: LocalDate
      Orders: Order list
      Coffees: IDictionary<CoffeeReferenceId, Id<Coffee>>
      Status: RoastStatus }
    static member Empty =
        { Customers = List.empty
          RoastDate = LocalDate.MinIsoValue.PlusDays(1)
          OrderByDate = LocalDate.MinIsoValue
          Orders = List.empty
          Coffees = dict List.empty
          Status = NotStarted }

module Roast =
    type Event =
        | OrderPlaced of OrderDetails
        | OrderCancelled of Id<Customer>
        | OrderConfirmed of Id<Customer>
        | CoffeesAdded of Id<Coffee> list
        | CustomersAdded of Id<Customer> list
        | RoastDatesChanged of roastDate: LocalDate * orderByDate: LocalDate
        | RoastStarted
        | RoastCompleted

    type Command =
        | PlaceOrder of Id<Customer> * OrderLineItem list * OffsetDateTime
        | CancelOrder of Id<Customer>
        | ConfirmOrder of Id<Customer>
        | AddCoffees of Id<Coffee> list
        | AddCustomers of Id<Customer> list
        | UpdateRoastDates of roastDate: LocalDate * orderByDate: LocalDate
        | StartRoast
        | CompleteRoast

    type Error =
        | OrderByDateAfterRoastDate
        | RoastDatesInPast
        | RoastDatesCannotBeChangedOnceStarted
        | CustomerHasAlreadyPlacedOrder
        | CustomerNotIncludedInRoast
        | AtLeastOneInvalidCoffeeQuantityInOrder // todo make this more specific
        | InvalidCoffeeReferenceIdsInOrder of CoffeeReferenceId list
        | MoreThanTwentySixCoffeesInRoast
        | OrderDoesNotExist
        | OrderAlreadyConfirmed
        | OrderWouldResultInInvalidInvoiceAmount
        | RoastAlreadyStarted
        | RoastAlreadyCompleted
        | RoastNotYetStarted

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

    let execute
        (allCustomers: seq<Id<Customer> * Customer>)
        (allCoffees: seq<Id<Coffee> * Coffee>)
        (today: LocalDate)
        roast
        cmd
        =
        match cmd with
        | UpdateRoastDates (roastDate, orderByDate) ->
            if orderByDate >= roastDate then
                Error OrderByDateAfterRoastDate
            else if orderByDate <= today || roastDate <= today then
                Error RoastDatesInPast
            else if roast.Status <> NotStarted then
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
                        | Ok _, Error e -> Error(List.singleton e)
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

                    let isError =
                        function
                        | Error _ -> true
                        | Ok _ -> false

                    let errantQuantities =
                        idQuantityMappings
                        |> List.filter (fun (_, qty) -> isError qty)

                    let assertOk =
                        function
                        | Ok a -> a
                        | Error _ -> failwith "why did you assert Ok if it was an Error, you dimwit?"

                    if List.length errantQuantities > 0 then
                        Error AtLeastOneInvalidCoffeeQuantityInOrder
                    else
                        idQuantityMappings
                        |> List.map (fun (coffeeId, quantity) -> (coffeeId, assertOk quantity))
                        |> dict
                        |> Ok

                normalizeLineItems lineItems
                |> Result.map (fun dict ->
                    { CustomerId = customerId
                      Timestamp = timestamp
                      LineItems = dict })

            Ok roast
            |> Result.bind onlyIfCustomerDidNotAlreadyOrder
            |> Result.bind onlyIfCustomerIsPartOfRoast
            |> Result.bind onlyIfAllCoffeesAreValidChoices
            |> Result.bind (createOrder lineItems)
            |> Result.map OrderPlaced

        | AddCoffees coffeeIds ->
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

        | CancelOrder customerId ->
            roast.Orders
            |> Seq.tryFind (getCustomerId >> ((=) customerId))
            |> function
                | Some _ -> Ok <| OrderCancelled customerId
                | None -> Error OrderDoesNotExist

        | ConfirmOrder customerId ->
            roast.Orders
            |> Seq.tryFind (getCustomerId >> ((=) customerId))
            |> function
                | Some order ->
                    match order with
                    | ConfirmedOrder _ -> Error OrderAlreadyConfirmed
                    | UnconfirmedOrder details ->
                        match getInvoiceAmt allCoffees details with
                        | Ok _ -> Ok(OrderConfirmed customerId)
                        | Error _ -> Error OrderWouldResultInInvalidInvoiceAmount
                | None -> Error OrderDoesNotExist

        | AddCustomers customerIds ->
            let validCustomerIds =
                allCustomers
                |> Seq.filter (fun (id, _) -> customerIds |> Seq.exists ((=) id))
                |> Seq.map fst
                |> Seq.except roast.Customers
                |> Seq.toList

            Ok <| CustomersAdded validCustomerIds

        | StartRoast ->
            match roast.Status with
            | NotStarted -> Ok RoastStarted
            | InProcess -> Error RoastAlreadyStarted
            | Complete -> Error RoastAlreadyCompleted

        | CompleteRoast ->
            match roast.Status with
            | InProcess -> Ok RoastCompleted
            | NotStarted -> Error RoastNotYetStarted
            | Complete -> Error RoastAlreadyCompleted

    let apply (allCoffees: seq<Id<Coffee> * Coffee>) roast event =
        match event with
        | OrderPlaced details -> { roast with Roast.Orders = UnconfirmedOrder details :: roast.Orders }

        | OrderCancelled customerId ->
            { roast with
                Orders =
                    roast.Orders
                    |> List.filter (getCustomerId >> (<>) customerId) }

        | OrderConfirmed customerId ->
            let confirmedOrder =
                roast.Orders
                |> List.find (getCustomerId >> (=) customerId)
                |> function
                    | UnconfirmedOrder details ->
                        match getInvoiceAmt allCoffees details with
                        | Ok invoiceAmt -> ConfirmedOrder(details, (UnpaidInvoice invoiceAmt))
                        | Error e -> failwith (sprintf "failed to create invoice, %A" e)
                    | ConfirmedOrder (details, invoice) -> ConfirmedOrder(details, invoice)

            { roast with
                Orders =
                    roast.Orders
                    |> List.filter (getCustomerId >> (<>) customerId)
                    |> List.append [ confirmedOrder ] }

        | CoffeesAdded coffeeIds ->
            let getNextCoffeeReferenceIds alreadyUsedReferenceIds count =
                let usedChars =
                    alreadyUsedReferenceIds
                    |> Seq.map (CoffeeReferenceId.value >> char)

                let alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"

                alphabet
                |> Seq.except usedChars
                |> Seq.take count
                |> Seq.map (
                    string
                    >> CoffeeReferenceId.create
                    >> (function
                    | Ok id -> id
                    | Error e -> failwith (sprintf "failed to create reference id, %A" e))
                )

            { roast with
                Coffees =
                    coffeeIds
                    |> Seq.zip (getNextCoffeeReferenceIds roast.Coffees.Keys coffeeIds.Length)
                    |> Seq.append (
                        roast.Coffees
                        |> Seq.map (fun kvp -> kvp.Key, kvp.Value)
                    )
                    |> dict }

        | CustomersAdded customerIds ->
            { roast with
                Customers =
                    roast.Customers
                    |> Seq.append customerIds
                    |> Seq.distinct
                    |> Seq.toList }

        | RoastDatesChanged (roastDate, orderByDate) ->
            { roast with
                RoastDate = roastDate
                OrderByDate = orderByDate }

        | RoastStarted -> { roast with Status = InProcess }

        | RoastCompleted -> { roast with Status = Complete }

    let createAggregate allCustomers allCoffees today =
        { Empty = Roast.Empty
          Apply = apply allCoffees
          Execute = execute allCustomers allCoffees today }

    let getOfferedCoffeeSummary (allCoffees: seq<Id<Coffee> * Coffee>) roast =
        let getSummary (referenceId, (_, coffee: Coffee)) =
            let name = CoffeeName.value coffee.Name
            let price = UsdPrice.value coffee.PricePerBag
            let weight = OzWeight.value coffee.WeightPerBag

            let description =
                CoffeeDescription.value coffee.Description

            $"{CoffeeReferenceId.value referenceId}: {name} ({price:C} per {weight:N} oz bag) — {description}"

        roast.Coffees
        |> Seq.sortBy (fun kvp -> CoffeeReferenceId.value kvp.Key)
        |> Seq.map (
            (fun kvp -> kvp.Key, allCoffees |> Seq.find (fst >> ((=) kvp.Value)))
            >> getSummary
        )
        |> Seq.fold (fun a b -> a + b + "\n") ""
