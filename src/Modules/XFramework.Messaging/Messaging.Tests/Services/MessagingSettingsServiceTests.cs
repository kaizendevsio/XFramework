using IdentityServer.Domain.Shared.Contracts;
using Messaging.Api.Services;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Requests.Settings;
using Messaging.Domain.Shared.Contracts.Responses;
using Messaging.Tests.Infrastructure;
using NUnit.Framework;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;

namespace Messaging.Tests.Services;

public sealed class MessagingSettingsServiceTests
{
    [Test]
    public async Task GetSettingsAsync_WhenNoRegistryRowsExist_ReturnsDefaults()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = CreateService(dataContext, tenantId);

        var result = await service.GetSettingsAsync(Request(tenantId));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var setting = FindSetting(result.Data!, "Messaging.Chat", "DirectThreads.Enabled");
        Assert.That(setting.Value, Is.EqualTo("true"));
        Assert.That(setting.Source, Is.EqualTo(MessagingSettingSources.Default));
    }

    [Test]
    public async Task UpdateSettingsAsync_WhenRowsDoNotExist_CreatesTenantRegistryRows()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = CreateService(dataContext, tenantId);

        var result = await service.UpdateSettingsAsync(UpdateRequest(
            tenantId,
            "Messaging.Chat",
            "DirectThreads.Enabled",
            "false"));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var config = dataContext.Set<RegistryConfiguration>()
            .Single(x => x.TenantId == tenantId && x.Key == "DirectThreads.Enabled");
        Assert.That(config.Value, Is.EqualTo("false"));

        var responseSetting = FindSetting(result.Data!, "Messaging.Chat", "DirectThreads.Enabled");
        Assert.That(responseSetting.Value, Is.EqualTo("false"));
        Assert.That(responseSetting.Source, Is.EqualTo(MessagingSettingSources.Stored));
    }

    [Test]
    public async Task UpdateSettingsAsync_WhenRowExists_UpdatesStoredValue()
    {
        var tenantId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            RegistryGroup(groupId, tenantId, "Messaging.Chat"),
            RegistryConfig(Guid.NewGuid(), tenantId, groupId, "DirectThreads.Enabled", "true"));
        var service = CreateService(dataContext, tenantId);

        var result = await service.UpdateSettingsAsync(UpdateRequest(
            tenantId,
            "Messaging.Chat",
            "DirectThreads.Enabled",
            "false"));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var configs = dataContext.Set<RegistryConfiguration>()
            .Where(x => x.TenantId == tenantId && x.Key == "DirectThreads.Enabled")
            .ToList();
        Assert.That(configs, Has.Count.EqualTo(1));
        Assert.That(configs[0].Value, Is.EqualTo("false"));
    }

    [Test]
    public async Task UpdateSettingsAsync_WhenKeyIsUnknown_ReturnsValidationError()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new InMemoryDataContext(), tenantId);

        var result = await service.UpdateSettingsAsync(UpdateRequest(
            tenantId,
            "Messaging.Chat",
            "Unknown.Setting",
            "true"));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Is.Not.Null);
    }

    [Test]
    public async Task UpdateSettingsAsync_WhenNumberIsInvalid_ReturnsValidationError()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new InMemoryDataContext(), tenantId);

        var result = await service.UpdateSettingsAsync(UpdateRequest(
            tenantId,
            "Messaging.Chat",
            "GroupThreads.MaxMembers",
            "-1"));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Is.Not.Null);
    }

    [Test]
    public async Task GetSettingsAsync_DoesNotReadRowsFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            RegistryGroup(otherGroupId, otherTenantId, "Messaging.Chat"),
            RegistryConfig(Guid.NewGuid(), otherTenantId, otherGroupId, "DirectThreads.Enabled", "false"));
        var service = CreateService(dataContext, tenantId, otherTenantId);

        var result = await service.GetSettingsAsync(Request(tenantId));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var setting = FindSetting(result.Data!, "Messaging.Chat", "DirectThreads.Enabled");
        Assert.That(setting.Value, Is.EqualTo("true"));
        Assert.That(setting.Source, Is.EqualTo(MessagingSettingSources.Default));
    }

    private static MessagingSettingsService CreateService(
        InMemoryDataContext dataContext,
        params Guid[] tenantIds) =>
        new(dataContext, new FakeTenantResolver(tenantIds));

    private static GetMessagingSettingsRequest Request(Guid tenantId) => new()
    {
        Metadata = new RequestMetadata
        {
            TenantId = tenantId
        }
    };

    private static UpdateMessagingSettingsRequest UpdateRequest(
        Guid tenantId,
        string groupName,
        string key,
        string value) => new()
    {
        Metadata = new RequestMetadata
        {
            TenantId = tenantId
        },
        Values =
        [
            new UpdateMessagingSettingValueRequest
            {
                GroupName = groupName,
                Key = key,
                Value = value
            }
        ]
    };

    private static MessagingSettingValueResponse FindSetting(
        MessagingSettingsResponse response,
        string groupName,
        string key) =>
        response.Groups
            .Single(group => group.GroupName == groupName)
            .Settings
            .Single(setting => setting.Key == key);

    private static RegistryConfigurationGroup RegistryGroup(
        Guid id,
        Guid tenantId,
        string name) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        Description = name,
        SystemReferenceId = Guid.NewGuid(),
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static RegistryConfiguration RegistryConfig(
        Guid id,
        Guid tenantId,
        Guid groupId,
        string key,
        string value) => new()
    {
        Id = id,
        TenantId = tenantId,
        GroupId = groupId,
        Key = key,
        Value = value,
        Unit = "boolean",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private sealed class FakeTenantResolver(params Guid[] tenantIds) : ITenantResolver
    {
        private readonly HashSet<Guid> _tenantIds = tenantIds.ToHashSet();

        public Task<Tenant> GetTenant(Guid? id)
        {
            if (id is null || id == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (!_tenantIds.Contains(id.Value))
            {
                throw new InvalidOperationException($"Tenant '{id}' could not be found.");
            }

            return Task.FromResult(new Tenant
            {
                Id = id.Value,
                TenantId = id.Value,
                Name = "Tenant",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }
    }
}
