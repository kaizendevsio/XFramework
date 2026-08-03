using System.Net;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category("Area:DataContext")]
[Category("Area:GeneratedEndpoints")]
public sealed class GeneratedEntityCrudTests : IntegrationTestBase
{
    [Test]
    public async Task GeneratedIdentityAddressCreateAndUpdate_AssignServerTenantAndPersist()
    {
        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            FirstName = "Generated",
            LastName = "Crud",
            IsEnabled = true
        };
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Set<IdentityInformation>().Add(identity);
            await seedDb.SaveChangesAsync();
        }

        var createResponse = await HttpClient.PostAsJsonAsync(
            "/api/identity-addresses",
            new CreateIdentityAddressRequest
            {
                IdentityInfoId = identity.Id,
                Street = "Initial street",
                ConsolidatedName = "Initial address"
            });

        createResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await createResponse.Content.ReadAsStringAsync());
        var created = await createResponse.Content.ReadFromJsonAsync<IdentityAddress>();
        created.Should().NotBeNull();
        created!.TenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        created.ConcurrencyStamp.Should().NotBeEmpty();

        var updateResponse = await HttpClient.PutAsJsonAsync(
            $"/api/identity-addresses/{created.Id}?expectedConcurrencyStamp={created.ConcurrencyStamp}",
            new UpdateIdentityAddressRequest
            {
                IdentityInfoId = identity.Id,
                Street = "Updated street",
                ConsolidatedName = "Updated address"
            });

        updateResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync());
        var updated = await updateResponse.Content.ReadFromJsonAsync<IdentityAddress>();
        updated.Should().NotBeNull();
        updated!.ConcurrencyStamp.Should().NotBeEmpty();
        updated.ConcurrencyStamp.Should().NotBe(created.ConcurrencyStamp);

        var staleUpdateResponse = await HttpClient.PutAsJsonAsync(
            $"/api/identity-addresses/{created.Id}?expectedConcurrencyStamp={created.ConcurrencyStamp}",
            new UpdateIdentityAddressRequest
            {
                IdentityInfoId = identity.Id,
                Street = "Stale street",
                ConsolidatedName = "Stale address"
            });

        staleUpdateResponse.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            await staleUpdateResponse.Content.ReadAsStringAsync());

        await using var assertionDb = CreateDbContext();
        var persisted = await assertionDb.Set<IdentityAddress>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);
        persisted.TenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        persisted.Street.Should().Be("Updated street");
        persisted.ConcurrencyStamp.Should().Be(updated.ConcurrencyStamp);
    }

    [Test]
    public async Task GeneratedIdentityAddressDelete_RejectsStaleConcurrencyStamp()
    {
        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            FirstName = "Generated",
            LastName = "Delete",
            IsEnabled = true
        };
        var address = new IdentityAddress
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = identity.Id,
            Street = "Delete street",
            ConsolidatedName = "Delete address",
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Set<IdentityInformation>().Add(identity);
            seedDb.Set<IdentityAddress>().Add(address);
            await seedDb.SaveChangesAsync();
        }

        var staleResponse = await HttpClient.DeleteAsync(
            $"/api/identity-addresses/{address.Id}?expectedConcurrencyStamp={Guid.NewGuid()}");

        staleResponse.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            await staleResponse.Content.ReadAsStringAsync());

        var deleteResponse = await HttpClient.DeleteAsync(
            $"/api/identity-addresses/{address.Id}?expectedConcurrencyStamp={address.ConcurrencyStamp}");

        deleteResponse.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            await deleteResponse.Content.ReadAsStringAsync());

        await using var assertionDb = CreateDbContext();
        var exists = await assertionDb.Set<IdentityAddress>()
            .AnyAsync(item => item.Id == address.Id);
        exists.Should().BeFalse();

        var deleted = await assertionDb.Set<IdentityAddress>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == address.Id);
        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().NotBeNull();
    }
}
