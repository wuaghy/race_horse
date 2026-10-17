using Contracts.Grpc.CustomerProfile;
using Identity.Application.Authentication;

namespace Identity.Api.Authentication;

public sealed class BookingCustomerProfileProvisioner(CustomerProfileProvisioning.CustomerProfileProvisioningClient client)
    : ICustomerProfileProvisioner
{
    public async Task ProvisionAsync(Guid identityUserId, string email, string fullName, string? phone, CancellationToken cancellationToken)
    {
        await client.ProvisionCustomerAsync(new ProvisionCustomerRequest
        {
            IdentityUserId = identityUserId.ToString(),
            Email = email,
            FullName = fullName,
            Phone = phone ?? string.Empty
        }, new Grpc.Core.CallOptions(
            deadline: DateTime.UtcNow.AddSeconds(30),
            cancellationToken: cancellationToken));
    }
}
