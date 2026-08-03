namespace XFramework.Core.Services.FeatureGates;

/// <summary>
/// Declares the tenant credential capability required by an endpoint.
/// </summary>
public sealed record TenantCapabilityRequirement(string CapabilityKey);
