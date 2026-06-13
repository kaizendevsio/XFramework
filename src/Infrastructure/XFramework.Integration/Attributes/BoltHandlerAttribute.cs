namespace XFramework.Integration.Attributes;

/// <summary>
/// Marks a static method as a Bolt handler in addition to being a REST endpoint handler.
/// The source generator scans for this attribute and generates the IBoltHandler adapter
/// that routes incoming Bolt thin-protocol messages to this method.
///
/// The decorated method must:
///   - Be static
///   - Have its first parameter be the request type (implementing IBoltRequest)
///   - Return Task&lt;Result&lt;T&gt;&gt; or Task&lt;Result&gt;
///   - Have remaining parameters resolvable from DI (services, CancellationToken)
///
/// Example:
///   [BoltHandler]
///   public static async Task&lt;Result&lt;AuthenticateIdentityResponse&gt;&gt; Handle(
///       AuthenticateIdentityRequest request,
///       IAuthService authService,
///       CancellationToken ct) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class BoltHandlerAttribute : Attribute;
