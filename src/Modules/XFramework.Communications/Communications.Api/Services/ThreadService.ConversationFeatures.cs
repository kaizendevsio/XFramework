using Communications.Domain.Shared;
namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    private Task<bool> FeatureEnabledAsync(Guid tenant, Guid thread, ConversationFeatures feature, CancellationToken ct) =>
        dataContext.Query<MessageThread>()
            .Where(x => x.Id == thread && x.TenantId == tenant && !x.IsDeleted && x.IsEnabled)
            .Where(x => (x.Features & feature) == feature).AnyAsync(ct);

    private Task<bool> HasAnotherAdminAsync(Guid tenant, Guid thread, Guid member, CancellationToken ct) =>
        dataContext.Query<MessageThreadMember>()
            .Where(x => x.TenantId == tenant && x.MessageThreadId == thread && x.Id != member && !x.IsDeleted && x.IsEnabled)
            .Where(x => x.Role == MessageThreadMemberRoles.Admin || x.Role == MessageThreadMemberRoles.Owner).AnyAsync(ct);

    private async Task ConvertDirectToGroupAsync(Guid tenant, Guid thread, CancellationToken ct)
    {
        var direct = await dataContext.Query<MessageDirectThread>()
            .Where(x => x.TenantId == tenant && x.MessageThreadId == thread && !x.IsDeleted && x.IsEnabled).FirstOrDefaultAsync(ct);
        if (direct is null) return;
        direct.IsDeleted = true; direct.IsEnabled = false; direct.DeletedAt = DateTime.UtcNow;
        dataContext.Update(direct);
    }
}
