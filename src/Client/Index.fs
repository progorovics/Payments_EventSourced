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
    | PaymentFileReceived of PaymentFile
    | PaymentFileValidated of Guid * bool
    | BankChannelAssigned of Guid * BankChannel
    | FraudCheckCompleted of Guid * FraudCheckResult
    | PaymentOptimized of Guid * OptimizationResult
    | OptimizedPaymentFileCreated of Guid * PaymentFile
    | PaymentFileSubmittedToBank of Guid * DateTime

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
        PaymentFileReceived paymentFile
        PaymentFileValidated (paymentFile.Id, true)
        BankChannelAssigned (paymentFile.Id, SWIFT)
        FraudCheckCompleted (paymentFile.Id, Passed)
        PaymentOptimized (paymentFile.Id, { Optimized = true; Details = "Payment optimized successfully." })
        OptimizedPaymentFileCreated (paymentFile.Id, paymentFileOptimized)
        PaymentFileSubmittedToBank (paymentFileOptimized.Id, DateTime.UtcNow.AddSeconds(5.0))
    ]
    let finalState =
        events
        |> List.fold (fun state evt ->
            match evt with
            | PaymentFileReceived p -> { state with PaymentFile = Some p }
            | PaymentFileValidated (_, isValid) -> { state with IsValid = Some isValid }
            | BankChannelAssigned (_, channel) -> { state with BankChannel = Some channel }
            | FraudCheckCompleted (_, result) -> { state with FraudResult = Some result }
            | PaymentOptimized (_, result) -> { state with OptimizationResult = Some result }
            | OptimizedPaymentFileCreated (_, newFile) -> { state with OptimizedPaymentFile = Some newFile }
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
            style.backgroundSize "cover"
            style.backgroundImageUrl "https://unsplash.it/1200/900?random"
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
                        prop.text "Payment File Tracker Event Sourced"
                    ]
                    // Payment Simulation Section with more horizontal space
                    Html.div [
                        // Updated classes for increased width and max width
                        prop.className "bg-white/80 rounded-md shadow-md p-4 w-full lg:w-full lg:max-w-4xl mt-4"

                        prop.children [
                            Html.h2 [ prop.className "text-xl font-bold"; prop.text "Payment File Simulation" ]
                            Html.button [
                                prop.className "flex-no-shrink p-2 px-12 rounded bg-teal-600 outline-none focus:ring-2 ring-teal-300 font-bold text-white hover:bg-teal disabled:opacity-30 disabled:cursor-not-allowed"
                                prop.onClick (fun _ -> dispatch RunPaymentSimulation)
                                prop.text "Run Simulation"
                            ]
                            match model.PaymentSim with
                            | Some sim ->
                                Html.div [
                                    Html.h3 [ prop.text "Final Payment State:" ]
                                    Html.pre [ prop.text (sprintf "%A" sim) ]
                                ]
                            | None ->
                                Html.div [ prop.text "No simulation run yet." ]
                            Html.div [
                                Html.h3 [ prop.text "Events:" ]
                                Html.ul [
                                    for evt in model.PaymentEvents do
                                        let evtStr =
                                            match evt with
                                            | PaymentFileReceived p -> sprintf "Payment file received: %A" p.Id
                                            | PaymentFileValidated (id, valid) -> sprintf "Payment file validated: %A - Valid: %b" id valid
                                            | BankChannelAssigned (id, channel) -> sprintf "Bank channel assigned: %A - Channel: %A" id channel
                                            | FraudCheckCompleted (id, result) ->
                                                match result with
                                                | Passed -> sprintf "Fraud check completed: %A - Passed" id
                                                | Failed msg -> sprintf "Fraud check completed: %A - Failed: %s" id msg
                                            | PaymentOptimized (id, opt) -> sprintf "Payment file optimization completed: %A - OK: %b, Details: %s" id opt.Optimized opt.Details
                                            | OptimizedPaymentFileCreated (id, newFile) -> sprintf "Optimized payment File Created: %A - New File ID: %A" id newFile.Id
                                            | PaymentFileSubmittedToBank (id, submittedAt) -> sprintf "payment file submitted to bank: %A - Submitted At: %A" id submittedAt
                                        Html.li [ prop.text evtStr ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
