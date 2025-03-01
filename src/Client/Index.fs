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

let initialPaymentState : PaymentState = {
    PaymentFile = None
    IsValid = None
    BankChannel = None
    FraudResult = None
    OptimizationResult = None
    OptimizedPaymentFile = None
    OptimizedPaymentFileSubmittedAt = None
}

type PaymentEvent =
    | PaymentFileReceived of PaymentFile * DateTime
    | PaymentFileValidated of Guid * bool * DateTime
    | BankChannelAssigned of Guid * BankChannel * DateTime
    | FraudCheckCompleted of Guid * FraudCheckResult * DateTime
    | PaymentOptimized of Guid * OptimizationResult * DateTime
    | OptimizedPaymentFileCreated of Guid * PaymentFile * DateTime
    | PaymentFileSubmittedToBank of Guid * DateTime

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
        PaymentFileReceived (paymentFile, DateTime.UtcNow)
        PaymentFileValidated (paymentFile.Id, true, DateTime.UtcNow.AddSeconds(1.0))
        BankChannelAssigned (paymentFile.Id, SWIFT, DateTime.UtcNow.AddSeconds(2.0))
        FraudCheckCompleted (paymentFile.Id, Passed, DateTime.UtcNow.AddSeconds(3.0))
        PaymentOptimized (paymentFile.Id, { Optimized = true; Details = "Payment optimized successfully." }, DateTime.UtcNow.AddSeconds(4.0))
        OptimizedPaymentFileCreated (paymentFile.Id, paymentFileOptimized, DateTime.UtcNow.AddSeconds(5.0))
        PaymentFileSubmittedToBank (paymentFileOptimized.Id, DateTime.UtcNow.AddSeconds(6.0))
    ]

    let finalState =
        events
        |> List.fold (fun state evt ->
            match evt with
            | PaymentFileReceived (p, _) -> { state with PaymentFile = Some p }
            | PaymentFileValidated (_, isValid, _) -> { state with IsValid = Some isValid }
            | BankChannelAssigned (_, channel, _) -> { state with BankChannel = Some channel }
            | FraudCheckCompleted (_, result, _) -> { state with FraudResult = Some result }
            | PaymentOptimized (_, result, _) -> { state with OptimizationResult = Some result }
            | OptimizedPaymentFileCreated (_, newFile, _) -> { state with OptimizedPaymentFile = Some newFile }
            | PaymentFileSubmittedToBank (_, submittedAt) -> { state with OptimizedPaymentFileSubmittedAt = Some submittedAt }
        ) initialPaymentState
    finalState, events

// Model, Messages, Init, and Update

type Model = {
    PaymentSim: PaymentState option
    PaymentEvents: PaymentEvent list
}

type Msg =
    | UploadPaymentFile of string

let init () =
    { PaymentSim = None; PaymentEvents = [] }, Cmd.none

let update msg model =
    match msg with
    | UploadPaymentFile content ->
        let finalState, events = processPaymentFile content
        { model with PaymentSim = Some finalState; PaymentEvents = events }, Cmd.none

// View: UI

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        Html.h1 [ prop.text "Upload a Payment File" ]
        Html.input [
            prop.type' "file"
            prop.onChange (fun (e: Browser.Types.Event) ->
                let input = e.target :?> Browser.Types.HTMLInputElement
                let files = input.files
                if not (isNull files) && files.length > 0 then
                    let file = files.item(0) // Use .item(0) instead of indexing
                    if not (isNull file) then
                        let reader = Browser.Dom.FileReader.Create()
                        reader.onload <- fun (_: Browser.Types.Event) ->
                            let content = reader.result |> string
                            dispatch (UploadPaymentFile content)
                        reader.readAsText(file)
            )
        ]

        Html.h2 [ prop.text "Event Timeline" ]
        Html.ul [
            model.PaymentEvents
            |> List.map (fun event ->
                Html.li [ prop.text (sprintf "%A" event) ]
            )
            |> prop.children
        ]
    ]
