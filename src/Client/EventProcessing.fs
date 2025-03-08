module EventProcessing

open Shared

let getEventTimestamp (event: PaymentFileEvent) =
    match event with
    | PaymentFileImported(metadata, _) -> metadata.CreatedAt
    | PaymentFileValidated(metadata, _) -> metadata.CreatedAt
    | BankChannelAssigned(metadata, _) -> metadata.CreatedAt
    | FraudCheckCompleted(metadata, _) -> metadata.CreatedAt
    | PaymentFileOptimized(metadata, _) -> metadata.CreatedAt
    | OptimizedPaymentFileCreated(metadata, _) -> metadata.CreatedAt
    | PaymentFileSubmittedToBank metadata -> metadata.CreatedAt

let getEventDescription (event: PaymentFileEvent) =
    match event with
    | PaymentFileImported(metaData, _) ->
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