namespace XFramework.Portal.Features.Communications.Services;

/// <summary>
/// Surfaces the message the Communications service actually returned instead of
/// collapsing every failure into a generic "check service health" string.
/// </summary>
public static class CommunicationsPortalErrors
{
    /// <summary>
    /// Returns the service-authored failure message when the read/write services rejected the
    /// request (for example an authorization or tenant-access failure), otherwise the caller's
    /// fallback for transport-level faults that carry no user-facing text.
    /// </summary>
    public static string Describe(Exception exception, string fallback) =>
        exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message)
            ? exception.Message
            : fallback;
}
