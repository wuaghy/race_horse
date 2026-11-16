using System.Data;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Booking.Application.Payments;
using BuildingBlocks.Exceptions;
using Contracts.IntegrationEvents;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Booking.Infrastructure.Payments;

public sealed class SqlPaymentService(IConfiguration configuration, IHttpClientFactory httpClientFactory) : IPaymentService
{
    private readonly string _connectionString = configuration.GetConnectionString("BookingDb")
        ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");
    private readonly bool _enabled = configuration.GetValue("PayOS:Enabled", false);
    private readonly string _clientId = configuration["PayOS:ClientId"]?.Trim() ?? string.Empty;
    private readonly string _apiKey = configuration["PayOS:ApiKey"]?.Trim() ?? string.Empty;
    private readonly string _checksumKey = configuration["PayOS:ChecksumKey"]?.Trim() ?? string.Empty;
    private readonly string _successUrl = configuration["PayOS:SuccessUrl"]?.Trim() ?? "http://localhost:5173/payment/success";
    private readonly string _cancelUrl = configuration["PayOS:CancelUrl"]?.Trim() ?? "http://localhost:5173/payment/cancel";
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<IReadOnlyList<PricingRuleRecord>> GetPricingRulesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT Id, Name, TransportMode, DeliveryPriority, OriginLocationId, DestinationLocationId, BaseAmount, PerHorseAmount, PerKmAmount, CurrencyCode, IsActive, CreatedAt, UpdatedAt FROM dbo.TransportPricingRules ORDER BY IsActive DESC, UpdatedAt DESC, Name;", connection);
        var result = new List<PricingRuleRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadPricingRule(reader));
        return result;
    }

    public async Task<PricingRuleRecord> CreatePricingRuleAsync(Guid actorUserId, CreatePricingRuleInput input, CancellationToken cancellationToken)
    {
        ValidateRule(input.Name, input.BaseAmount, input.PerHorseAmount, input.PerKmAmount, input.CurrencyCode);
        var id = Guid.NewGuid();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("INSERT INTO dbo.TransportPricingRules (Id, Name, TransportMode, DeliveryPriority, OriginLocationId, DestinationLocationId, BaseAmount, PerHorseAmount, PerKmAmount, CurrencyCode, IsActive, CreatedByUserId) VALUES (@Id, @Name, @Mode, @Priority, @Origin, @Destination, @BaseAmount, @PerHorseAmount, @PerKmAmount, @Currency, @Active, @Actor);", connection);
        AddRuleParameters(command, id, input.Name, input.TransportMode, input.DeliveryPriority, input.OriginLocationId, input.DestinationLocationId, input.BaseAmount, input.PerHorseAmount, input.PerKmAmount, input.CurrencyCode, input.IsActive, actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (await GetPricingRulesAsync(cancellationToken)).First(x => x.Id == id);
    }

    public async Task<PricingRuleRecord> UpdatePricingRuleAsync(Guid actorUserId, Guid ruleId, UpdatePricingRuleInput input, CancellationToken cancellationToken)
    {
        ValidateRule(input.Name, input.BaseAmount, input.PerHorseAmount, input.PerKmAmount, input.CurrencyCode);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("UPDATE dbo.TransportPricingRules SET Name=@Name, TransportMode=@Mode, DeliveryPriority=@Priority, OriginLocationId=@Origin, DestinationLocationId=@Destination, BaseAmount=@BaseAmount, PerHorseAmount=@PerHorseAmount, PerKmAmount=@PerKmAmount, CurrencyCode=@Currency, IsActive=@Active, UpdatedAt=SYSUTCDATETIME() WHERE Id=@Id;", connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ruleId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = input.Name.Trim();
        command.Parameters.Add("@Mode", SqlDbType.VarChar, 30).Value = (object?)Normalize(input.TransportMode) ?? DBNull.Value;
        command.Parameters.Add("@Priority", SqlDbType.VarChar, 30).Value = (object?)Normalize(input.DeliveryPriority) ?? DBNull.Value;
        command.Parameters.Add("@Origin", SqlDbType.UniqueIdentifier).Value = (object?)input.OriginLocationId ?? DBNull.Value;
        command.Parameters.Add("@Destination", SqlDbType.UniqueIdentifier).Value = (object?)input.DestinationLocationId ?? DBNull.Value;
        AddDecimalParameter(command, "@BaseAmount", input.BaseAmount); AddDecimalParameter(command, "@PerHorseAmount", input.PerHorseAmount); AddDecimalParameter(command, "@PerKmAmount", input.PerKmAmount);
        command.Parameters.Add("@Currency", SqlDbType.VarChar, 10).Value = input.CurrencyCode.Trim().ToUpperInvariant();
        command.Parameters.Add("@Active", SqlDbType.Bit).Value = input.IsActive;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new NotFoundException("Pricing rule", ruleId);
        return (await GetPricingRulesAsync(cancellationToken)).First(x => x.Id == ruleId);
    }

    public async Task DeletePricingRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("UPDATE dbo.TransportPricingRules SET IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE Id=@Id;", connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ruleId;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new NotFoundException("Pricing rule", ruleId);
    }

    public async Task<PaymentInvoiceRecord?> GetCustomerInvoiceAsync(Guid identityUserId, Guid orderId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT p.Id, p.OrderId, o.OrderNo, o.RequestId, p.InvoiceNo, p.Amount, p.CurrencyCode, p.Status, p.ProviderTransactionId, p.PayOsOrderCode, p.CheckoutUrl, p.QrCode, p.PayOsPaymentLinkId, p.CheckoutCreatedAt, p.PaidAt, p.CreatedAt, p.UpdatedAt, COALESCE(p.SettlementMethod,p.PaymentMethod), COALESCE(p.SettlementReference,p.PaymentReference), p.SettlementEvidenceReference, p.SettledByUserId, p.SettlementNotes FROM dbo.TransportPaymentInvoices p INNER JOIN dbo.TransportOrders o ON o.Id=p.OrderId INNER JOIN dbo.TransportRequests r ON r.Id=o.RequestId INNER JOIN dbo.Customers c ON c.Id=r.CustomerId WHERE p.OrderId=@OrderId AND c.IdentityUserId=@IdentityUserId;", connection);
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadInvoice(reader) : null;
    }

    public async Task<PaymentInvoiceRecord?> GetAdminInvoiceAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try { return await GetInvoiceByOrderIdAsync(orderId, cancellationToken); }
        catch (NotFoundException) { return null; }
    }

    public async Task<PaymentCheckoutLink> CreateCheckoutAsync(Guid identityUserId, Guid orderId, string? customerId, CancellationToken cancellationToken)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_checksumKey)) throw new BusinessRuleException("PayOS is not configured. Set PayOS:Enabled, ClientId, ApiKey and ChecksumKey first.", "PAYOS_NOT_CONFIGURED");
        var invoice = await GetCustomerInvoiceAsync(identityUserId, orderId, cancellationToken) ?? throw new NotFoundException("Payment invoice", orderId);
        if (!string.Equals(invoice.Status, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase)) throw new BusinessRuleException("This invoice is not awaiting payment.", "PAYMENT_STATE_INVALID");
        if (invoice.PayOsOrderCode is null) throw new BusinessRuleException("This invoice is missing a PayOS order code.", "PAYOS_ORDER_CODE_MISSING");
        // PayOS payment links are tied to the numeric order code. Reuse the
        // persisted link when the customer closes the checkout page and opens
        // the request again; creating a second link with the same order code
        // makes PayOS reject the request as an existing order.
        if (!string.IsNullOrWhiteSpace(invoice.CheckoutUrl))
            return new PaymentCheckoutLink(invoice.CheckoutUrl, invoice.QrCode, invoice.PayOsOrderCode.Value, invoice.PayOsPaymentLinkId);
        var amount = decimal.ToInt64(decimal.Round(invoice.Amount, 0, MidpointRounding.AwayFromZero));
        var description = $"EQ {invoice.OrderNo}";
        var orderCode = invoice.PayOsOrderCode.Value;
        // Recover links created before link persistence was added. PayOS
        // rejects another POST for an existing order code, but its detail API
        // exposes the paymentLinkId needed to rebuild the hosted URL.
        var restored = await TryRestoreCheckoutLinkAsync(orderCode, cancellationToken);
        if (restored is not null)
            return restored;
        var payload = $"amount={amount.ToString(CultureInfo.InvariantCulture)}&cancelUrl={_cancelUrl}&description={description}&orderCode={orderCode.ToString(CultureInfo.InvariantCulture)}&returnUrl={_successUrl}";
        var requestBody = new { orderCode, amount, description, cancelUrl = _cancelUrl, returnUrl = _successUrl, signature = HmacHex(payload, _checksumKey) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api-merchant.payos.vn/v2/payment-requests") { Content = JsonContent.Create(requestBody) };
        request.Headers.Add("x-client-id", _clientId); request.Headers.Add("x-api-key", _apiKey);
        using var response = await _httpClientFactory.CreateClient("PayOS").SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new BusinessRuleException($"PayOS checkout failed ({(int)response.StatusCode}).", "PAYOS_CHECKOUT_FAILED");
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var responseCode = root.TryGetProperty("code", out var code) ? code.GetString() : null;
        if (!string.Equals(responseCode, "00", StringComparison.OrdinalIgnoreCase)) throw new BusinessRuleException(root.TryGetProperty("desc", out var desc) ? desc.GetString() ?? "PayOS rejected the payment request." : "PayOS rejected the payment request.", "PAYOS_CHECKOUT_FAILED");
        var data = root.GetProperty("data");
        var checkoutUrl = data.GetProperty("checkoutUrl").GetString()!;
        var qrCode = data.TryGetProperty("qrCode", out var qr) ? qr.GetString() : null;
        var returnedOrderCode = data.TryGetProperty("orderCode", out var returnedCode) ? returnedCode.GetInt64() : orderCode;
        var paymentLinkId = data.TryGetProperty("paymentLinkId", out var linkId) ? linkId.GetString() : null;
        await PersistCheckoutLinkAsync(invoice.Id, checkoutUrl, qrCode, paymentLinkId, cancellationToken);
        return new PaymentCheckoutLink(checkoutUrl, qrCode, returnedOrderCode, paymentLinkId);
    }

    public async Task<PaymentInvoiceRecord> RecordManualSettlementAsync(Guid actorUserId, Guid orderId, ManualOrderSettlementInput input, CancellationToken cancellationToken)
    {
        var method = input.PaymentMethod?.Trim().ToUpperInvariant();
        var reference = Normalize(input.ReferenceNumber);
        var evidenceReference = Normalize(input.EvidenceReference);
        var notes = Normalize(input.Notes);
        if (method is not ("BANK_TRANSFER" or "CASH" or "OTHER"))
            throw new BusinessRuleException("Method must be BANK_TRANSFER, CASH, or OTHER.", "PAYMENT_METHOD_INVALID", "method");
        if (reference is null || reference.Length > 150)
            throw new BusinessRuleException("A settlement reference of up to 150 characters is required.", "PAYMENT_REFERENCE_REQUIRED", "reference");
        if (evidenceReference?.Length > 500)
            throw new BusinessRuleException("Evidence reference cannot exceed 500 characters.", "PAYMENT_EVIDENCE_REFERENCE_TOO_LONG", "evidenceReference");
        if (notes?.Length > 1000)
            throw new BusinessRuleException("Settlement notes cannot exceed 1,000 characters.", "PAYMENT_NOTES_TOO_LONG", "notes");

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await using var invoiceLock = new SqlCommand(
                "SELECT Id, Status, SettlementMethod, SettlementReference FROM dbo.TransportPaymentInvoices WITH (UPDLOCK, HOLDLOCK) WHERE OrderId=@OrderId;",
                connection, transaction);
            invoiceLock.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
            Guid invoiceId;
            string status;
            string? previousMethod;
            string? previousReference;
            await using (var reader = await invoiceLock.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken)) throw new NotFoundException("Payment invoice", orderId);
                invoiceId = reader.GetGuid(0);
                status = reader.GetString(1);
                previousMethod = reader.IsDBNull(2) ? null : reader.GetString(2);
                previousReference = reader.IsDBNull(3) ? null : reader.GetString(3);
            }

            if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previousMethod, method, StringComparison.Ordinal) &&
                string.Equals(previousReference, reference, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return await GetInvoiceByOrderIdAsync(orderId, cancellationToken);
            }

            if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
                throw new AppException("A different settlement has already been recorded for this invoice.", statusCode: 409, errorCode: 40900);
            if (!string.Equals(status, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase))
                throw new AppException("Only an invoice awaiting payment can be settled manually.", statusCode: 409, errorCode: 40900);

            var settledAt = DateTime.UtcNow;
            await using (var update = new SqlCommand(
                "UPDATE dbo.TransportPaymentInvoices SET Status='PAID', SettlementMethod=@Method, SettlementReference=@Reference, SettlementEvidenceReference=@EvidenceReference, SettledByUserId=@ActorUserId, SettlementNotes=@Notes, PaidAt=@PaidAt, UpdatedAt=@PaidAt WHERE Id=@InvoiceId AND Status='PENDING_PAYMENT';",
                connection, transaction))
            {
                update.Parameters.Add("@Method", SqlDbType.VarChar, 30).Value = method;
                update.Parameters.Add("@Reference", SqlDbType.NVarChar, 150).Value = reference;
                update.Parameters.Add("@EvidenceReference", SqlDbType.NVarChar, 500).Value = (object?)evidenceReference ?? DBNull.Value;
                update.Parameters.Add("@ActorUserId", SqlDbType.UniqueIdentifier).Value = actorUserId;
                update.Parameters.Add("@Notes", SqlDbType.NVarChar, 1000).Value = (object?)notes ?? DBNull.Value;
                update.Parameters.Add("@PaidAt", SqlDbType.DateTime2).Value = settledAt;
                update.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new AppException("The invoice changed while settlement was being recorded.", statusCode: 409, errorCode: 40900);
            }

            await using (var audit = new SqlCommand(
                "INSERT INTO dbo.TransportPaymentAuditLogs (InvoiceId, OrderId, Action, OldStatus, NewStatus, SettlementMethod, SettlementReference, EvidenceReference, Notes, ActorUserId, OccurredAt) VALUES (@InvoiceId, @OrderId, 'MANUAL_SETTLEMENT_RECORDED', 'PENDING_PAYMENT', 'PAID', @Method, @Reference, @EvidenceReference, @Notes, @ActorUserId, @OccurredAt);",
                connection, transaction))
            {
                audit.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                audit.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
                audit.Parameters.Add("@Method", SqlDbType.VarChar, 30).Value = method;
                audit.Parameters.Add("@Reference", SqlDbType.NVarChar, 150).Value = reference;
                audit.Parameters.Add("@EvidenceReference", SqlDbType.NVarChar, 500).Value = (object?)evidenceReference ?? DBNull.Value;
                audit.Parameters.Add("@Notes", SqlDbType.NVarChar, 1000).Value = (object?)notes ?? DBNull.Value;
                audit.Parameters.Add("@ActorUserId", SqlDbType.UniqueIdentifier).Value = actorUserId;
                audit.Parameters.Add("@OccurredAt", SqlDbType.DateTime2).Value = settledAt;
                await audit.ExecuteNonQueryAsync(cancellationToken);
            }

            var completion = await ReadPaymentCompletionContextForOrderAsync(connection, transaction, orderId, cancellationToken);
            var correlationId = Guid.NewGuid();
            var paymentEvent = new BookingOrderPaymentCompletedEvent
            {
                CorrelationId = correlationId,
                OccurredAt = settledAt,
                Data = new BookingOrderPaymentCompletedData(completion.OrderId, settledAt, completion.RequestId,
                    completion.CustomerUserId, completion.CustomerEmail, completion.RequestNo, completion.OrderNo, method, reference)
            };
            await WriteOutboxAsync(connection, transaction, paymentEvent, correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetInvoiceByOrderIdAsync(orderId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PaymentInvoiceRecord> GetInvoiceByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT p.Id, p.OrderId, o.OrderNo, o.RequestId, p.InvoiceNo, p.Amount, p.CurrencyCode, p.Status, p.ProviderTransactionId, p.PayOsOrderCode, p.CheckoutUrl, p.QrCode, p.PayOsPaymentLinkId, p.CheckoutCreatedAt, p.PaidAt, p.CreatedAt, p.UpdatedAt, COALESCE(p.SettlementMethod,p.PaymentMethod), COALESCE(p.SettlementReference,p.PaymentReference), p.SettlementEvidenceReference, p.SettledByUserId, p.SettlementNotes FROM dbo.TransportPaymentInvoices p INNER JOIN dbo.TransportOrders o ON o.Id=p.OrderId WHERE p.OrderId=@OrderId;", connection);
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new NotFoundException("Payment invoice", orderId);
        return ReadInvoice(reader);
    }

    private async Task<PaymentCheckoutLink?> TryRestoreCheckoutLinkAsync(long orderCode, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api-merchant.payos.vn/v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}");
        request.Headers.Add("x-client-id", _clientId);
        request.Headers.Add("x-api-key", _apiKey);
        using var response = await _httpClientFactory.CreateClient("PayOS").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        if (!root.TryGetProperty("code", out var code) || !string.Equals(code.GetString(), "00", StringComparison.OrdinalIgnoreCase) || !root.TryGetProperty("data", out var data))
            return null;
        var paymentLinkId = data.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(paymentLinkId))
            return null;
        var checkoutUrl = data.TryGetProperty("checkoutUrl", out var checkout) ? checkout.GetString() : null;
        checkoutUrl ??= $"https://pay.payos.vn/web/{paymentLinkId}";
        await PersistCheckoutLinkAsyncForOrderCode(orderCode, checkoutUrl, null, paymentLinkId, cancellationToken);
        return new PaymentCheckoutLink(checkoutUrl, null, orderCode, paymentLinkId);
    }

    public async Task<bool> ProcessPayOsWebhookAsync(PayOsPaymentWebhook input, CancellationToken cancellationToken)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_checksumKey) || input.Data is null || !input.Success || !string.Equals(input.Code, "00", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = BuildWebhookSignature(input.Data);
        if (string.IsNullOrWhiteSpace(input.Signature) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(input.Signature.Trim().ToLowerInvariant()), Encoding.UTF8.GetBytes(expected))) return false;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await using var update = new SqlCommand("UPDATE p SET Status='PAID', PaymentMethod='PAYOS', PaymentReference=COALESCE(@TransactionId, PaymentReference), ProviderTransactionId=COALESCE(@TransactionId, ProviderTransactionId), PaidAt=COALESCE(PaidAt, SYSUTCDATETIME()), UpdatedAt=SYSUTCDATETIME() FROM dbo.TransportPaymentInvoices p WHERE p.PayOsOrderCode=@OrderCode AND p.Amount=@Amount AND p.Status='PENDING_PAYMENT';", connection, transaction);
            update.Parameters.Add("@OrderCode", SqlDbType.BigInt).Value = input.Data.OrderCode; AddDecimalParameter(update, "@Amount", input.Data.Amount);
            update.Parameters.Add("@TransactionId", SqlDbType.VarChar, 150).Value = (object?)Normalize(input.Data.Reference) ?? DBNull.Value;
            var changed = await update.ExecuteNonQueryAsync(cancellationToken) > 0;
            if (changed)
            {
                var completion = await ReadPaymentCompletionContextAsync(connection, transaction, input.Data.OrderCode, cancellationToken);
                var correlationId = Guid.NewGuid();
                var paidAt = DateTime.UtcNow;
                var paymentEvent = new BookingOrderPaymentCompletedEvent
                {
                    CorrelationId = correlationId,
                    OccurredAt = paidAt,
                    Data = new BookingOrderPaymentCompletedData(completion.OrderId, paidAt, completion.RequestId, completion.CustomerUserId, completion.CustomerEmail, completion.RequestNo, completion.OrderNo, "PAYOS", Normalize(input.Data.Reference))
                };
                await WriteOutboxAsync(connection, transaction, paymentEvent, correlationId, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken); return changed;
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) { var connection = new SqlConnection(_connectionString); await connection.OpenAsync(cancellationToken); return connection; }
    private static PricingRuleRecord ReadPricingRule(SqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetString(9), reader.GetBoolean(10), ToUtcOffset(reader.GetDateTime(11)), ToUtcOffset(reader.GetDateTime(12)));
    private static PaymentInvoiceRecord ReadInvoice(SqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetInt64(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : ToUtcOffset(reader.GetDateTime(13)), reader.IsDBNull(14) ? null : ToUtcOffset(reader.GetDateTime(14)), ToUtcOffset(reader.GetDateTime(15)), ToUtcOffset(reader.GetDateTime(16)), reader.IsDBNull(17) ? null : reader.GetString(17), reader.IsDBNull(18) ? null : reader.GetString(18), reader.IsDBNull(19) ? null : reader.GetString(19), reader.IsDBNull(20) ? null : reader.GetGuid(20), reader.IsDBNull(21) ? null : reader.GetString(21));
    private async Task PersistCheckoutLinkAsync(Guid invoiceId, string checkoutUrl, string? qrCode, string? paymentLinkId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("UPDATE dbo.TransportPaymentInvoices SET CheckoutUrl=@CheckoutUrl, QrCode=@QrCode, PayOsPaymentLinkId=@PaymentLinkId, CheckoutCreatedAt=COALESCE(CheckoutCreatedAt, SYSUTCDATETIME()), UpdatedAt=SYSUTCDATETIME() WHERE Id=@Id AND Status='PENDING_PAYMENT' AND (CheckoutUrl IS NULL OR LTRIM(RTRIM(CheckoutUrl))='');", connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = invoiceId;
        command.Parameters.Add("@CheckoutUrl", SqlDbType.NVarChar, 1000).Value = checkoutUrl;
        command.Parameters.Add("@QrCode", SqlDbType.NVarChar, -1).Value = (object?)qrCode ?? DBNull.Value;
        command.Parameters.Add("@PaymentLinkId", SqlDbType.VarChar, 150).Value = (object?)paymentLinkId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private async Task PersistCheckoutLinkAsyncForOrderCode(long orderCode, string checkoutUrl, string? qrCode, string? paymentLinkId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("UPDATE dbo.TransportPaymentInvoices SET CheckoutUrl=@CheckoutUrl, QrCode=@QrCode, PayOsPaymentLinkId=@PaymentLinkId, CheckoutCreatedAt=COALESCE(CheckoutCreatedAt, SYSUTCDATETIME()), UpdatedAt=SYSUTCDATETIME() WHERE PayOsOrderCode=@OrderCode AND Status='PENDING_PAYMENT' AND (CheckoutUrl IS NULL OR LTRIM(RTRIM(CheckoutUrl))='');", connection);
        command.Parameters.Add("@OrderCode", SqlDbType.BigInt).Value = orderCode;
        command.Parameters.Add("@CheckoutUrl", SqlDbType.NVarChar, 1000).Value = checkoutUrl;
        command.Parameters.Add("@QrCode", SqlDbType.NVarChar, -1).Value = (object?)qrCode ?? DBNull.Value;
        command.Parameters.Add("@PaymentLinkId", SqlDbType.VarChar, 150).Value = (object?)paymentLinkId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private static void ValidateRule(string name, decimal baseAmount, decimal perHorseAmount, decimal perKmAmount, string currency) { if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Pricing rule name is required.", "PRICING_RULE_NAME_REQUIRED"); if (baseAmount < 0 || perHorseAmount < 0 || perKmAmount < 0) throw new BusinessRuleException("Pricing amounts cannot be negative.", "PRICING_RULE_AMOUNT_INVALID"); if (!string.Equals(currency?.Trim(), "VND", StringComparison.OrdinalIgnoreCase)) throw new BusinessRuleException("PayOS checkout currently supports VND pricing only.", "PRICING_RULE_CURRENCY_INVALID", "currencyCode"); }
    private static void AddRuleParameters(SqlCommand command, Guid id, string name, string? mode, string? priority, Guid? origin, Guid? destination, decimal baseAmount, decimal perHorseAmount, decimal perKmAmount, string currency, bool active, Guid actor) { command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id; command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = name.Trim(); command.Parameters.Add("@Mode", SqlDbType.VarChar, 30).Value = (object?)Normalize(mode) ?? DBNull.Value; command.Parameters.Add("@Priority", SqlDbType.VarChar, 30).Value = (object?)Normalize(priority) ?? DBNull.Value; command.Parameters.Add("@Origin", SqlDbType.UniqueIdentifier).Value = (object?)origin ?? DBNull.Value; command.Parameters.Add("@Destination", SqlDbType.UniqueIdentifier).Value = (object?)destination ?? DBNull.Value; AddDecimalParameter(command, "@BaseAmount", baseAmount); AddDecimalParameter(command, "@PerHorseAmount", perHorseAmount); AddDecimalParameter(command, "@PerKmAmount", perKmAmount); command.Parameters.Add("@Currency", SqlDbType.VarChar, 10).Value = currency.Trim().ToUpperInvariant(); command.Parameters.Add("@Active", SqlDbType.Bit).Value = active; command.Parameters.Add("@Actor", SqlDbType.UniqueIdentifier).Value = actor; }
    private static void AddDecimalParameter(SqlCommand command, string name, decimal value) { var parameter = command.Parameters.Add(name, SqlDbType.Decimal); parameter.Precision = 18; parameter.Scale = 2; parameter.Value = value; }
    private sealed record PaymentCompletionContext(Guid OrderId, Guid? RequestId, Guid? CustomerUserId, string? CustomerEmail, string? RequestNo, string? OrderNo);

    private static async Task<PaymentCompletionContext> ReadPaymentCompletionContextForOrderAsync(SqlConnection connection, SqlTransaction transaction, Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT o.Id, o.RequestId, c.IdentityUserId, c.Email, r.RequestNo, o.OrderNo FROM dbo.TransportOrders o " +
            "INNER JOIN dbo.TransportRequests r ON r.Id=o.RequestId INNER JOIN dbo.Customers c ON c.Id=r.CustomerId WHERE o.Id=@OrderId;",
            connection, transaction);
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new NotFoundException("Transport order", orderId);
        return new PaymentCompletionContext(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private static async Task<PaymentCompletionContext> ReadPaymentCompletionContextAsync(SqlConnection connection, SqlTransaction transaction, long orderCode, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT o.Id, o.RequestId, c.IdentityUserId, c.Email, r.RequestNo, o.OrderNo " +
            "FROM dbo.TransportPaymentInvoices p " +
            "INNER JOIN dbo.TransportOrders o ON o.Id = p.OrderId " +
            "INNER JOIN dbo.TransportRequests r ON r.Id = o.RequestId " +
            "INNER JOIN dbo.Customers c ON c.Id = r.CustomerId " +
            "WHERE p.PayOsOrderCode=@OrderCode;",
            connection,
            transaction);
        command.Parameters.Add("@OrderCode", SqlDbType.BigInt).Value = orderCode;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The paid PayOS order code is not linked to a transport order.");
        return new PaymentCompletionContext(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }
    private static async Task WriteOutboxAsync(SqlConnection connection, SqlTransaction transaction, IIntegrationEvent integrationEvent, Guid correlationId, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var command = new SqlCommand("INSERT INTO dbo.OutboxMessages (EventType, AggregateType, AggregateId, Payload, CorrelationId, OccurredAt, Status) VALUES (@EventType, 'TransportOrder', @AggregateId, @Payload, @CorrelationId, @OccurredAt, 'PENDING');", connection, transaction);
        var payment = (BookingOrderPaymentCompletedEvent)integrationEvent;
        command.Parameters.Add("@EventType", SqlDbType.VarChar, 150).Value = integrationEvent.EventType;
        command.Parameters.Add("@AggregateId", SqlDbType.UniqueIdentifier).Value = payment.Data.OrderId;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        command.Parameters.Add("@OccurredAt", SqlDbType.DateTime2).Value = integrationEvent.OccurredAt;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private string BuildWebhookSignature(PayOsWebhookData data) { var values = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["accountNumber"] = data.AccountNumber ?? string.Empty, ["amount"] = data.Amount.ToString(CultureInfo.InvariantCulture), ["code"] = data.Code ?? string.Empty, ["counterAccountBankId"] = data.CounterAccountBankId ?? string.Empty, ["counterAccountBankName"] = data.CounterAccountBankName ?? string.Empty, ["counterAccountName"] = data.CounterAccountName ?? string.Empty, ["counterAccountNumber"] = data.CounterAccountNumber ?? string.Empty, ["currency"] = data.Currency ?? string.Empty, ["desc"] = data.Desc ?? string.Empty, ["description"] = data.Description ?? string.Empty, ["orderCode"] = data.OrderCode.ToString(CultureInfo.InvariantCulture), ["paymentLinkId"] = data.PaymentLinkId ?? string.Empty, ["reference"] = data.Reference ?? string.Empty, ["transactionDateTime"] = data.TransactionDateTime ?? string.Empty, ["virtualAccountName"] = data.VirtualAccountName ?? string.Empty, ["virtualAccountNumber"] = data.VirtualAccountNumber ?? string.Empty }; return HmacHex(string.Join("&", values.Select(pair => $"{pair.Key}={pair.Value}")), _checksumKey); }
    private static string HmacHex(string payload, string key) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset ToUtcOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
