module Server

open System
open System.Collections.Generic
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http


/// Represents a payment file.
type PaymentFile = {
    Id: Guid
    StorageLink: string
    ReceivedAt: DateTime
    Actor: string
    Source: string
}

/// Available bank channels.
type BankChannel =
    | SWIFT
    | EBICS

/// Fraud check result.
type FraudCheckResult =
    | Passed
    | Failed of string

/// Optimization result.
type OptimizationResult = {
    Optimized: bool
    Details: string
}

/// Metadata for a payment file event.
type PaymentFileEventContext = {
    EventId: Guid
    CreatedAt: DateTime
    PaymentFileId: Guid
    Actor: string
    Source: string
    CorrelationId: Guid option
}

/// Payment events representing workflow steps.
type PaymentEvent =
    | PaymentFileValidated of PaymentFileEventContext * bool
    | BankChannelAssigned of PaymentFileEventContext * BankChannel
    | FraudCheckCompleted of PaymentFileEventContext * FraudCheckResult
    | PaymentOptimized of PaymentFileEventContext * OptimizationResult
    | OptimizedPaymentFileCreated of PaymentFileEventContext * PaymentFile
    | PaymentFileSubmittedToBank of PaymentFileEventContext

module EventStore =
    open System.Collections.Concurrent

    let private store = ConcurrentBag<PaymentEvent>()
    let private eventIndex = Dictionary<Guid, List<PaymentEvent>>()
    let private storeLock = obj()

    let storeEvent (event: PaymentEvent) =
        lock storeLock (fun () ->
            store.Add(event)
            let context =
                match event with
                | PaymentFileValidated (context, _) -> context
                | BankChannelAssigned (context, _) -> context
                | FraudCheckCompleted (context, _) -> context
                | PaymentOptimized (context, _) -> context
                | OptimizedPaymentFileCreated (context, _) -> context
                | PaymentFileSubmittedToBank context -> context

            let correlationId = context.CorrelationId |> Option.defaultValue context.PaymentFileId
            if eventIndex.ContainsKey(correlationId) then
                eventIndex.[correlationId].Add(event)
            else
                eventIndex.[correlationId] <- List<PaymentEvent>([event])
            // Logging the stored event for debugging/tracing purposes.
            printfn "Stored event: %A" event
        )
        event

    /// Retrieves all stored events.
    let getEvents () : PaymentEvent list =
        store |> List.ofSeq

    /// Checks if events exist for a given correlation ID.
    let eventsExistForCorrelationId (correlationId: Guid) : bool =
        eventIndex.ContainsKey(correlationId)

    /// Retrieves events for a given correlation ID.
    let getEventsByCorrelationId (correlationId: Guid) : PaymentEvent list =
        if eventIndex.ContainsKey(correlationId) then
            eventIndex.[correlationId] |> List.ofSeq
        else
            []

/// Module to create event metadata.
module EventContext =
    /// Creates a new event metadata record.
    /// If no correlationId is provided, it defaults to the paymentFileId.
    let create (paymentFileId: Guid) (actor: string) (source: string) (correlationId: Guid option) =
        {
            EventId = Guid.NewGuid()
            CreatedAt = DateTime.UtcNow
            PaymentFileId = paymentFileId
            Actor = actor
            Source = source
            CorrelationId = correlationId
        }

// Command handlers for processing payment file workflow steps.

open EventContext
open EventStore

let validatePaymentFile paymentFileId actor source isValid =
    let metadata = create paymentFileId actor source (Some paymentFileId)
    PaymentFileValidated(metadata, isValid)
    |> storeEvent

let assignBankChannel paymentFileId actor source channel =
    let metadata = create paymentFileId actor source (Some paymentFileId)
    BankChannelAssigned(metadata, channel)
    |> storeEvent

let completeFraudCheck paymentFileId actor source result =
    let metadata = create paymentFileId actor source (Some paymentFileId)
    FraudCheckCompleted(metadata, result)
    |> storeEvent

let optimizePaymentFile paymentFileId actor source optimized details =
    let metadata = create paymentFileId actor source (Some paymentFileId)
    PaymentOptimized(metadata, { Optimized = optimized; Details = details })
    |> storeEvent

let createOptimizedPaymentFile originalPaymentFileId (newPaymentFile: PaymentFile) actor source =
    let metadata = create newPaymentFile.Id actor source (Some originalPaymentFileId)
    OptimizedPaymentFileCreated(metadata, newPaymentFile)
    |> storeEvent

let submitPaymentFileToBank paymentFileId actor source =
    let metadata = create paymentFileId actor source (Some paymentFileId)
    PaymentFileSubmittedToBank(metadata)
    |> storeEvent

