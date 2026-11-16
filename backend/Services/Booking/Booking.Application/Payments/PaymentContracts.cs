namespace Booking.Application.Payments;

public sealed record PricingRuleRecord(
    Guid Id,
    string Name,
    string? TransportMode,
    string? DeliveryPriority,
    Guid? OriginLocationId,
    Guid? DestinationLocationId,
    decimal BaseAmount,
    decimal PerHorseAmount,
    decimal PerKmAmount,
    string CurrencyCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreatePricingRuleInput(
    string Name,
    string? TransportMode,
    string? DeliveryPriority,
    Guid? OriginLocationId,
    Guid? DestinationLocationId,
    decimal BaseAmount,
    decimal PerHorseAmount,
    decimal PerKmAmount,
    string CurrencyCode,
    bool IsActive = true);

public sealed record UpdatePricingRuleInput(
    string Name,
    string? TransportMode,
    string? DeliveryPriority,
    Guid? OriginLocationId,
    Guid? DestinationLocationId,
    decimal BaseAmount,
    decimal PerHorseAmount,
    decimal PerKmAmount,
    string CurrencyCode,
    bool IsActive);

public sealed record PaymentInvoiceRecord(
    Guid Id,
    Guid OrderId,
    string OrderNo,
    Guid RequestId,
    string InvoiceNo,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string? ProviderTransactionId,
    long? PayOsOrderCode,
    string? CheckoutUrl,
    string? QrCode,
    string? PayOsPaymentLinkId,
    DateTimeOffset? CheckoutCreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? SettlementMethod = null,
    string? SettlementReference = null,
    string? SettlementEvidenceReference = null,
    Guid? SettledByUserId = null,
    string? SettlementNotes = null);

public sealed record ManualOrderSettlementInput(
    string PaymentMethod,
    string ReferenceNumber,
    string? EvidenceReference = null,
    string? Notes = null);

public sealed record PaymentCheckoutLink(
    string CheckoutUrl,
    string? QrCode,
    long OrderCode,
    string? PaymentLinkId = null);

public sealed record PayOsWebhookData(
    long OrderCode,
    decimal Amount,
    string? Description,
    string? Reference,
    string? TransactionDateTime,
    string? Currency,
    string? PaymentLinkId,
    string? AccountNumber = null,
    string? CounterAccountNumber = null,
    string? CounterAccountName = null,
    string? CounterAccountBankId = null,
    string? CounterAccountBankName = null,
    string? Code = null,
    string? Desc = null,
    string? VirtualAccountName = null,
    string? VirtualAccountNumber = null);

public sealed record PayOsPaymentWebhook(
    string? Code,
    string? Desc,
    bool Success,
    PayOsWebhookData? Data,
    string? Signature);

public interface IPaymentService
{
    Task<IReadOnlyList<PricingRuleRecord>> GetPricingRulesAsync(CancellationToken cancellationToken);
    Task<PricingRuleRecord> CreatePricingRuleAsync(Guid actorUserId, CreatePricingRuleInput input, CancellationToken cancellationToken);
    Task<PricingRuleRecord> UpdatePricingRuleAsync(Guid actorUserId, Guid ruleId, UpdatePricingRuleInput input, CancellationToken cancellationToken);
    Task DeletePricingRuleAsync(Guid ruleId, CancellationToken cancellationToken);
    Task<PaymentInvoiceRecord?> GetCustomerInvoiceAsync(Guid identityUserId, Guid orderId, CancellationToken cancellationToken);
    Task<PaymentInvoiceRecord?> GetAdminInvoiceAsync(Guid orderId, CancellationToken cancellationToken);
    Task<PaymentCheckoutLink> CreateCheckoutAsync(Guid identityUserId, Guid orderId, string? customerId, CancellationToken cancellationToken);
    Task<PaymentInvoiceRecord> RecordManualSettlementAsync(Guid actorUserId, Guid orderId, ManualOrderSettlementInput input, CancellationToken cancellationToken);
    Task<bool> ProcessPayOsWebhookAsync(PayOsPaymentWebhook input, CancellationToken cancellationToken);
}
