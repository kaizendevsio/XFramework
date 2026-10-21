using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using System.Net;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Portal.Features.POS;

public sealed class PosCustomerCreation(IIdentityServerServiceWrapper identity)
{
    public Guid? IdentityId { get; private set; }
    private Guid? _tenantId;

    public async Task<CmdResponse<IdentityCredential>> CreateAsync(Guid tenantId, string name, string userName,
        string password, RequestMetadata metadata, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || metadata.RequestedTenantId != tenantId ||
            (_tenantId.HasValue && _tenantId != tenantId))
            return Failure("Customer creation tenant changed; reopen the dialog", 403);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(userName) || userName.Trim().Length > 256 ||
            password.Length < 8 || System.Text.Encoding.UTF8.GetByteCount(password) > 72)
            return Failure("Enter a name (maximum 100 characters), username, and a password of at least 8 characters (maximum 72 UTF-8 bytes)", 400);

        _tenantId = tenantId;
        if (IdentityId is null)
        {
            var person = await identity.CreateIdentity(new CreateIdentityRequest
            {
                Metadata = metadata, IdentityName = name.Trim()
            }, ct);
            if (!person.IsSuccess || person.Response is null)
                return Failure(person.Message ?? "Could not create customer", person.IsSuccess ? 502 : (int)person.HttpStatusCode);
            if (person.Response.TenantId != tenantId || person.Response.Id == Guid.Empty)
                return Failure("Customer response does not match the selected tenant", 403);
            IdentityId = person.Response.Id;
        }

        // Retain the identity on credential failure so a retry does not create another person.
        var credential = await identity.CreateCredential(new CreateCredentialRequest
        {
            Metadata = metadata, IdentityInfoId = IdentityId.Value,
            UserAlias = name.Trim(), UserName = userName.Trim(), Password = password
        }, ct);
        if (!credential.IsSuccess || credential.Response is null)
            return Failure(credential.Message ?? "Could not create customer login", credential.IsSuccess ? 502 : (int)credential.HttpStatusCode);
        var created = credential.Response;
        if (created.TenantId != tenantId || created.Id == Guid.Empty)
            return Failure("Customer response does not match the selected tenant", 403);
        return new() { HttpStatusCode = HttpStatusCode.OK, Response = new()
        {
            Id = created.Id, TenantId = created.TenantId, IdentityInfoId = created.IdentityInfoId,
            UserName = created.UserName, UserAlias = created.UserAlias, IsEnabled = created.IsEnabled
        }};
    }

    private static CmdResponse<IdentityCredential> Failure(string message, int status) =>
        new() { HttpStatusCode = (HttpStatusCode)status, Message = message };
}
