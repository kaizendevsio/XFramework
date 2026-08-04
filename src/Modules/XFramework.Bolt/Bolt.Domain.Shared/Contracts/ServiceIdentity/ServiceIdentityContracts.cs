using Bolt.Domain.Shared.Contracts.Requests;
using MemoryPack;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Domain.Shared.ServiceIdentity;

[MemoryPackable]
public partial record IssueServiceTokenRequest : RequestBase
{
    [MemoryPackOrder(1)] public string ClientId { get; set; } = string.Empty;
    [MemoryPackOrder(2)] public string ClientSecret { get; set; } = string.Empty;
    [MemoryPackOrder(3)] public string Audience { get; set; } = string.Empty;
    [MemoryPackOrder(4)] public List<string> Scopes { get; set; } = [];
}

[MemoryPackable]
public partial record GetServiceSigningKeysRequest : RequestBase,
    IBoltRequest<GetServiceSigningKeysRequest, QueryResponse<ServiceSigningKeysResponse>>
{
    [MemoryPackOrder(1)] public string? KeyId { get; set; }
}

[MemoryPackable]
public partial record RotateServiceSigningKeyRequest : RequestBase,
    IBoltRequest<RotateServiceSigningKeyRequest, QueryResponse<ServiceSigningKeyResponse>>
{
    [MemoryPackOrder(1)] public string? Reason { get; set; }
}

[MemoryPackable]
public partial record RetireServiceSigningKeyRequest : RequestBase,
    IBoltRequest<RetireServiceSigningKeyRequest, QueryResponse<ServiceSigningKeyResponse>>
{
    [MemoryPackOrder(1)] public string KeyId { get; set; } = string.Empty;
}

[MemoryPackable]
public partial record ServiceTokenResponse
{
    [MemoryPackOrder(0)] public string AccessToken { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public string TokenType { get; set; } = "Bearer";
    [MemoryPackOrder(2)] public DateTime ExpiresAtUtc { get; set; }
}

[MemoryPackable]
public partial record ServiceSigningKeysResponse
{
    [MemoryPackOrder(0)] public List<ServiceSigningKeyResponse> Keys { get; set; } = [];
    [MemoryPackOrder(1)] public Dictionary<string, List<string>> CredentialGenerationsByClient { get; set; } = [];
}

[MemoryPackable]
public partial record ServiceSigningKeyResponse
{
    [MemoryPackOrder(0)] public string KeyId { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public string Algorithm { get; set; } = "RS256";
    [MemoryPackOrder(2)] public string PublicKeyPem { get; set; } = string.Empty;
    [MemoryPackOrder(3)] public DateTime CreatedAtUtc { get; set; }
    [MemoryPackOrder(4)] public DateTime? ActivatedAtUtc { get; set; }
    [MemoryPackOrder(5)] public DateTime? RetiredAtUtc { get; set; }
    [MemoryPackOrder(6)] public bool IsActive { get; set; }
}
