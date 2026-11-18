using Compliance.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Application.DTOs.Readiness;
using Planning.Infrastructure.Clients;
using Xunit;

namespace Planning.Tests;

public class ComplianceGrpcClientTests
{
    [Fact]
    public async Task VerifyTripComplianceAsync_MapsQueryAndSets5SecondDeadline()
    {
        var testInvoker = new TestCallInvoker();
        var rpcClient = new ComplianceRpc.ComplianceRpcClient(testInvoker);
        var client = new ComplianceGrpcClient(rpcClient, NullLogger<ComplianceGrpcClient>.Instance);

        var tripId = Guid.NewGuid();
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        var departureDate = new DateOnly(2026, 12, 1);

        var query = new ComplianceTripQuery(
            tripId,
            originId,
            destId,
            new List<Guid> { horseId },
            departureDate
        );

        var result = await client.VerifyTripComplianceAsync(query);

        Assert.NotNull(testInvoker.CapturedRequest);
        Assert.Equal(tripId.ToString(), testInvoker.CapturedRequest.TripId);
        Assert.Equal(originId.ToString(), testInvoker.CapturedRequest.OriginCountryId);
        Assert.Equal(destId.ToString(), testInvoker.CapturedRequest.DestinationCountryId);
        Assert.Equal("2026-12-01", testInvoker.CapturedRequest.PlannedDepartureDate);
        Assert.Single(testInvoker.CapturedRequest.HorseIds);
        Assert.Equal(horseId.ToString(), testInvoker.CapturedRequest.HorseIds[0]);

        // Verify deadline is around 5 seconds in future
        Assert.NotNull(testInvoker.CapturedOptions.Deadline);
        var diff = testInvoker.CapturedOptions.Deadline.Value - DateTime.UtcNow;
        Assert.True(diff.TotalSeconds > 2 && diff.TotalSeconds <= 6, $"Deadline diff was {diff.TotalSeconds} seconds");

        Assert.Equal(tripId, result.TripId);
        Assert.True(result.IsCompliant);
    }

    private class TestCallInvoker : CallInvoker
    {
        public VerifyTripComplianceRequest? CapturedRequest { get; private set; }
        public CallOptions CapturedOptions { get; private set; }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            CapturedRequest = request as VerifyTripComplianceRequest;
            CapturedOptions = options;

            var fakeResponse = new VerifyTripComplianceResponse
            {
                TripId = CapturedRequest?.TripId ?? Guid.NewGuid().ToString(),
                IsCompliant = true,
                TotalRequired = 1,
                TotalMet = 1,
                TotalMissing = 0,
                EvaluatedAt = DateTime.UtcNow.ToString("o")
            };

            return new AsyncUnaryCall<TResponse>(
                Task.FromResult((TResponse)(object)fakeResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotImplementedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotImplementedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotImplementedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotImplementedException();
    }
}