/// DTOs for incoming JSON requests.
module Dtos =
    open System

    type ValidatePaymentDto = {
        PaymentFileId: Guid
        Actor: string
        Source: string
        IsValid: bool
    }

    type AssignBankChannelDto = {
        PaymentFileId: Guid
        Actor: string
        Source: string
        BankChannel: string // Expected values: "SWIFT" or "EBICS"
    }

    type FraudCheckDto = {
        PaymentFileId: Guid
        Actor: string
        Source: string
        Passed: bool
        Error: string option
    }

    type OptimizePaymentDto = {
        PaymentFileId: Guid
        Actor: string
        Source: string
        Optimized: bool
        Details: string
    }

    type CreateOptimizedPaymentDto = {
        OriginalPaymentFileId: Guid
        NewPaymentFileId: Guid
        StorageLink: string
        ReceivedAt: DateTime
        Actor: string
        Source: string
    }

    type SubmitPaymentDto = {
        PaymentFileId: Guid
        Actor: string
        Source: string
    }

/// API Controllers for handling HTTP requests.
module PaymentController =
    open Dtos

    let parseBankChannel (channel: string) =
        match channel.ToUpperInvariant() with
        | "SWIFT" -> SWIFT
        | "EBICS" -> EBICS
        | _ -> failwith "Invalid bank channel"

    let validatePaymentHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<ValidatePaymentDto>()
            let event = validatePaymentFile dto.PaymentFileId dto.Actor dto.Source dto.IsValid
            return! json event next ctx
        }

    let assignBankChannelHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<AssignBankChannelDto>()
            let channel = parseBankChannel dto.BankChannel
            let event = assignBankChannel dto.PaymentFileId dto.Actor dto.Source channel
            return! json event next ctx
        }

    let fraudCheckHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<FraudCheckDto>()
            let result =
                if dto.Passed then Passed
                else Failed (defaultArg dto.Error "Fraud check failed")
            let event = completeFraudCheck dto.PaymentFileId dto.Actor dto.Source result
            return! json event next ctx
        }

    let optimizePaymentHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<OptimizePaymentDto>()
            let event = optimizePaymentFile dto.PaymentFileId dto.Actor dto.Source dto.Optimized dto.Details
            return! json event next ctx
        }

    let createOptimizedPaymentHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<CreateOptimizedPaymentDto>()
            let newPaymentFile = { PaymentFile.Id = dto.NewPaymentFileId; StorageLink = dto.StorageLink; ReceivedAt = dto.ReceivedAt; Actor = dto.Actor; Source = dto.Source }
            let event = createOptimizedPaymentFile dto.OriginalPaymentFileId newPaymentFile dto.Actor dto.Source
            return! json event next ctx
        }

    let submitPaymentHandler (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! dto = ctx.BindJsonAsync<SubmitPaymentDto>()
            let event = submitPaymentFileToBank dto.PaymentFileId dto.Actor dto.Source
            return! json event next ctx
        }

    // Endpoint to retrieve events for a given correlation id.
    let getEventsByCorrelationHandler (correlationId: Guid) (next: HttpFunc) (ctx: HttpContext) =
        task {
            let filteredEvents =
                if eventsExistForCorrelationId correlationId then
                    getEventsByCorrelationId correlationId
                else
                    []
            return! json filteredEvents next ctx
        }

let paymentRouter =
    // Combine controllers into a single router.
    choose [
        POST >=> route "/api/commands/validate" >=> PaymentController.validatePaymentHandler
        POST >=> route "/api/commands/assign-bank-channel" >=> PaymentController.assignBankChannelHandler
        POST >=> route "/api/commands/complete-fraud-check" >=> PaymentController.fraudCheckHandler
        POST >=> route "/api/commands/optimize" >=> PaymentController.optimizePaymentHandler
        POST >=> route "/api/commands/create-optimized-payment-file" >=> PaymentController.createOptimizedPaymentHandler
        POST >=> route "/api/commands/submit-to-bank" >=> PaymentController.submitPaymentHandler
        GET  >=> routef "/api/events/correlation/%O" PaymentController.getEventsByCorrelationHandler
    ]

/// Defines the Saturn application with the necessary middleware and routing.
let webApp =
    // Define the web application router.
    paymentRouter

/// Defines the Saturn application with the necessary middleware and routing.
let app = application {
    // Use the defined router for handling HTTP requests.
    use_router webApp
    // Enable in-memory caching.
    memory_cache
    // Serve static files from the "public" directory.
    use_static "public"
    // Enable GZIP compression for responses.
    use_gzip
}

[<EntryPoint>]
let main _ =
    run app
    0
