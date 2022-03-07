namespace EvansFreshRoast.Dto

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open System
open EvansFreshRoast.Domain

[<CLIMutable>]
type CreateRoastRequest =
    { Name: string
      RoastDate: DateTimeOffset
      OrderByDate: DateTimeOffset }

[<CLIMutable>]
type RoastSummaryCoffee =
    { Id: Guid
      Name: string }
    static member encode rsc =
        Encode.object [ "id", Encode.guid rsc.Id
                        "name", Encode.string rsc.Name ]
        
    static member decoder: Decoder<RoastSummaryCoffee> =
        Decode.object <| fun get ->
            { Id = get.Required.Field "id" Decode.guid
              Name = get.Required.Field "name" Decode.string }

[<CLIMutable>]
type RoastSummary =
    { Id: Guid
      Name: string
      RoastDate: DateTimeOffset
      OrderByDate: DateTimeOffset
      CustomersCount: int
      Coffees: RoastSummaryCoffee list
      RoastStatus: RoastStatus
      OrdersCount: int }
    static member encode rs =
        Encode.object [ "id", Encode.guid rs.Id
                        "name", Encode.string rs.Name
                        "roastDate", Encode.datetimeOffset rs.RoastDate
                        "orderByDate", Encode.datetimeOffset rs.OrderByDate
                        "customersCount", Encode.int rs.CustomersCount
                        "coffees", rs.Coffees |> List.map RoastSummaryCoffee.encode |> Encode.list
                        "roastStatus", Encode.string <| string rs.RoastStatus
                        "ordersCount", Encode.int rs.OrdersCount ]
        
    static member decoder: Decoder<RoastSummary> =
        let decodeRoastStatus =
            Decode.string
            |> Decode.andThen (
                function
                | "NotPublished" -> Decode.succeed NotPublished
                | "Open" -> Decode.succeed Open
                | "Closed" -> Decode.succeed Closed
                | s -> Decode.fail $"{s} is not a valid RoastStatus.")
        
        Decode.object <| fun get ->
            { Id = get.Required.Field "id" Decode.guid
              Name = get.Required.Field "name" Decode.string
              RoastDate = get.Required.Field "roastDate" Decode.datetimeOffset
              OrderByDate = get.Required.Field "orderByDate" Decode.datetimeOffset
              CustomersCount = get.Required.Field "customersCount" Decode.int
              Coffees = get.Required.Field "coffees" (Decode.list RoastSummaryCoffee.decoder)
              RoastStatus = get.Required.Field "roastStatus" decodeRoastStatus
              OrdersCount = get.Required.Field "ordersCount" Decode.int }

[<CLIMutable>]
type RoastDetailsOrderInvoice =
    { Amount: decimal
      PaymentMethod: string option }
    static member encode rdoi =
        Encode.object [ "amount", Encode.decimal rdoi.Amount
                        "paymentMethod", Encode.option Encode.string rdoi.PaymentMethod ]
        
    static member decoder: Decoder<RoastDetailsOrderInvoice> =
        Decode.object <| fun get ->
            { Amount = get.Required.Field "amount" Decode.decimal
              PaymentMethod = get.Optional.Field "paymentMethod" Decode.string }

[<CLIMutable>]
type RoastDetailsOrder =
    { CustomerId: Guid
      Timestamp: DateTimeOffset
      LineItems: Map<Guid, int>
      Invoice: RoastDetailsOrderInvoice option }
    static member encode rdo =
        let encodeLineItems (lineItems: Map<Guid, int>) =
            Map.toList lineItems
            |> List.map (Encode.tuple2 Encode.guid Encode.int)
            |> Encode.list
        
        Encode.object [ "customerId", Encode.guid rdo.CustomerId
                        "timestamp", Encode.datetimeOffset rdo.Timestamp
                        "invoice", Encode.option RoastDetailsOrderInvoice.encode rdo.Invoice
                        "lineItems", encodeLineItems rdo.LineItems ]
        
    static member decoder: Decoder<RoastDetailsOrder> =
        let decodeLineItems: Decoder<Map<Guid, int>> =
            Decode.list (Decode.tuple2 Decode.guid Decode.int)
            |> Decode.andThen (Map.ofList >> Decode.succeed)
        
        Decode.object <| fun get ->
            { CustomerId = get.Required.Field "customerId" Decode.guid
              Timestamp = get.Required.Field "timestamp" Decode.datetimeOffset
              Invoice = get.Optional.Field "invoice" RoastDetailsOrderInvoice.decoder
              LineItems = get.Required.Field "lineItems"  decodeLineItems }

[<CLIMutable>]
type RoastDetails =
    { Id: Guid
      Name: string
      RoastDate: DateTimeOffset
      OrderByDate: DateTimeOffset
      Customers: Guid list
      Coffees: Guid list
      Orders: RoastDetailsOrder list
      Status: RoastStatus
      SentRemindersCount: int }
    static member encode rd =
        Encode.object [ "id", Encode.guid rd.Id
                        "name", Encode.string rd.Name
                        "roastDate", Encode.datetimeOffset rd.RoastDate
                        "orderByDate", Encode.datetimeOffset rd.OrderByDate
                        "customers", rd.Customers |> List.map Encode.guid |> Encode.list
                        "coffees", rd.Coffees |> List.map Encode.guid |> Encode.list
                        "orders", rd.Orders |> List.map RoastDetailsOrder.encode |> Encode.list
                        "status", Encode.string <| string rd.Status
                        "sentRemindersCount", Encode.int rd.SentRemindersCount ]
        
    static member decoder: Decoder<RoastDetails> =
        let decodeRoastStatus =
            Decode.string
            |> Decode.andThen (
                function
                | "NotPublished" -> Decode.succeed NotPublished
                | "Open" -> Decode.succeed Open
                | "Closed" -> Decode.succeed Closed
                | s -> Decode.fail $"{s} is not a valid roast status." )
        
        Decode.object <| fun get ->
            { Id = get.Required.Field "id" Decode.guid
              Name = get.Required.Field "name" Decode.string
              RoastDate = get.Required.Field "roastDate" Decode.datetimeOffset
              OrderByDate = get.Required.Field "orderByDate" Decode.datetimeOffset
              Customers = get.Required.Field "customers" (Decode.list Decode.guid)
              Coffees = get.Required.Field "coffees" (Decode.list Decode.guid)
              Orders = get.Required.Field "orders" (Decode.list RoastDetailsOrder.decoder)
              Status = get.Required.Field "status" decodeRoastStatus
              SentRemindersCount = get.Required.Field "sentRemindersCount" Decode.int }
