module Server

open System
open System.Collections.Generic
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.OpenApi.Models
open Swashbuckle.AspNetCore.SwaggerGen
open Shared

module EventStore =
    open System.Collections.Concurrent

    let private store = ConcurrentBag<PaymentFileEvent>()
    let private eventIndex = ConcurrentDictionary<Guid, ConcurrentBag<PaymentFileEvent>>()

    let storeEvent (event: PaymentFileEvent) =
        store.Add(event)

        let context =
            match event with
            | PaymentFileReceived(context, _) -> context
            | PaymentFileValidated(context, _) -> context
            | BankChannelAssigned(context, _) -> context
            | FraudCheckCompleted(context, _) -> context
            | PaymentFileOptimized(context, _) -> context
            | OptimizedPaymentFileCreated(context, _) -> context
            | PaymentFileSubmittedToBank context -> context

        let correlationId =
            context.CorrelationId |> Option.defaultValue context.PaymentFileId

        eventIndex.AddOrUpdate(
            correlationId,
            (fun _ -> ConcurrentBag<PaymentFileEvent>([ event ])),
            (fun _ events -> events.Add(event); events)
        ) |> ignore
        event

    let getEvents () : IEnumerable<PaymentFileEvent> = store :> IEnumerable<PaymentFileEvent>

    // Fetches events from the event store by correlation ID.
    // Returns a list of PaymentFileEvent associated with the given correlation ID.
    let getEventsByCorrelationId (correlationId: Guid) : Async<PaymentFileEvent list> =
        async {
            return eventIndex.TryGetValue(correlationId)
                   |> function
                       | true, events -> List.ofSeq events
                       | _ -> []
        }

    // Fetches a list of distinct correlation IDs from the event store.
    // Correlation IDs are extracted from various types of payment file events.
    let getCorrelationIds () =
        async {
            // Fetch correlation IDs from the event store
            let correlationIds = getEvents()
                                |> Seq.toList
                                |> List.choose (fun event ->
                                    match event with
                                    | PaymentFileValidated(metadata, _) -> metadata.CorrelationId
                                    | BankChannelAssigned(metadata, _) -> metadata.CorrelationId
                                    | FraudCheckCompleted(metadata, _) -> metadata.CorrelationId
                                    | PaymentFileOptimized(metadata, _) -> metadata.CorrelationId
                                    | OptimizedPaymentFileCreated(metadata, _) -> metadata.CorrelationId
                                    | PaymentFileSubmittedToBank metadata -> metadata.CorrelationId
                                    | _ -> None)
                                |> List.distinct
            return correlationIds
        }

    // Receives a payment file and stores the received event in the event store.
    // The function creates a `PaymentFileReceived` event with the provided DTO and stores it.
    // The event includes metadata such as EventId, CreatedAt, PaymentFileId, Actor, Source, and CorrelationId.
    // The function returns an async unit.
    let receivePaymentFile (dto: ReceivePaymentFileDto) =
        async {
            let event =
                storeEvent (
                    PaymentFileReceived(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFile.Id
                            Actor = dto.PaymentFile.Actor
                            Source = dto.PaymentFile.Source
                            CorrelationId = Some dto.PaymentFile.Id
                        },
                        dto.PaymentFile
                    )
                )
            return ()
        }

    // Validates a payment file and stores the validation event in the event store.
    // The function creates a `PaymentFileValidated` event with the provided DTO and stores it.
    // The event includes metadata such as EventId, CreatedAt, PaymentFileId, Actor, Source, and CorrelationId.
    // The function returns an async unit.
    let validatePaymentFile (dto: ValidatePaymentFileDto) =
        async {
            let event =
                storeEvent (
                    PaymentFileValidated(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        dto.IsValid
                    )
                )
            return ()
        }

    // Assigns a bank channel to a payment file.
    // Stores a `BankChannelAssigned` event in the event store.
    // The bank channel is determined based on the `BankChannel` property of the provided DTO.
    let assignBankChannel (dto : AssignBankChannelDto) =
        async {
            let event =
                storeEvent (
                    BankChannelAssigned(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        if dto.BankChannel = "SWIFT" then BankChannel.SWIFT else BankChannel.EBICS
                    )
                )
            return ()
        }

    // Optimizes a payment file and stores the optimization event in the event store.
    // The function creates a `PaymentOptimized` event with the provided DTO and stores it.
    // The event includes metadata such as EventId, CreatedAt, PaymentFileId, Actor, Source, and CorrelationId.
    // The function returns an async unit.
    let optimizePaymentFile (dto: OptimizePaymentFileDto) =
        async {
            let event =
                let optimizationResult = { Optimized = dto.Optimized; Details = dto.Details }
                storeEvent (
                    PaymentFileOptimized(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        optimizationResult
                    )
                )
            return ()
        }

    // Completes a fraud check for a payment file and stores the result in the event store.
    // The function creates a `FraudCheckCompleted` event with the provided DTO and stores it.
    // The event includes metadata such as EventId, CreatedAt, PaymentFileId, Actor, Source, and CorrelationId.
    // The function returns an async unit.
    let completeFraudCheck (dto : CompleteFraudCheckDto) =
        async {
            let result =
                if dto.Passed then
                    Passed
                else
                    Failed(defaultArg dto.Error "Fraud check failed")

            let event =
                storeEvent (
                    FraudCheckCompleted(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        result
                    )
                )
            return ()
        }

    // Creates an optimized payment file and stores the event in the event store.
    // The function creates an `OptimizedPaymentFileCreated` event with the provided DTO and stores it.
    // The event includes metadata such as EventId, CreatedAt, PaymentFileId, Actor, Source, and CorrelationId.
    // The function returns an async unit.
    let createOptimizedPaymentFile (dto: CreateOptimizedPaymentFileDto) =
        async {
            let event =
                let newStorageLink = "Path/To"
                let newPaymentFile = {
                    Id = dto.NewPaymentFileId
                    StorageLink = newStorageLink
                    ReceivedAt = DateTime.UtcNow
                    Actor = "System"
                    Source = "OptimizationResult"
                }
                storeEvent (
                    OptimizedPaymentFileCreated(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.NewPaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = Some dto.OriginalPaymentFileId
                        },
                        newPaymentFile
                    )
                )
            return ()
        }

    // Submits a payment file to the bank and stores the event in the event store.
    // The function creates a `PaymentFileSubmittedToBank` event with the provided DTO and stores it.
    // The event includes metadata such as EventId, CreatedAt, PaymentFileId, Actor, Source, and CorrelationId.
    // The function returns an async unit.
    let submitPaymentFile (dto: SubmitPaymentFileDto) =
        async {
            let event =
                storeEvent (
                    PaymentFileSubmittedToBank(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        }
                    )
                )
            return ()
        }

    let paymentFileApi =
        {
            getCorrelationIds = getCorrelationIds
            getEventsByCorrelationId = getEventsByCorrelationId
            receivePaymentFile = receivePaymentFile
            validatePaymentFile = validatePaymentFile
            assignBankChannel = assignBankChannel
            completeFraudCheck = completeFraudCheck
            optimizePaymentFile = optimizePaymentFile
            createOptimizedPaymentFile = createOptimizedPaymentFile
            submitPaymentFile = submitPaymentFile
        }

    // The PaymentController module handles HTTP requests related to payment file operations.
    // It includes handlers for validating payment files, assigning bank channels, and completing fraud checks.
    module PaymentController =

        // Handles HTTP POST requests to validate a payment file.
        // Binds the request body to a ValidatePaymentFileDto and stores a PaymentFileValidated event.
        let validatePaymentHandler (next: HttpFunc) (ctx: HttpContext) = task {
            let! (dto: ValidatePaymentFileDto) = ctx.BindJsonAsync<Shared.ValidatePaymentFileDto>()

            let event =
                storeEvent (
                    PaymentFileValidated(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        dto.IsValid
                    )
                )
            return! json event next ctx
        }

        // Handles HTTP POST requests to assign a bank channel to a payment file.
        // Binds the request body to an AssignBankChannelDto and stores a BankChannelAssigned event.
        let assignBankChannelHandler (next: HttpFunc) (ctx: HttpContext) = task {
            let! dto = ctx.BindJsonAsync<AssignBankChannelDto>()

            let event =
                storeEvent (
                    BankChannelAssigned(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        if dto.BankChannel = "SWIFT" then SWIFT else EBICS
                    )
                )
            return! json event next ctx
        }

        // Handles HTTP POST requests to complete a fraud check for a payment file.
        // Binds the request body to a CompleteFraudCheckDto and stores a FraudCheckCompleted event.
        let completeFraudCheckHandler (next: HttpFunc) (ctx: HttpContext) = task {
            let! dto = ctx.BindJsonAsync<CompleteFraudCheckDto>()

            let result =
                if dto.Passed then
                    Passed
                else
                    Failed(defaultArg dto.Error "Fraud check failed")

            let event =
                storeEvent (
                    FraudCheckCompleted(
                        {
                            EventId = Guid.NewGuid()
                            CreatedAt = DateTime.UtcNow
                            PaymentFileId = dto.PaymentFileId
                            Actor = dto.Actor
                            Source = dto.Source
                            CorrelationId = None
                        },
                        result
                    )
                )
            return! json event next ctx
        }

    // Defines the routes for the Payment File API.
    // - GET "/" returns a welcome message.
    // - POST "/api/commands/validate" validates a payment file.
    // - POST "/api/commands/assign-bank-channel" assigns a bank channel to a payment file.
    // - POST "/api/commands/complete-fraud-check" completes a fraud check for a payment file.
    let paymentRouter =
        choose [
            GET >=> route "/" >=> text "Welcome to the Payment File API"
            POST >=> route "/api/commands/validate" >=> PaymentController.validatePaymentHandler
            POST >=> route "/api/commands/assign-bank-channel" >=> PaymentController.assignBankChannelHandler
            POST >=> route "/api/commands/complete-fraud-check" >=> PaymentController.completeFraudCheckHandler
        ]

    // Swagger configuration
    let configureSwagger (swaggerGenOptions: SwaggerGenOptions) =
        let info = OpenApiInfo()
        info.Title <- "Payment API"
        info.Version <- "v1"
        swaggerGenOptions.SwaggerDoc("v1", info) |> ignore

    // Defines the main application configuration.
    // - Sets up the router for handling HTTP requests.
    // - Configures static file serving and gzip compression.
    // - Registers services including Swagger and the PaymentFileApi.
    // - Configures middleware for Swagger.
    let app = application {
        use_router paymentRouter
        use_static "public"
        use_gzip

        // Register Swagger in the DI container
        service_config (fun services ->
//             services.AddSwaggerGen(configureSwagger) |> ignore

            // Register the PaymentFileApi service
            services.AddSingleton<IPaymentFileApi>(paymentFileApi) |> ignore

            services) // Return the services instance here

        // Configure middleware for Swagger
//         app_config (fun appBuilder ->
//                 appBuilder.UseSwagger() |> ignore
//                 appBuilder.UseSwaggerUI(fun c ->
//                         c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment API v1")
//                         c.RoutePrefix <- "swagger") |> ignore
//                 appBuilder) // Return the appBuilder instance here
    }

    [<EntryPoint>]
    let main _ =
        run app
        0