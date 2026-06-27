using IdentityServer.Domain.Shared.Contracts;
using Communications.Api.Services;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;

namespace Communications.Tests.Services;

public sealed class CommunicationsAdminReadServiceTests
{
    private const string TrustedMetadataSecret = "communications-admin-read-test-secret";

    [Test]
    public async Task QueryUsersAsync_ReturnsOnlyRowsForRequestedTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var threadType = ThreadType(Guid.NewGuid(), tenantId);
        var otherThreadType = ThreadType(Guid.NewGuid(), otherTenantId);
        var credential = Credential(Guid.NewGuid(), tenantId, "tenant-user");
        var otherCredential = Credential(Guid.NewGuid(), otherTenantId, "other-user");
        var thread = Thread(Guid.NewGuid(), tenantId, threadType, "Tenant Thread");
        var otherThread = Thread(Guid.NewGuid(), otherTenantId, otherThreadType, "Other Thread");
        var member = Member(Guid.NewGuid(), tenantId, thread, credential);
        var otherMember = Member(Guid.NewGuid(), otherTenantId, otherThread, otherCredential);
        dataContext.Seed(
            threadType,
            otherThreadType,
            credential.IdentityInfo!,
            otherCredential.IdentityInfo!,
            credential,
            otherCredential,
            thread,
            otherThread,
            member,
            otherMember,
            Message(Guid.NewGuid(), tenantId, thread, member, "tenant message"),
            Message(Guid.NewGuid(), otherTenantId, otherThread, otherMember, "other message"));
        var service = CreateService(dataContext);

        var result = await service.QueryUsersAsync(new QueryCommunicationsAdminUsersRequest
        {
            Metadata = Metadata(tenantId),
            Grid = new CommunicationsAdminGridRequest { Count = 20 }
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.Summary.CommunicationsUserCount, Is.EqualTo(1));
        Assert.That(result.Data.Items, Has.Count.EqualTo(1));
        Assert.That(result.Data.Items[0].CredentialId, Is.EqualTo(credential.Id));
        Assert.That(result.Data.Items[0].DisplayName, Is.EqualTo("tenant-user"));
    }

    [Test]
    public async Task QueryThreadsAsync_SearchesPagesAndTruncatesMessagePreview()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var threadType = ThreadType(Guid.NewGuid(), tenantId);
        var credential = Credential(Guid.NewGuid(), tenantId, "tenant-user");
        var supportThread = Thread(Guid.NewGuid(), tenantId, threadType, "Support Thread");
        var generalThread = Thread(Guid.NewGuid(), tenantId, threadType, "General Thread");
        var supportMember = Member(Guid.NewGuid(), tenantId, supportThread, credential);
        var generalMember = Member(Guid.NewGuid(), tenantId, generalThread, credential);
        dataContext.Seed(
            threadType,
            credential.IdentityInfo!,
            credential,
            supportThread,
            generalThread,
            supportMember,
            generalMember,
            Message(Guid.NewGuid(), tenantId, supportThread, supportMember, new string('x', 150)),
            Message(Guid.NewGuid(), tenantId, generalThread, generalMember, "short text"));
        var service = CreateService(dataContext);

        var result = await service.QueryThreadsAsync(new QueryCommunicationsAdminThreadsRequest
        {
            Metadata = Metadata(tenantId),
            Grid = new CommunicationsAdminGridRequest
            {
                SearchText = "Support",
                Count = 10
            }
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TotalItemCount, Is.EqualTo(1));
        Assert.That(result.Data.Items[0].Name, Is.EqualTo("Support Thread"));
        Assert.That(result.Data.Items[0].LastMessagePreview, Does.EndWith("..."));
        Assert.That(result.Data.Items[0].LastMessagePreview.Length, Is.LessThanOrEqualTo(93));
    }

    [Test]
    public async Task GetUserDetailAsync_WhenCredentialBelongsToAnotherTenant_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherCredential = Credential(Guid.NewGuid(), otherTenantId, "other-user");
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(otherCredential.IdentityInfo!, otherCredential);
        var service = CreateService(dataContext);

