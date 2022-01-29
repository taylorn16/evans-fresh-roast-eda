module EvansFreshRoast.Sms.IncomingCommandParser

open EvansFreshRoast.Domain
open EvansFreshRoast.Framework
open EvansFreshRoast.Utils
open System.Text.RegularExpressions
open NodaTime

type Command =
    | RoastCommand of Id<Roast> * Roast.Command
    | CustomerCommand of Id<Customer> * Customer.Command

type LineParseError =
    | CouldNotParse
    | InvalidQuantity of CoffeeReferenceId

type ParseError =
    | NoOpenRoast of attemptedCommand: string
    | LineParseErrors of LineParseError seq


let trim (s: string) = s.Trim()

let clean (s: string) =
    let loweredTrimmed = s.ToLowerInvariant().Trim()
    Regex(@"[\f\v\t ]").Replace(loweredTrimmed, "")

let splitOnNewlines (s: string) =
    Regex(@"\r\n|\n\r|\r|\n").Split(s)

let parseLine (s: string) =
    let lineRegex = Regex("(\d{1,2})([a-z]{1})")
    let matches = lineRegex.Matches(s)

    if matches.Count < 1 then
        Error CouldNotParse
    else
        let groups = matches.Item(0).Groups
        let qty = groups.Item(1).Value |> int
        let refId =
            groups.Item(2).Value.ToUpperInvariant()
            |> CoffeeReferenceId.create
            |> unsafeAssertOk

        Ok (refId, qty)

let parseLines (orderMsg: string) =
    let merge =
        Map.fold
            (fun merged key value ->
                merged
                |> Map.change key (fun x ->
                    match x with
                    | Some q -> Some (q + value)
                    | None -> Some value))
    
    let lineParseResults =
        orderMsg
        |> splitOnNewlines
        |> Seq.map (trim >> parseLine)
        |> List.ofSeq
    
    let failedLines =
        lineParseResults
        |> List.filter (not << isOk)
        |> List.map unsafeAssertError

    match failedLines with
    | [] ->
        let parsedLineItems =
            lineParseResults
            |> List.map ( 
                unsafeAssertOk
                >> (fun (refId, qty) -> Map([refId, qty]))
            )
            |> List.fold merge Map.empty
            |> Map.map (fun _ qty -> Quantity.create qty)
        
        let invalidLineItems =
            parsedLineItems
            |> Map.filter (fun _ qty -> not <| isOk qty)
            |> Map.toList
            |> List.map (fun (refId, _) -> InvalidQuantity refId)

        match invalidLineItems with
        | [] ->
            parsedLineItems
            |> Map.map (fun _ qty -> unsafeAssertOk qty)
            |> Map.toList
            |> List.map (fun (refId, qty) ->
                { OrderReferenceId = refId
                  Quantity = qty })
            |> Ok
        | _ ->
            Error <| LineParseErrors invalidLineItems
    | _ ->
        Error <| LineParseErrors failedLines

let parse
    (getAllRoasts: unit -> Async<list<Id<Roast> * RoastStatus>>)
    (getNow: unit -> OffsetDateTime)
    customerId
    smsMsg
    = async {
    let getOpenRoastId () = async {
        let! allRoasts = getAllRoasts()
        
        return allRoasts
        |> List.tryFind (snd >> (=) Open)
        |> Option.map fst
    }

    match SmsMsg.value smsMsg |> clean with
    | "subscribe" ->
        return Ok <| CustomerCommand(customerId, Customer.Command.Subscribe)

    | "unsubscribe" ->
        return Ok <| CustomerCommand(customerId, Customer.Command.Unsubscribe)

    | "cancel" ->
        match! getOpenRoastId() with
        | Some openRoastId ->
            return Ok <| RoastCommand(openRoastId, Roast.Command.CancelOrder(customerId))

        | None ->
            return Error <| NoOpenRoast "cancel an order"

    | "confirm" ->
        match! getOpenRoastId() with
        | Some openRoastId ->
            return Ok <| RoastCommand(openRoastId, Roast.Command.ConfirmOrder(customerId))

        | None ->
            return Error <| NoOpenRoast "confirm an order"

    | orderMsg ->
        match parseLines(orderMsg) with
        | Ok lineItems ->
            match! getOpenRoastId() with
            | Some openRoastId ->
                return Ok <| RoastCommand(openRoastId, Roast.Command.PlaceOrder(customerId, lineItems, getNow()))

            | None ->
                return Error <| NoOpenRoast "place an order"

        | Error parseError ->
            return Error parseError
}
