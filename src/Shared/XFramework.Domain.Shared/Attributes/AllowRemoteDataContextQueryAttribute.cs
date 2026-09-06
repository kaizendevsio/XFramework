namespace XFramework.Domain.Shared.Attributes;

/// <summary>
/// Explicitly allows remote <c>IDataContext</c> queries for an entity without exposing generated REST CRUD endpoints.
/// </summary>
/// <remarks>
/// Remote queries remain subject to generated actor, tenant, feature, capability, and service-scope authorization.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AllowRemoteDataContextQueryAttribute : Attribute;
