namespace XFramework.Integration.Attributes;

/// <summary>
/// Marks a static method as a StreamFlow handler in addition to being a REST endpoint handler.
/// The source generator scans for this attribute and generates the SignalR registration code
/// that routes incoming StreamFlow messages to this method.
///
/// The decorated method must:
///   - Be static
///   - Have its first parameter be the request type (implementing IStreamflowRequest)
///   - Return Task&lt;Result&lt;T&gt;&gt; or Task&lt;Result&gt;
///   - Have remaining parameters resolvable from DI (services, CancellationToken)
///
/// Example:
///   [StreamFlowHandler]
///   public static async Task&lt;Result&lt;AuthenticateIdentityResponse&gt;&gt; Handle(
///       AuthenticateIdentityRequest request,
///       IAuthService authService,
///       CancellationToken ct) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class StreamFlowHandlerAttribute : Attribute;
