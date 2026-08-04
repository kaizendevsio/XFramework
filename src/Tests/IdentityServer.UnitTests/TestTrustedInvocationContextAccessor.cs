using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

internal sealed class TestTrustedInvocationContextAccessor(TrustedInvocationContext? current = null)
    : ITrustedInvocationContextStore
{
    public TrustedInvocationContext? Current { get; set; } = current;

    public void Set(TrustedInvocationContext context) => Current = context;
}
