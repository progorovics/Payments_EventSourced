module Index

open Elmish
open Feliz
open System

// Payment Simulation Domain Types

type PaymentFile = {
    Id: Guid
    Content: string
    CustomerId: Guid
    ReceivedAt: DateTime
}

type BankChannel =
    | SWIFT
    | EBICS

type FraudCheckResult =
    | Passed
    | Failed of string

type OptimizationResult = { Optimized: bool; Details: string }

type PaymentState = {
    PaymentFile: PaymentFile option
    IsValid: bool option
    BankChannel: BankChannel option
    FraudResult: FraudCheckResult option
    OptimizationResult: OptimizationResult option
    OptimizedPaymentFile: PaymentFile option
    OptimizedPaymentFileSubmittedAt: DateTime option
}

let initialPaymentState: PaymentState = {
    PaymentFile = None
    IsValid = None
    BankChannel = None
    FraudResult = None
    OptimizationResult = None
    OptimizedPaymentFile = None
    OptimizedPaymentFileSubmittedAt = None
}

// Define common metadata for all payment events
type PaymentEventMetadata = {
    EventId: Guid
    CreatedAt: DateTime
    PaymentFileId: Guid
    Actor: string
    Source: string
    CorrelationId: Guid option
}

type PaymentEvent =
    | PaymentFileReceived of PaymentEventMetadata * PaymentFile
    | PaymentFileValidated of PaymentEventMetadata * bool
    | BankChannelAssigned of PaymentEventMetadata * BankChannel
    | FraudCheckCompleted of PaymentEventMetadata * FraudCheckResult
    | PaymentOptimized of PaymentEventMetadata * OptimizationResult
    | OptimizedPaymentFileCreated of PaymentEventMetadata * PaymentFile
    | PaymentFileSubmittedToBank of PaymentEventMetadata

let processPaymentFile content =
    let customerId = Guid.NewGuid()

    let paymentFile = {
        Id = Guid.NewGuid()
        Content = content
        CustomerId = customerId
        ReceivedAt = DateTime.UtcNow
    }

    let paymentFileOptimized = {
        Id = Guid.NewGuid()
        Content = content + " (Optimized)"
        CustomerId = customerId
        ReceivedAt = DateTime.UtcNow
    }

    let events = [
        PaymentFileReceived(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFile.Id
                Actor = "PublicAPI"
                Source = "API"
                CorrelationId = Some paymentFile.Id
            },
            paymentFile
        )
        PaymentFileValidated(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFile.Id
                Actor = "UserEsther"
                Source = "UI"
                CorrelationId = Some paymentFile.Id
            },
            true
        )
        BankChannelAssigned(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFile.Id
                Actor = "System"
                Source = "WebApp"
                CorrelationId = Some paymentFile.Id
            },
            SWIFT
        )
        FraudCheckCompleted(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFile.Id
                Actor = "System"
                Source = "WebApp"
                CorrelationId = Some paymentFile.Id
            },
            Passed
        )
        PaymentOptimized(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFile.Id
                Actor = "System"
                Source = "WebApp"
                CorrelationId = Some paymentFile.Id
            },
            {
                Optimized = true
                Details = "Payment file optimized successfully."
            }
        )
        OptimizedPaymentFileCreated(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFileOptimized.Id
                Actor = "System"
                Source = "AutomationJob"
                CorrelationId = Some paymentFile.Id
            },
            paymentFileOptimized
        )
        PaymentFileSubmittedToBank(
            {
                EventId = Guid.NewGuid()
                CreatedAt = DateTime.UtcNow
                PaymentFileId = paymentFileOptimized.Id
                Actor = "System"
                Source = "WebApp"
                CorrelationId = Some paymentFile.Id
            }
        )
    ]

    let finalState =
        events
        |> List.fold
            (fun state evt ->
                match evt with
                | PaymentFileReceived(p, file) -> { state with PaymentFile = Some file }
                | PaymentFileValidated(_, isValid) -> { state with IsValid = Some isValid }
                | BankChannelAssigned(_, channel) -> {
                    state with
                        BankChannel = Some channel
                  }
                | FraudCheckCompleted(_, result) -> { state with FraudResult = Some result }
                | PaymentOptimized(_, result) -> {
                    state with
                        OptimizationResult = Some result
                  }
                | OptimizedPaymentFileCreated(_, newFile) -> {
                    state with
                        OptimizedPaymentFile = Some newFile
                  }
                | PaymentFileSubmittedToBank metadata -> {
                    state with
                        OptimizedPaymentFileSubmittedAt = Some metadata.CreatedAt
                  })
            initialPaymentState

    finalState, events


