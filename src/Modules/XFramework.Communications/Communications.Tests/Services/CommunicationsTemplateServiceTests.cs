using IdentityServer.Domain.Shared.Contracts;
using Communications.Api.Services;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;

namespace Communications.Tests.Services;

public sealed class CommunicationsTemplateServiceTests
{
    [Test]
    public async Task GetTemplatesAsync_WhenNoRowsExist_SeedsSystemTemplatesOnce()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = CreateService(dataContext, tenantId);

        var first = await service.GetTemplatesAsync(ListRequest(tenantId));
        var second = await service.GetTemplatesAsync(ListRequest(tenantId));

        Assert.That(first.IsSuccess, Is.True, first.Message);
        Assert.That(second.IsSuccess, Is.True, second.Message);
        Assert.That(first.Data!.Items.Count(item => item.TemplateType == MessageTemplateTypes.System), Is.EqualTo(3));
        Assert.That(dataContext.Set<MessageTemplate>().Count(item => item.TemplateType == MessageTemplateTypes.System), Is.EqualTo(3));
    }

    [Test]
    public async Task UpdateTemplateAsync_WhenSystemTemplate_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = CreateService(dataContext, tenantId);
        var seeded = await service.GetTemplatesAsync(ListRequest(tenantId));
        var systemTemplate = seeded.Data!.Items.Single(item => item.Key == MessageTemplateKeys.IdentityOtp);

        var result = await service.UpdateTemplateAsync(new UpdateMessageTemplateRequest
        {
            TemplateId = systemTemplate.Id,
            Name = "Editable OTP",
            Metadata = Metadata(tenantId)
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task CreateTemplateAsync_WhenDuplicateActiveTenantKey_ReturnsValidationError()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new InMemoryDataContext(), tenantId);

        var first = await service.CreateTemplateAsync(CreateTenantRequest(tenantId, "Announcements.Welcome"));
        var second = await service.CreateTemplateAsync(CreateTenantRequest(tenantId, "announcements.welcome"));

        Assert.That(first.IsSuccess, Is.True, first.Message);
        Assert.That(second.IsSuccess, Is.False);
        Assert.That(second.Errors, Contains.Key("Key"));
    }

    [Test]
    public async Task CreateTemplateAsync_WhenTemplateTypeIsUnknown_ReturnsValidationError()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new InMemoryDataContext(), tenantId);

        var result = await service.CreateTemplateAsync(new CreateMessageTemplateRequest
        {
            TemplateType = "Organization",
            Key = "custom.invalid",
            Name = "Invalid",
            Body = "Invalid",
            Metadata = Metadata(tenantId)
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Contains.Key("TemplateType"));
    }

    [Test]
    public async Task RenderTemplateAsync_ByKey_PrefersTenantTemplateAndReplacesTokensCaseInsensitive()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new InMemoryDataContext(), tenantId);
        var created = await service.CreateTemplateAsync(new CreateMessageTemplateRequest
        {
            TemplateType = MessageTemplateTypes.Tenant,
            Key = MessageTemplateKeys.CommunicationsGeneric,
            Name = "Tenant Generic",
            Body = "Tenant says |Message|",
            RequiredVariables = ["Message"],
            Metadata = Metadata(tenantId)
        });

        var rendered = await service.RenderTemplateAsync(new RenderMessageTemplateRequest
        {
            TemplateKey = MessageTemplateKeys.CommunicationsGeneric,
            TemplateVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["message"] = "hello"
            },
            Metadata = Metadata(tenantId)
        });

        Assert.That(created.IsSuccess, Is.True, created.Message);
        Assert.That(rendered.IsSuccess, Is.True, rendered.Message);
        Assert.That(rendered.Data!.TemplateType, Is.EqualTo(MessageTemplateTypes.Tenant));
        Assert.That(rendered.Data.Body, Is.EqualTo("Tenant says hello"));
    }

    [Test]
    public async Task RenderTemplateAsync_WhenRequiredVariableMissing_ReturnsValidationError()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(new InMemoryDataContext(), tenantId);

        var result = await service.RenderTemplateAsync(new RenderMessageTemplateRequest
        {
            TemplateKey = MessageTemplateKeys.IdentityOtp,
            Metadata = Metadata(tenantId)
        });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Contains.Key("TemplateVariables"));
    }

    [Test]
    public async Task RenderTemplateAsync_WhenUserTemplateRequestedByOtherCredential_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var ownerCredentialId = Guid.NewGuid();
        var otherCredentialId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(Credential(ownerCredentialId, tenantId), Credential(otherCredentialId, tenantId));
        var service = CreateService(dataContext, tenantId);
        var created = await service.CreateTemplateAsync(new CreateMessageTemplateRequest
        {
            TemplateType = MessageTemplateTypes.User,
            OwnerCredentialId = ownerCredentialId,
            Key = "quick.reply",
            Name = "Quick Reply",
            Body = "Thanks, |Name|",
            RequiredVariables = ["Name"],
            Metadata = Metadata(tenantId, ownerCredentialId)
        });

        var result = await service.RenderTemplateAsync(new RenderMessageTemplateRequest
        {
            TemplateId = created.Data!.Id,
            TemplateVariables = new Dictionary<string, string> { ["Name"] = "Ava" },
            Metadata = Metadata(tenantId, otherCredentialId)
        });

        Assert.That(created.IsSuccess, Is.True, created.Message);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task DeleteTemplateAsync_WhenTenantTemplate_MarksTemplateDeleted()
    {
        var tenantId = Guid.NewGuid();
        var dataContext = new InMemoryDataContext();
        var service = CreateService(dataContext, tenantId);
        var created = await service.CreateTemplateAsync(CreateTenantRequest(tenantId, "tenant.delete"));

        var result = await service.DeleteTemplateAsync(new DeleteMessageTemplateRequest
        {
            TemplateId = created.Data!.Id,
            Metadata = Metadata(tenantId)
        });

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var stored = dataContext.Set<MessageTemplate>().Single(template => template.Id == created.Data.Id);
        Assert.That(stored.IsDeleted, Is.True);
        Assert.That(stored.IsEnabled, Is.False);
        Assert.That(stored.DeletedAt, Is.Not.Null);
    }

    [Test]
    public async Task GetTemplatesAsync_BackfillsLegacyOtpRegistryTemplateAsTenantTemplate()
    {
        var tenantId = Guid.NewGuid();
        var group = RegistryGroup(Guid.NewGuid(), tenantId, "CommunicationsService_Otp");
        var dataContext = new InMemoryDataContext();
        dataContext.Seed(
            group,
            RegistryConfig(Guid.NewGuid(), tenantId, group, "MessageTemplate", "Legacy code |Value|"));
        var service = CreateService(dataContext, tenantId);

        var result = await service.GetTemplatesAsync(ListRequest(tenantId));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        var backfilled = result.Data!.Items.Single(item =>
            item.TemplateType == MessageTemplateTypes.Tenant &&
            item.Key == MessageTemplateKeys.IdentityOtp);
        Assert.That(backfilled.Body, Is.EqualTo("Legacy code |Value|"));
        Assert.That(backfilled.IsDefault, Is.True);
    }

    private static CommunicationsTemplateService CreateService(
        InMemoryDataContext dataContext,
        params Guid[] tenantIds) =>
        new(
            dataContext,
            new FakeTenantResolver(tenantIds),
            new CommunicationsRequestContextResolver(
                new HttpContextAccessor(),
                TestConfiguration(),
                serviceInvocationResolver: new FakeTrustedServiceInvocationResolver()),
            NullLogger<CommunicationsTemplateService>.Instance);

    private static GetMessageTemplatesRequest ListRequest(Guid tenantId) => new()
    {
        IncludeInactive = true,
        PageSize = 100,
        Metadata = Metadata(tenantId)
    };

    private static CreateMessageTemplateRequest CreateTenantRequest(Guid tenantId, string key) => new()
    {
        TemplateType = MessageTemplateTypes.Tenant,
        Key = key,
        Name = "Tenant Template",
        Body = "Hello |Name|",
        RequiredVariables = ["Name"],
        Metadata = Metadata(tenantId)
    };

    private static RequestMetadata Metadata(Guid tenantId, Guid? credentialId = null)
    {
        var metadata = new RequestMetadata
        {
            TenantId = tenantId,
            CredentialId = credentialId,
            Name = "XFramework.Portal",
            ServiceAccessToken = FakeTrustedServiceInvocationResolver.ValidPortalToken
        };
        return metadata;
    }

    private static IConfiguration TestConfiguration() =>
        new ConfigurationBuilder()
            .Build();

    private static IdentityCredential Credential(Guid id, Guid tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        UserName = $"user-{id:N}",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

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
        RegistryConfigurationGroup group,
        string key,
        string value) => new()
    {
        Id = id,
        TenantId = tenantId,
        GroupId = group.Id,
        Group = group,
        Key = key,
        Value = value,
        Unit = "string",
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
                throw new ArgumentNullException(nameof(id));

            if (!_tenantIds.Contains(id.Value))
                throw new InvalidOperationException($"Tenant '{id}' could not be found.");

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
