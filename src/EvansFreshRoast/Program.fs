module EvansFreshRoast.App

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open EvansFreshRoast.HttpHandlers
open System.Text.Json
open NodaTime.Serialization.SystemTextJson
open NodaTime
open EvansFreshRoast.EventConsumers.ReadModels
open EvansFreshRoast.EventConsumers.Sms
open Microsoft.Extensions.Configuration

// ---------------------------------
// Web app
// ---------------------------------

let webApp (compositionRoot: CompositionRoot) =
    choose [ subRoute "/api/v1"
                 (choose [ GET  >=> choose [ route "/hello" >=> handleGetHello
                                             routef "/customers/%O" (getCustomer compositionRoot)
                                             route "/customers" >=> getCustomers compositionRoot ]
                           POST >=> choose [ route "/roasts" >=> handlePostRoast
                                             route "/coffees" >=> handlePostCoffee
                                             routef "/coffees/%O/activate" handleActivateCoffee
                                             route "/customers" >=> postCustomer compositionRoot ] ])
             setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> setStatusCode 500
    >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder: CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:5000", "https://localhost:5001")
        .AllowAnyMethod()
        .AllowAnyHeader()
    |> ignore

let configureApp (compositionRoot: CompositionRoot) (app: IApplicationBuilder) =
    let env =
        app.ApplicationServices.GetService<IWebHostEnvironment>()

    (match env.IsDevelopment() with
     | true -> app.UseDeveloperExceptionPage()
     | false ->
         app
             .UseGiraffeErrorHandler(errorHandler)
             .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseGiraffe(webApp compositionRoot)

let configureServices (services: IServiceCollection) =
    services.AddHostedService<CustomerReadModelConsumer>() |> ignore
    services.AddHostedService<CustomerSmsConsumer>() |> ignore
    services.AddCors() |> ignore
    services.AddGiraffe() |> ignore

    let serializerOptions =
        let opts =
            JsonSerializerOptions().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        opts

    services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(serializerOptions))
    |> ignore

    ()

let configureLogging (builder: ILoggingBuilder) =
    builder.AddConsole().AddDebug() |> ignore

let configureSettings (configurationBuilder: IConfigurationBuilder) =
    configurationBuilder
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional=false)

[<EntryPoint>]
let main args =
    let confBuilder = configureSettings <| ConfigurationBuilder()
    let settings = confBuilder.Build().Get<Settings>()
    let compositionRoot = CompositionRoot.compose settings

    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .Configure(Action<IApplicationBuilder> (configureApp compositionRoot))
                .ConfigureServices(Action<IServiceCollection> configureServices)
                .ConfigureLogging(configureLogging)
            |> ignore)
        .Build()
        .Run()

    0
