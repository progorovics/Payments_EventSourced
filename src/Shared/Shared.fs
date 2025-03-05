namespace Shared

open System

type ValidatePaymentFileDto = {
    PaymentFileId: Guid
    Actor: string
    Source: string
    IsValid: bool
}

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
    ReceivedAt: DateTime
    Actor: string
    Source: string
}

type SubmitPaymentFileDto = {
    PaymentFileId: Guid
    Actor: string
    Source: string
}
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
type OptimizationResult = { Optimized: bool; Details: string }

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
    | PaymentFileValidated of PaymentFileEventContext * bool
    | BankChannelAssigned of PaymentFileEventContext * BankChannel
    | FraudCheckCompleted of PaymentFileEventContext * FraudCheckResult
    | PaymentOptimized of PaymentFileEventContext * OptimizationResult
    | OptimizedPaymentFileCreated of PaymentFileEventContext * PaymentFile
    | PaymentFileSubmittedToBank of PaymentFileEventContext

type IPaymentFileApi =
    {
        getCorrelationIds: unit -> Async<Guid list>
        getEventsByCorrelationId: Guid -> Async<PaymentFileEvent list>
        validatePayment: ValidatePaymentFileDto -> Async<unit>
        assignBankChannel: AssignBankChannelDto -> Async<unit>
        completeFraudCheck: CompleteFraudCheckDto -> Async<unit>
    }