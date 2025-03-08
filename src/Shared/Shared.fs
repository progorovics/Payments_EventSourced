namespace Shared

open System
type PaymentFile = {
    Id: Guid
    StorageLink: string
    Actor: string
    Source: string
}
type ImportPaymentFileDto = {
    PaymentFile: PaymentFile
}

type ValidatePaymentFileDto = {
    PaymentFileId: Guid
    Actor: string
    Source: string
    IsValid: bool
}

type GetCorrelationIdsDto = unit

type AssignBankChannelDto = {
    PaymentFileId: Guid
    Actor: string
    Source: string
    BankChannel: string
}

type CompleteFraudCheckDto = {
    PaymentFileId: Guid
    Actor: string
    Source: string
    Passed: bool
    Error: string option
}

type OptimizePaymentFileDto = {
    PaymentFileId: Guid
    Actor: string
    Source: string
    Optimized: bool
    Details: string
}

type CreateOptimizedPaymentFileDto = {
    OriginalPaymentFileId: Guid
    NewPaymentFileId: Guid
    StorageLink: string
    Actor: string
    Source: string
}

type SubmitPaymentFileDto = {
    PaymentFileId: Guid
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
type OptimizationResult = { Optimized: bool; Details: string }

type PaymentFileState = {
    PaymentFile: PaymentFile option
    IsValid: bool option
    BankChannel: BankChannel option
    FraudCheckResult: FraudCheckResult option
    OptimizationResult: OptimizationResult option
    OptimizedPaymentFile: PaymentFile option
    OptimizedPaymentFileSubmittedAt: DateTime option
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
type PaymentFileEvent =
    | PaymentFileImported of PaymentFileEventContext * PaymentFile
    | PaymentFileValidated of PaymentFileEventContext * bool
    | BankChannelAssigned of PaymentFileEventContext * BankChannel
    | FraudCheckCompleted of PaymentFileEventContext * FraudCheckResult
    | PaymentFileOptimized of PaymentFileEventContext * OptimizationResult
    | OptimizedPaymentFileCreated of PaymentFileEventContext * PaymentFile
    | PaymentFileSubmittedToBank of PaymentFileEventContext

type IPaymentFileApi =
    {
        getCorrelationIds: unit -> Async<Guid list>
        getEventsByCorrelationId: Guid -> Async<PaymentFileEvent list>
        importPaymentFile: ImportPaymentFileDto -> Async<unit>
        validatePaymentFile: ValidatePaymentFileDto -> Async<unit>
        assignBankChannel: AssignBankChannelDto -> Async<unit>
        completeFraudCheck: CompleteFraudCheckDto -> Async<unit>
        optimizePaymentFile: OptimizePaymentFileDto -> Async<unit>
        createOptimizedPaymentFile: CreateOptimizedPaymentFileDto -> Async<unit>
        submitPaymentFile: SubmitPaymentFileDto -> Async<unit>
    }