        var result = await service.GetUserDetailAsync(new GetCommunicationsAdminUserDetailRequest
        {
            Metadata = Metadata(tenantId),
            CredentialId = otherCredential.Id
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetModerationAsync_DoesNotReturnReportsFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var threadType = ThreadType(Guid.NewGuid(), tenantId);
        var otherThreadType = ThreadType(Guid.NewGuid(), otherTenantId);
        var credential = Credential(Guid.NewGuid(), tenantId, "tenant-user");
        var otherCredential = Credential(Guid.NewGuid(), otherTenantId, "other-user");
        var thread = Thread(Guid.NewGuid(), tenantId, threadType, "Tenant Thread");
        var otherThread = Thread(Guid.NewGuid(), otherTenantId, otherThreadType, "Other Thread");
        var member = Member(Guid.NewGuid(), tenantId, thread, credential);
        var otherMember = Member(Guid.NewGuid(), otherTenantId, otherThread, otherCredential);
        var message = Message(Guid.NewGuid(), tenantId, thread, member, "tenant text");
        var otherMessage = Message(Guid.NewGuid(), otherTenantId, otherThread, otherMember, "other text");
        dataContext.Seed(
            threadType,
            otherThreadType,
            credential.IdentityInfo!,
            otherCredential.IdentityInfo!,
            credential,
            otherCredential,
            thread,
            otherThread,
            member,
            otherMember,
            message,
            otherMessage,
            Report(Guid.NewGuid(), tenantId, message, member),
            Report(Guid.NewGuid(), otherTenantId, otherMessage, otherMember));
        var service = CreateService(dataContext);

        var result = await service.GetModerationAsync(new GetCommunicationsAdminModerationRequest
        {
            Metadata = Metadata(tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.OpenReportCount, Is.EqualTo(1));
        Assert.That(result.Data.Reports, Has.Count.EqualTo(1));
        Assert.That(result.Data.Reports[0].Thread, Is.EqualTo("Tenant Thread"));
    }

    [Test]
    public async Task GetModerationAsync_WhenAdminAuditHidden_ReturnsOnlyPolicies()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(PolicySetting(tenantId, "Moderation.AdminAuditVisible", "false"));
        var service = CreateService(dataContext);

        var result = await service.GetModerationAsync(new GetCommunicationsAdminModerationRequest
        {
            Metadata = Metadata(tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.OpenReportCount, Is.EqualTo(0));
        Assert.That(result.Data.Reports, Is.Empty);
        Assert.That(result.Data.Blocks, Is.Empty);
        Assert.That(result.Data.Policies, Is.Not.Empty);
    }

    private static CommunicationsAdminReadService CreateService(InMemoryDataContext dataContext) =>
        new(
            dataContext,
            new CommunicationsRequestContextResolver(new HttpContextAccessor(), TestConfiguration()),
            new CommunicationsPolicyService(dataContext, new MemoryCache(new MemoryCacheOptions())));

    private static RequestMetadata Metadata(Guid tenantId)
    {
        var metadata = new RequestMetadata
        {
            TenantId = tenantId,
            Name = "ControlPanel"
        };
        RequestMetadataTrust.Sign(metadata, TrustedMetadataSecret);
        return metadata;
    }

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Communications:TrustedMetadata:SharedSecret"] = TrustedMetadataSecret
            })
            .Build();

    private static RegistryConfiguration PolicySetting(Guid tenantId, string key, string value) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Key = key,
        Value = value,
        Unit = "policy",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageThreadType ThreadType(Guid id, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = "Chat",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageThread Thread(
        Guid id,
        Guid tenantId,
        MessageThreadType type,
        string name) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        Description = $"{name} description",
        TypeId = type.Id,
        Type = type,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static IdentityCredential Credential(Guid id, Guid tenantId, string userName)
    {
        var identityInfo = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentityName = userName,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        return new IdentityCredential
        {
            Id = id,
            TenantId = tenantId,
            IdentityInfoId = identityInfo.Id,
            IdentityInfo = identityInfo,
            UserName = userName,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
    }

    private static MessageThreadMember Member(
        Guid id,
        Guid tenantId,
        MessageThread thread,
        IdentityCredential credential) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadId = thread.Id,
        MessageThread = thread,
        CredentialId = credential.Id,
        Credential = credential,
        Emoji = string.Empty,
        Alias = credential.UserName ?? string.Empty,
        Description = string.Empty,
        Role = MessageThreadMemberRoles.Member,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static Message Message(
        Guid id,
        Guid tenantId,
        MessageThread thread,
        MessageThreadMember member,
        string text) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageThreadId = thread.Id,
        MessageThread = thread,
        MessageThreadMemberId = member.Id,
        MessageThreadMember = member,
        Text = text,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static MessageReport Report(
        Guid id,
        Guid tenantId,
        Message message,
        MessageThreadMember reporter) => new()
    {
        Id = id,
        TenantId = tenantId,
        MessageId = message.Id,
        ReporterMemberId = reporter.Id,
        Reason = "Spam",
        Details = "Reported for moderation",
        Status = MessageReportStatuses.Open,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };
}
