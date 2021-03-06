namespace EvansFreshRoast.Api

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open System.Text.Json
open EvansFreshRoast.Api.EventConsumers.ReadModels
open EvansFreshRoast.Api.EventConsumers.Sms
open Microsoft.Extensions.Configuration
open EvansFreshRoast.Api.Composition
open EvansFreshRoast.Api
open RabbitMQ.Client
open Microsoft.IdentityModel.Tokens
open System.Text
open EvansFreshRoast.Api.HttpHandlers

module Program =
    // ---------------------------------
    // Web app
    // ---------------------------------
    let webApp (compositionRoot: CompositionRoot) =
        choose [
            subRoute "/api/v1/coffees" (authenticate >=> Coffees.Router.router compositionRoot)
            subRoute "/api/v1/customers" (authenticate >=> Customers.Router.router compositionRoot)
            subRoute "/api/v1/roasts" (authenticate >=> Roasts.Router.router compositionRoot)
            subRoute "/api/v1/sms" (Sms.Router.router compositionRoot)
            subRoute "/api/v1/auth" (Auth.Router.router compositionRoot)
            routeCix "(/?)" >=> htmlFile "wwwroot/index.html"
            setStatusCode 404 >=> text "Not Found"
        ]

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

        Console.WriteLine($"Is Development Env: %A{env.IsDevelopment()}")
        
        (match env.IsDevelopment() with
        | true -> app.UseDeveloperExceptionPage()
        | false ->
            app
                .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection()
                .UseHsts())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseAuthentication()
            .UseRouting()
            .UseEndpoints(fun endpoints ->
                endpoints.MapHub<DomainEventsHub>("/api/v1/ws/domain-events") |> ignore)
            .UseGiraffe(webApp compositionRoot)

    let configureServices (settings: Settings) (compositionRoot: CompositionRoot) (services: IServiceCollection) =
        services.AddSingleton<CompositionRoot>(compositionRoot) |> ignore
        services.AddSingleton<IConnectionFactory>(fun sp ->
            sp.GetRequiredService<CompositionRoot>().RabbitMqConnectionFactory) |> ignore
        services.AddHostedService<CustomerReadModelConsumer>() |> ignore
        services.AddHostedService<CoffeeReadModelConsumer>() |> ignore
        services.AddHostedService<RoastReadModelConsumer>() |> ignore
        services.AddHostedService<CustomerSmsConsumer>() |> ignore
        services.AddHostedService<RoastSmsConsumer>() |> ignore
        services.AddCors() |> ignore
        services.AddGiraffe() |> ignore
        services.AddSignalR() |> ignore

        let serializerOptions =
            let opts = JsonSerializerOptions()
            opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
            opts

        services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(serializerOptions))
        |> ignore

        services
            .AddAuthentication(fun opt ->
                opt.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun opt ->
                let key = SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Jwt.SecretKey))

                opt.TokenValidationParameters <- TokenValidationParameters(
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = settings.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Jwt.Audience)

                let events = JwtBearerEvents()
                events.OnMessageReceived <- fun ctx ->
                    task {
                        let token =
                            ctx.Request.Cookies["efr.auth.token"]
                            |> Option.ofObj
                            |> Option.defaultValue (
                                ctx.Request.Headers["Authentication"].ToArray()
                                |> Array.tryHead
                                |> Option.defaultValue ""
                            )
                        
                        ctx.Token <- token
                    }
                opt.Events <- events
            ) |> ignore
        ()

    let configureLogging (builder: ILoggingBuilder) =
        builder.AddConsole().AddDebug() |> ignore

    let configureSettings (configurationBuilder: IConfigurationBuilder) =
        configurationBuilder
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional=false)
            .AddEnvironmentVariables()

    [<EntryPoint>]
    let main args =
        let confBuilder = configureSettings <| ConfigurationBuilder()
        let settings = confBuilder.Build().Get<Settings>()
        let compositionRoot = CompositionRoot.compose settings

        if compositionRoot.TwilioAccountSid <> "" then
            Twilio.TwilioClient.Init(settings.Twilio.AccountSid, settings.Twilio.AuthToken)
        else
            ()

        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot = Path.Combine(contentRoot, "wwwroot")

        Host
            .CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> (configureApp compositionRoot))
                    .ConfigureServices(Action<IServiceCollection> (configureServices settings compositionRoot))
                    .ConfigureLogging(configureLogging)
                |> ignore)
            .Build()
            .Run()

        0
