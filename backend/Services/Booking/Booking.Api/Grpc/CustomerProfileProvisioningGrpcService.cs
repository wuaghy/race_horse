using Booking.Application.Customers;
using Contracts.Grpc.CustomerProfile;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Booking.Api.Grpc;

public sealed class CustomerProfileProvisioningGrpcService(ICustomerHorseService customerService)
    : CustomerProfileProvisioning.CustomerProfileProvisioningBase
{
    public override async Task<Empty> ProvisionCustomer(ProvisionCustomerRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.IdentityUserId, out var identityUserId) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Identity user ID, email, and full name are required."));
        }

        await customerService.ProvisionProfileAsync(
            identityUserId,
            request.Email,
            request.FullName,
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone,
            context.CancellationToken);
        return new Empty();
    }
}
