namespace EvansFreshRoast.Domain

open EvansFreshRoast.Framework
open NodaTime
open System.Collections.Generic
open EvansFreshRoast.Utils

type RoastStatus =
    | NotStarted
    | InProcess
    | Complete

type Roast =
    { Customers: Id<CustomerProjection> list
      RoastDate: LocalDate
      OrderByDate: LocalDate
      Orders: Order list
      Coffees: IDictionary<CoffeeReferenceId, Id<CoffeeProjection>>
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
        | OrderCancelled of Id<CustomerProjection>
        | OrderConfirmed of Id<CustomerProjection>
        | CoffeesAdded of Id<CoffeeProjection> list
        | CustomersAdded of Id<CustomerProjection> list
        | RoastDatesChanged of roastDate: LocalDate * orderByDate: LocalDate
        | RoastStarted
        | RoastCompleted

    type Command =
        | PlaceOrder of Id<CustomerProjection> * OrderLineItem list * OffsetDateTime
        | CancelOrder of Id<CustomerProjection>
        | ConfirmOrder of Id<CustomerProjection>
        | AddCoffees of Id<CoffeeProjection> list
        | AddCustomers of Id<CustomerProjection> list
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

    let getInvoiceAmt (allCoffees: seq<CoffeeProjection>) { LineItems = lineItems } =
        lineItems
        |> Seq.sumBy (fun kvp ->
            let price =
                allCoffees
                |> Seq.find (fun coffee -> coffee.Id = kvp.Key)
                |> fun coffee -> coffee.PricePerBag
                |> UsdPrice.value

            let qty = Quantity.value kvp.Value |> decimal

            price * qty)
        |> UsdInvoiceAmount.create

    let execute (allCustomers: seq<CustomerProjection>) (allCoffees: seq<CoffeeProjection>) (today: LocalDate) roast cmd =
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
                |> Seq.filter (fun coffee -> coffeeIds |> Seq.exists ((=) coffee.Id))
                |> Seq.map (fun coffee -> coffee.Id)
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
                |> Seq.filter (fun cust -> customerIds |> Seq.exists ((=) cust.Id))
                |> Seq.map (fun cust -> cust.Id)
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

    let apply (allCoffees: seq<CoffeeProjection>) roast event =
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

    let getOfferedCoffeeSummary (allCoffees: seq<CoffeeProjection>) roast =
        let getSummary (referenceId, (coffee: CoffeeProjection)) =
            let name = CoffeeName.value coffee.Name
            let price = UsdPrice.value coffee.PricePerBag
            let weight = OzWeight.value coffee.WeightPerBag

            let description =
                CoffeeDescription.value coffee.Description

            $"{CoffeeReferenceId.value referenceId}: {name} ({price:C} per {weight:N} oz bag) — {description}"

        roast.Coffees
        |> Seq.sortBy (fun kvp -> CoffeeReferenceId.value kvp.Key)
        |> Seq.map (
            (fun kvp ->
                (kvp.Key,
                allCoffees
                |> Seq.find ((fun c -> c.Id) >> ((=) kvp.Value))))
            >> getSummary
        )
        |> Seq.fold (fun a b -> a + b + "\n") ""
