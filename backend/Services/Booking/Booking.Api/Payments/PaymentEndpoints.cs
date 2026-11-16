using System.Security.Claims;
using Booking.Application.Payments;
using BuildingBlocks.Api;

namespace Booking.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/pricing-rules").RequireAuthorization();
        admin.MapGet("/", async (ClaimsPrincipal principal, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!IsStaff(principal)) return Results.Forbid();
            var rules = await service.GetPricingRulesAsync(cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<PricingRuleRecord>>.Success(rules, requestId: GetRequestId(context)));
        });
        admin.MapPost("/", async (CreatePricingRuleInput input, ClaimsPrincipal principal, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!IsStaff(principal)) return Results.Forbid();
            var rule = await service.CreatePricingRuleAsync(GetUserId(principal), input, cancellationToken);
            return Results.Created($"/api/v1/admin/pricing-rules/{rule.Id}", ApiResponse<PricingRuleRecord>.Success(rule, "Pricing rule created.", GetRequestId(context)));
        });
        admin.MapPut("/{ruleId:guid}", async (Guid ruleId, UpdatePricingRuleInput input, ClaimsPrincipal principal, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!IsStaff(principal)) return Results.Forbid();
            var rule = await service.UpdatePricingRuleAsync(GetUserId(principal), ruleId, input, cancellationToken);
            return Results.Ok(ApiResponse<PricingRuleRecord>.Success(rule, "Pricing rule updated.", GetRequestId(context)));
        });
        admin.MapDelete("/{ruleId:guid}", async (Guid ruleId, ClaimsPrincipal principal, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!IsStaff(principal)) return Results.Forbid();
            await service.DeletePricingRuleAsync(ruleId, cancellationToken);
            return Results.NoContent();
        });

        var customer = app.MapGroup("/api/v1/orders").RequireAuthorization("Customer");
        customer.MapGet("/{orderId:guid}/payment", async (Guid orderId, ClaimsPrincipal principal, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var invoice = await service.GetCustomerInvoiceAsync(GetUserId(principal), orderId, cancellationToken);
            return invoice is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Payment invoice was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<PaymentInvoiceRecord>.Success(invoice, requestId: GetRequestId(context)));
        });
        customer.MapPost("/{orderId:guid}/payment/checkout", async (Guid orderId, ClaimsPrincipal principal, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var checkout = await service.CreateCheckoutAsync(GetUserId(principal), orderId, principal.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
            return Results.Ok(ApiResponse<PaymentCheckoutLink>.Success(checkout, requestId: GetRequestId(context)));
        });

        var adminOrders = app.MapGroup("/api/v1/admin/orders").RequireAuthorization();
        adminOrders.MapGet("/{orderId:guid}/payment", async (
            Guid orderId,
            ClaimsPrincipal principal,
            IPaymentService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (!IsStaff(principal)) return Results.Forbid();
            var invoice = await service.GetAdminInvoiceAsync(orderId, cancellationToken);
            return invoice is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Payment invoice was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<PaymentInvoiceRecord>.Success(invoice, requestId: GetRequestId(context)));
        }).WithName("GetAdminOrderPayment").WithOpenApi();

        adminOrders.MapPost("/{orderId:guid}/payment/manual-settlement", async (
            Guid orderId,
            ManualOrderSettlementInput input,
            ClaimsPrincipal principal,
            IPaymentService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (!IsStaff(principal)) return Results.Forbid();
            var invoice = await service.RecordManualSettlementAsync(GetUserId(principal), orderId, input, cancellationToken);
            return Results.Ok(ApiResponse<PaymentInvoiceRecord>.Success(invoice, "Manual settlement recorded.", GetRequestId(context)));
        }).WithName("RecordManualOrderSettlement").WithOpenApi();

        // PayOS calls this endpoint from the public internet. The checksum signature
        // is verified against the invoice order code and amount before marking it paid.
        app.MapPost("/api/v1/payments/payos/webhook", async (PayOsPaymentWebhook input, IPaymentService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var processed = await service.ProcessPayOsWebhookAsync(input, cancellationToken);
            return processed
                ? Results.Ok(new { success = true, status = "PAID", requestId = GetRequestId(context) })
                : Results.Accepted(value: new { success = true, status = "IGNORED", requestId = GetRequestId(context) });
        }).AllowAnonymous();
    }

    private static bool IsStaff(ClaimsPrincipal principal) => principal.FindFirstValue("role") is "ADMIN" or "LOGISTICS_MANAGER";
    private static Guid GetUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")!);
    private static Guid GetRequestId(HttpContext context) => Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var id) ? id : Guid.NewGuid();
}
