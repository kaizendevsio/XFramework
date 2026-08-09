namespace XFramework.Core.Attributes;

/// <summary>
/// Compatibility shim for the canonical shared generated-endpoint metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
[Obsolete(
    "Use XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute. This compatibility shim will be removed in a future breaking release.",
    false)]
public sealed class GenerateEndpointsAttribute
    : global::XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute
{
    public new EndpointType Type
    {
        get => (EndpointType)(int)base.Type;
        set => base.Type = (global::XFramework.Domain.Shared.Attributes.EndpointType)(int)value;
    }

    public new EndpointActions Actions
    {
        get => (EndpointActions)(int)base.Actions;
        set => base.Actions = (global::XFramework.Domain.Shared.Attributes.EndpointActions)(int)value;
    }
}
