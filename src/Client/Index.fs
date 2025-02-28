module Index

open Elmish
open Feliz
open System

// -----------------------------------------------------
// Payment Simulation Domain Types & Simulation Function
// -----------------------------------------------------

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

let getEventTimestamp (event: PaymentEvent) =
    match event with
    | PaymentFileReceived (_, timestamp) -> timestamp
    | PaymentFileValidated (_, _, timestamp) -> timestamp
    | BankChannelAssigned (_, _, timestamp) -> timestamp
    | FraudCheckCompleted (_, _, timestamp) -> timestamp
    | PaymentOptimized (_, _, timestamp) -> timestamp
    | OptimizedPaymentFileCreated (_, _, timestamp) -> timestamp
    | PaymentFileSubmittedToBank (_, timestamp) -> timestamp

let getEventDescription (event: PaymentEvent) =
    match event with
        | PaymentFileReceived (file, _) -> sprintf "Payment file %A -> imported" file.Id
        | PaymentFileValidated (id, isValid, _) -> sprintf "Payment file %A -> validated: %b" id isValid
        | BankChannelAssigned (id, channel, _) -> sprintf "Payment file %A -> bank channel assigned: %A" id channel
        | FraudCheckCompleted (id, result, _) -> sprintf "Payment file %A -> Fraud check completed: %A" id result
        | PaymentOptimized (id, result, _) -> sprintf "Payment file %A -> optimization completed: %s" id result.Details
        | OptimizedPaymentFileCreated (id, paymentFileOptimized, _) -> sprintf "Payment file %A -> Optimized payment file created (id: %A)" id paymentFileOptimized.Id
        | PaymentFileSubmittedToBank (id, _) -> sprintf "Payment file %A -> Optimized payment file submitted to bank" id

let runSimulation () : PaymentState * PaymentEvent list =

    let CustomerId = Guid.NewGuid()
    // Create a sample payment file in PAIN.001 format.
    let paymentFile = {
        Id = Guid.NewGuid()
        Content = "<PAIN.001>...</PAIN.001>"
        CustomerId = CustomerId
        ReceivedAt = DateTime.UtcNow
    }
    // Create a sample payment file in PAIN.001 format.
    let paymentFileOptimized = {
        Id = Guid.NewGuid()
        Content = "<PAIN.001>...</PAIN.001>"
        CustomerId = CustomerId
        ReceivedAt = DateTime.UtcNow
    }

    let events = [
        PaymentFileReceived (paymentFile, DateTime.UtcNow)
        PaymentFileValidated (paymentFile.Id, true, DateTime.UtcNow.AddSeconds(1.0))
        BankChannelAssigned (paymentFile.Id, SWIFT, DateTime.UtcNow.AddSeconds(2.0))
        FraudCheckCompleted (paymentFile.Id, Passed, DateTime.UtcNow.AddSeconds(3.0))
        PaymentOptimized (paymentFile.Id, { Optimized = true; Details = "Payment optimized successfully." }, DateTime.UtcNow.AddSeconds(19.0))
        OptimizedPaymentFileCreated (paymentFile.Id, paymentFileOptimized, DateTime.UtcNow.AddSeconds(19.5))
        PaymentFileSubmittedToBank (paymentFileOptimized.Id, DateTime.UtcNow.AddSeconds(25.0))
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

// -----------------------------------------------------
// Model, Messages, Init, and Update
// -----------------------------------------------------

type Model = {
    PaymentSim: PaymentState option
    PaymentEvents: PaymentEvent list
}

type Msg =
    | RunPaymentSimulation

let init () =
    { PaymentSim = None; PaymentEvents = [] }, Cmd.none

let update msg model =
    match msg with
    | RunPaymentSimulation ->
        let finalState, events = runSimulation ()
        { model with PaymentSim = Some finalState; PaymentEvents = events }, Cmd.none

// -----------------------------------------------------
// View: Payment Simulation UI
// -----------------------------------------------------

let view (model: Model) (dispatch: Msg -> unit) =
    Html.section [
        prop.className "h-screen w-screen"
        prop.style [
            style.backgroundColor "#001F3F" // Dark blue background
            style.backgroundSize "cover"
            // style.backgroundImageUrl "https://unsplash.it/1200/900?random"
            style.backgroundPosition "no-repeat center center fixed"
        ]
        prop.children [
            Html.a [
                prop.href "https://safe-stack.github.io/"
                prop.className "absolute block ml-12 h-12 w-12 bg-teal-300 hover:cursor-pointer hover:bg-teal-400"
                prop.children [ Html.img [ prop.src "/favicon.png"; prop.alt "Logo" ] ]
            ]
            Html.div [
                prop.className "flex flex-col items-center justify-center h-full gap-6"
                prop.children [
                    Html.h1 [
                        prop.className "text-center text-6xl font-bold text-white mb-3 rounded-md p-4"
                        prop.children [
                            Html.text "Payment File Tracker"
                            Html.br []
                            Html.text "Event Sourced"
                        ]              ]
                    Html.button [
                        prop.className "bg-gray-500 hover:bg-gray-700 text-white font-bold py-2 px-4 rounded"
                        prop.text "Run Payment Simulation"
                        prop.onClick (fun _ -> dispatch RunPaymentSimulation)
                    ]
                    Html.div [
                        prop.className "w-full lg:w-full lg:max-w-4xl mt-4"
                        prop.children [
                            Html.h2 [
                                prop.className "text-white"
                                prop.text "Event Timeline"
                            ]
                            Html.div [
                                prop.className "flex flex-col text-white"
                                prop.children (
                                    model.PaymentEvents
                                    |> List.map (fun event ->
                                        Html.div [
                                            prop.className "flex items-center mb-4"
                                            prop.children [
                                                Html.span [
                                                    prop.className "w-4 h-4 bg-gray-400 rounded-full mr-4"
                                                ]
                                                Html.span [
                                                    prop.text (sprintf "%s: %s" ((getEventTimestamp event).ToString("dd MMM yyyy HH:mm:ss")) (getEventDescription event))
                                                ]
                                            ]
                                        ]
                                    )
                                )
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
