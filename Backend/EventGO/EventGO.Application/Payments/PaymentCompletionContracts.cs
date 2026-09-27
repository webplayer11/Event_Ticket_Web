namespace EventGO.Application.Payments;

public enum PaymentCompletionError
{
    None,
    PaymentNotFound,
    PaymentNotPending,
    OrderNotPayable,
    PaymentAmountMismatch,
    ReservationNotActive,
    LatePaymentPolicyUndefined,
    InventoryConflict,
    ConcurrencyConflict
}

public sealed record PaymentCompletionResult(
    PaymentCompletionError Error,
    bool AlreadyCompleted = false);

// This use case must only be called after the payment adapter has verified
// the provider notification. It intentionally is not exposed as a public API.
public interface IPaymentCompletionService
{
    Task<PaymentCompletionResult> CompleteVerifiedPaymentAsync(
        Guid paymentId,
        string providerTransactionId,
        CancellationToken cancellationToken = default);
}
