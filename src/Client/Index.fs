module Index

open Elmish
open Feliz
open System
open Fable.Remoting.Client
open Shared

// Payment File Simulation Domain Types

let initialPaymentFileState: PaymentFileState = {
    PaymentFile = None
    IsValid = None
    BankChannel = None
    FraudCheckResult = None
    OptimizationResult = None
    OptimizedPaymentFile = None
    OptimizedPaymentFileSubmittedAt = None
}

let moveFileToFolder content =
    // Simulate moving the file to a folder and returning the new link
    "/uploads/" + Guid.NewGuid().ToString()

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
    UploadedPaymentFile: PaymentFile option
    PaymentFileEvents: PaymentFileEvent list
    CorrelationIds: Guid list
    SelectedCorrelationId: Guid option
}

let init () =
    {
        UploadedPaymentFile = None
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

let routeBuilder typeName methodName =
    sprintf "/api/%s/%s" typeName methodName

let api = Remoting.createApi()
            |> Remoting.withRouteBuilder routeBuilder
            |> Remoting.buildProxy<IPaymentFileApi>

let update msg model =
    match msg with
    | UploadPaymentFile content ->
        // Move the file to a folder
        let link = "/uploads/" + Guid.NewGuid().ToString()
        let paymentFile = {
            Id = Guid.NewGuid()
            StorageLink = link
            ReceivedAt = DateTime.UtcNow
            Actor = "Andreas Progorovics"
            Source = "UI"
        }
        let model = { model with UploadedPaymentFile = Some paymentFile }
        model, Cmd.none
    | StartProcessing paymentFile ->
        let model = { model with UploadedPaymentFile = Some paymentFile }
        model, Cmd.none
    | Reset ->
        init ()
    | FetchCorrelationIds ->
        model, Cmd.OfAsync.perform api.getCorrelationIds () SetCorrelationIds
    | SetCorrelationIds ids ->
        { model with CorrelationIds = ids }, Cmd.none
    | SelectCorrelationId id ->
        { model with SelectedCorrelationId = Some id }, Cmd.none
    | FetchEvents id ->
        model, Cmd.OfAsync.perform api.getEventsByCorrelationId id SetEvents
    | SetEvents events ->
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
