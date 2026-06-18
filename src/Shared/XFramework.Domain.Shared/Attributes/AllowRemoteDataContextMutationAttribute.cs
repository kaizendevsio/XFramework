namespace XFramework.Domain.Shared.Attributes;

/// <summary>
/// Explicitly allows remote <c>IDataContext</c> mutation for an entity without exposing generated REST CRUD endpoints.
/// </summary>
/// <remarks>
/// Use this only for entities that have an authenticated admin/client surface which intentionally writes through
/// remote <c>IDataContext</c>. Server-side tenant metadata validation still applies.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AllowRemoteDataContextMutationAttribute : Attribute;
