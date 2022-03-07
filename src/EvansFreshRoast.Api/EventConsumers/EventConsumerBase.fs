namespace EvansFreshRoast.Api.EventConsumers

open EvansFreshRoast.Api
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open System.Threading
open Thoth.Json.Net
open EvansFreshRoast.Framework
open RabbitMQ.Client
open System.Threading.Tasks
open EvansFreshRoast.Utils
open System.Text

type RabbitMqConsumerError =
    | NoMessageAvailable
    | DeserializationError of string

type Ack = unit -> unit

[<AbstractClass>]
type EventConsumerBase<'State, 'Event>
    ( logger: ILogger,
      connectionFactory: IConnectionFactory,
      exchangeName: string,
      route: string,
      queueName: string,
      decoder: Decoder<DomainEvent<'State, 'Event>>,
      domainEventsHub: IHubContext<DomainEventsHub> ) =

    let connection = connectionFactory.CreateConnection()
    let channel = connection.CreateModel()

    let cts = new CancellationTokenSource()

    abstract member handleEvent: DomainEvent<'State, 'Event> -> Async<Result<string option, exn>>

    interface IHostedService with
        member this.StartAsync(_: CancellationToken) = task {
            logger.LogInformation("Domain event read model consumer starting up.") // TODO: which one?

            channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true, false, null)
            channel.QueueDeclare(queueName, true, false, false, null) |> ignore
            channel.QueueBind(queueName, exchangeName, route, null)

            let consumeTask = async {
                while not cts.Token.IsCancellationRequested do
                    do! Task.Delay(500, cts.Token) |> Async.AwaitTask

                    let queueMsg = channel.BasicGet(queueName, false)

                    let domainEvent =
                        queueMsg
                        |> Option.ofObj
                        |> Result.ofOption NoMessageAvailable
                        |> Result.map (fun msg -> Encoding.UTF8.GetString msg.Body.Span)
                        |> Result.bind (
                            Decode.fromString decoder >> (Result.mapError DeserializationError)
                        )

                    let ack () = channel.BasicAck(queueMsg.DeliveryTag, false)

                    match domainEvent with
                    | Ok evt ->
                        match! this.handleEvent evt with
                        | Ok(Some payload) ->
                            do! domainEventsHub.Clients.All.SendAsync("Send", payload)
                                |> Async.AwaitTask
                            ack()
                            return ()
                            
                        | Ok None ->
                            ack()
                            return ()
                            
                        | Error e ->
                            logger.LogError(e, "Exception in event consumer handleEvent.")
                            ack()
                            return ()
                    | Error (DeserializationError e) ->
                        logger.LogError(e)
                        ack()
                        return () // error deserializing event
                    | _ -> return () // no message
            }
            Async.Start(consumeTask, cts.Token)

            return ()
        }

        member _.StopAsync(_: CancellationToken) = task {
            logger.LogInformation("Domain event read model consumer shutting down.") // TODO: which one?
            cts.Cancel()
            channel.Close()
            connection.Close()
            return ()
        }
