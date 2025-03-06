module Index

open Elmish
open Feliz
open System
open Fable.Remoting.Client
open Shared

// Payment Simulation Domain Types


let initialPaymentState: PaymentState = {
    PaymentFile = None
    IsValid = None
    BankChannel = None
    FraudCheckResult = None
    OptimizationResult = None
    OptimizedPaymentFile = None
    OptimizedPaymentFileSubmittedAt = None
}

// Define common metadata for all payment events


let moveFileToFolder content =
    // Simulate moving the file to a folder and returning the new link
    "/uploads/" + Guid.NewGuid().ToString()

let receiveFile content =
    let link = moveFileToFolder content

    let paymentFile = {
        Id = Guid.NewGuid()
        StorageLink = link
        ReceivedAt = DateTime.UtcNow
        Actor = "System"
        Source = "WebApp"
    }

    PaymentFileReceived(
        {
            EventId = Guid.NewGuid()
            CreatedAt = DateTime.UtcNow
            PaymentFileId = paymentFile.Id
            Actor = "System"
            Source = "WebApp"
            CorrelationId = None
        },
        paymentFile
    )

let processPaymentFile paymentFile: PaymentState * PaymentFileEvent list =
    let optimisationOutputFolder = "/optimized"

    let paymentFileOptimized: PaymentFile = {
        Id = Guid.NewGuid()
        StorageLink = optimisationOutputFolder + "/" + paymentFile.Id.ToString()
        ReceivedAt = DateTime.UtcNow
        Actor = "System"
        Source = "WebApp"
    }

    let events = [
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
            BankChannel.SWIFT
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
            FraudCheckResult.Passed
        )
        PaymentFileOptimized(
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
            (fun (state: PaymentState) event ->
                match event with
                | PaymentFileValidated(_, isValid) -> { state with IsValid = Some isValid }
                | BankChannelAssigned(_, channel) -> { state with BankChannel = Some channel }
                | FraudCheckCompleted(_, result) -> { state with FraudCheckResult = Some result }
                | PaymentFileOptimized(_, result) -> { state with OptimizationResult = Some result }
                | OptimizedPaymentFileCreated(_, newFile) -> { state with OptimizedPaymentFile = Some newFile }
                | PaymentFileSubmittedToBank _ -> { state with OptimizedPaymentFileSubmittedAt = Some DateTime.UtcNow }
                | _ -> state
            )
            initialPaymentState

    finalState, events

let getEventTimestamp (event: PaymentFileEvent) =
    match event with
    | PaymentFileReceived(metadata, _) -> metadata.CreatedAt
    | PaymentFileValidated(metadata, _) -> metadata.CreatedAt
    | BankChannelAssigned(metadata, _) -> metadata.CreatedAt
    | FraudCheckCompleted(metadata, _) -> metadata.CreatedAt
    | PaymentFileOptimized(metadata, _) -> metadata.CreatedAt
    | OptimizedPaymentFileCreated(metadata, _) -> metadata.CreatedAt
    | PaymentFileSubmittedToBank metadata -> metadata.CreatedAt

let getEventDescription (event: PaymentFileEvent) =
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
    | PaymentFileOptimized(metaData, result) ->
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
    PaymentFileEvents: PaymentFileEvent list
    CorrelationIds: Guid list
    SelectedCorrelationId: Guid option
}

let init () =
    {
        PaymentSim = None
        PaymentFileEvents = []
        CorrelationIds = []
        SelectedCorrelationId = None
    },
    Cmd.none

type Msg =
    | UploadPaymentFile of string
    | StartProcessing of PaymentFile
    | Reset
    | FetchCorrelationIds
    | SetCorrelationIds of Guid list
    | SelectCorrelationId of Guid
    | FetchEvents of Guid
    | SetEvents of PaymentFileEvent list

let api = Remoting.createApi()
            |> Remoting.withRouteBuilder (fun typeName methodName -> sprintf "/api/%s/%s" typeName methodName)
            |> Remoting.buildProxy<IPaymentFileApi>

let update msg model =
    match msg with
    | UploadPaymentFile content ->
        // Move the file to a folder
        let link = "/uploads/" + Guid.NewGuid().ToString()

        let event = receiveFile content
        let finalState = { initialPaymentState with PaymentFile = Some (match event with | PaymentFileReceived(_, file) -> file | _ -> failwith "Unexpected event") }
        {
            model with
                PaymentSim = Some finalState
                PaymentFileEvents = [event]
        },
        Cmd.none
    | StartProcessing paymentFile ->
        let finalState, events = processPaymentFile paymentFile
        {
            model with
                PaymentSim = Some finalState
                PaymentFileEvents = events
        },
        Cmd.none
    | Reset ->
        init ()
    | FetchCorrelationIds ->
        let cmd = Cmd.OfAsync.perform api.getCorrelationIds () SetCorrelationIds
        model, cmd
    | SetCorrelationIds ids ->
        { model with CorrelationIds = ids }, Cmd.none
    | SelectCorrelationId id ->
        { model with SelectedCorrelationId = Some id }, Cmd.ofMsg (FetchEvents id)
    | FetchEvents id ->
        let cmd = Cmd.OfAsync.perform (fun () -> api.getEventsByCorrelationId id) () SetEvents
        model, cmd
    | SetEvents (events: PaymentFileEvent list) ->
        { model with PaymentFileEvents = events }, Cmd.none

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

            Html.button [
                prop.className "bg-blue-500 text-white px-4 py-2 rounded"
                prop.text "Fetch Correlation IDs"
                prop.onClick (fun _ -> dispatch FetchCorrelationIds)
            ]

            Html.h2 [
                prop.className "text-xl font-semibold text-gray-700 mt-6 mb-2"
                prop.text "Correlation IDs"
            ]

            Html.ul [
                prop.className "bg-gray-100 p-4 rounded-lg"
                prop.children (
                    model.CorrelationIds
                    |> List.map (fun id ->
                        Html.li [
                            prop.className "py-2 border-b border-gray-300 text-gray-700"
                            prop.text (id.ToString())
                            prop.onClick (fun _ -> dispatch (SelectCorrelationId id))
                        ]
                    )
                )
            ]

            Html.h2 [
                prop.className "text-xl font-semibold text-gray-700 mt-6 mb-2"
                prop.text "Event Timeline"
            ]

            Html.ul [
                prop.className "bg-gray-100 p-4 rounded-lg"
                prop.children (
                    model.PaymentFileEvents
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
