namespace EvansFreshRoast

open Microsoft.Extensions.Hosting
open RabbitMQ.Client
open System.Text
open Thoth.Json.Net
open EvansFreshRoast.Serialization.DomainEvents
open EvansFreshRoast.Serialization.Customer
open EvansFreshRoast.Utils
open EvansFreshRoast.Framework
open System.Threading
open System.Threading.Tasks
open EvansFreshRoast.ReadModels
open Microsoft.Extensions.Logging

type RabbitMqConsumerError =
    | NoMessageAvailable
    | DeserializationError of string

type CustomerReadModelConsumer (logger: ILogger<CustomerReadModelConsumer>) =
    let exchangeName = "domain.events"
    let route = "domain.events.customer"
    let queueName = "domain.events.customer.readModel"

    let connectionString =
        // "Host=readmodelsdb;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        "Host=localhost;Port=2345;Database=evans_fresh_roast_reads;Username=read_models_user;Password=read_models_pass;"
        |> ConnectionString.create

    let connectionFactory = ConnectionFactory(
        HostName = "localhost", //"rabbitmq",
        UserName = "guest",
        Password = "guest",
        Port = 5672)
    let connection = connectionFactory.CreateConnection()
    let channel = connection.CreateModel()

    let cts = new CancellationTokenSource()

    interface IHostedService with
        member _.StartAsync(ct: CancellationToken) =
            task {
                logger.LogInformation("Customer domain event read model consumer starting up.")

                channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true, false, null)
                channel.QueueDeclare(queueName, true, false, false, null) |> ignore
                channel.QueueBind(queueName, exchangeName, route, null)

                let consumeTask =
                    async {
                        while not cts.Token.IsCancellationRequested do
                            do! Task.Delay(500, cts.Token) |> Async.AwaitTask

                            let queueMsg = channel.BasicGet(queueName, false)

                            let domainEvent =
                                queueMsg
                                |> Option.ofObj
                                |> Result.ofOption NoMessageAvailable
                                |> Result.map (fun msg -> Encoding.UTF8.GetString msg.Body.Span)
                                |> Result.bind (
                                    Decode.fromString (decodeDomainEvent decodeCustomerEvent)
                                    >> (Result.mapError DeserializationError)
                                )

                            match domainEvent with
                            | Ok evt ->
                                logger.LogInformation("RabbitMQ message received")

                                match! Customer.updateReadModel connectionString evt with
                                | Ok () ->
                                    logger.LogInformation("RabbitMQ message handled successfully.")
                                    channel.BasicAck(queueMsg.DeliveryTag, false)
                                    return ()
                                | Error e ->
                                    logger.LogError(e) // error updating read model in db
                                    channel.BasicAck(queueMsg.DeliveryTag, false)
                                    return ()
                            | Error (DeserializationError e) ->
                                logger.LogError(e)
                                channel.BasicAck(queueMsg.DeliveryTag, false)
                                return () // error deserializing event
                            | _ -> return ()
                    }
                Async.Start(consumeTask, cts.Token)

                return ()
            }

        member _.StopAsync(_: CancellationToken) =
            task {
                logger.LogInformation("Customer domain event read model consumer shutting down.")
                cts.Cancel()
                channel.Close()
                connection.Close()
                return ()
            }