let getEventTimestamp (event: PaymentEvent) =
    match event with
    | PaymentFileReceived(metadata, _) -> metadata.CreatedAt
    | PaymentFileValidated(metadata, _) -> metadata.CreatedAt
    | BankChannelAssigned(metadata, _) -> metadata.CreatedAt
    | FraudCheckCompleted(metadata, _) -> metadata.CreatedAt
    | PaymentOptimized(metadata, _) -> metadata.CreatedAt
    | OptimizedPaymentFileCreated(metadata, _) -> metadata.CreatedAt
    | PaymentFileSubmittedToBank metadata -> metadata.CreatedAt

let getEventDescription (event: PaymentEvent) =
    match event with
    | PaymentFileReceived(metaData, _) ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + "Payment file imported"
    | PaymentFileValidated(metaData, isValid) ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + sprintf "Payment file validated: %b" isValid
    | BankChannelAssigned(metaData, channel) ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + sprintf "Payment file bank channel assigned: %A" channel
    | FraudCheckCompleted(metaData, result) ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + sprintf "Payment file fraud check completed: %A" result
    | PaymentOptimized(metaData, result) ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + sprintf "Payment file optimized: %s" result.Details
    | OptimizedPaymentFileCreated(metaData, newFile) ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + "Payment file optimized payment file created: "
        + newFile.Id.ToString()
    | PaymentFileSubmittedToBank metaData ->
        "CorrelationId: "
        + metaData.CorrelationId.Value.ToString()
        + " ==> "
        + "Payment file optimized payment file submitted to bank"

// Model, Messages, Init, and Update

type Model = {
    PaymentSim: PaymentState option
    PaymentEvents: PaymentEvent list
}

let init () =
    {
        PaymentSim = None
        PaymentEvents = []
    },
    Cmd.none

type Msg =
    | UploadPaymentFile of string
    | Reset

let update msg model =
    match msg with
    | UploadPaymentFile content ->
        let finalState, events = processPaymentFile content

        {
            model with
                PaymentSim = Some finalState
                PaymentEvents = events
        },
        Cmd.none
    | Reset ->
        init ()

// View: UI
let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto p-6 bg-white shadow-lg rounded-lg"
        prop.children [
            Html.h1 [
                prop.className "text-2xl font-bold text-gray-800 mb-4"
                prop.text "Upload a Payment File"
            ]

            Html.input [
                prop.className "w-full border border-gray-300 rounded-lg p-2 mb-4"
                prop.type' "file"
                prop.onChange (fun (e: Browser.Types.Event) ->
                    let input = e.target :?> Browser.Types.HTMLInputElement
                    let files = input.files

                    dispatch Reset |> ignore  // Explicitly ignore the result

                    if not (isNull files) && files.length > 0 then
                        let file = files.item(0)

                        if not (isNull file) then
                            let allowedTypes = [ "text/plain"; "application/json"; "text/xml" ]

                            if List.contains file.``type`` allowedTypes then
                                let reader = Browser.Dom.FileReader.Create()
                                reader.onload <- fun _ ->
                                    let content = reader.result |> string
                                    dispatch (UploadPaymentFile content) |> ignore  // Explicitly ignore
                                reader.readAsText file
                            else
                                Browser.Dom.window.alert ($"Unsupported file type {file.``type``}. Please upload a plain text, JSON, or XML file.") |> ignore  // Explicitly ignore
                )
            ]

            Html.h2 [
                prop.className "text-xl font-semibold text-gray-700 mt-6 mb-2"
                prop.text "Event Timeline"
            ]

            Html.ul [
                prop.className "bg-gray-100 p-4 rounded-lg"
                prop.children (
                    model.PaymentEvents
                    |> List.map (fun event ->
                        Html.li [
                            prop.className "py-2 border-b border-gray-300 text-gray-700"
                            prop.text (
                                (getEventTimestamp event).ToString("yyyy-MM-dd HH:mm:ss") + ": " + getEventDescription event
                            )
                        ]
                    )
                )
            ]
        ]
    ]
