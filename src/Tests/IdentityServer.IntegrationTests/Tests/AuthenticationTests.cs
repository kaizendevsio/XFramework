using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
public class AuthenticationTests : IntegrationTestBase
{
    [OneTimeSetUp]
    public async Task WarmUp()
    {
        // Warmup: authenticate multiple times via both paths to ensure
        // .NET tiered JIT fully promotes BCrypt hot loops to Tier 1 (optimized) code.
        const int warmupIterations = 2;

        for (var i = 0; i < warmupIterations; i++)
        {
            var username = UniqueUsername();
            var password = "WarmUp123!";
            await SeedCredentialWithRole(username, password);

            var request = CreateAuthRequest(username, password);

            // Warmup HTTP path
            using var warmupClient = new HttpClient { BaseAddress = new Uri(IntegrationTestFixture.IdentityServerUrl) };
            await warmupClient.PostAsJsonAsync("/api/auth/authenticate", request);

            // Warmup StreamFlow transport path (service wrapper → SignalR hub → handler → response)
            try
            {
                await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
                    CreateAuthRequest(username, password));
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"[Warmup] StreamFlow warmup {i} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        TestContext.Out.WriteLine($"[Warmup] {warmupIterations} iterations complete — JIT fully tiered");
    }

    #region HTTP Path Tests

    [Test]
    public async Task Http_Authenticate_WithValidCredentials_ReturnsTokenAndSession()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (response, elapsed) = await TimedHttpPost("/api/auth/authenticate", CreateAuthRequest(username, password));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Response body: {body}");

        var result = System.Text.Json.JsonSerializer.Deserialize<AuthenticateIdentityResponse>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.SessionId.Should().NotBeNull();
        result.Credential.Should().NotBeNull();
        result.Credential!.Id.Should().Be(credential.Id);

        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_WithWrongPassword_Returns400()
    {
        var username = UniqueUsername();
        await SeedCredentialWithRole(username, "CorrectPassword123!");

        var (response, elapsed) = await TimedHttpPost("/api/auth/authenticate",
            CreateAuthRequest(username, "WrongPassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_WithNonExistentUser_Returns404()
    {
        var (response, elapsed) = await TimedHttpPost("/api/auth/authenticate",
            CreateAuthRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "SomePassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_WithEmptyUsername_Returns400()
    {
        var (response, elapsed) = await TimedHttpPost("/api/auth/authenticate",
            CreateAuthRequest("", "SomePassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_WithEmptyPassword_Returns400()
    {
        var (response, elapsed) = await TimedHttpPost("/api/auth/authenticate",
            CreateAuthRequest(UniqueUsername(), ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_WithEmptyRoleId_Returns400()
    {
        var username = UniqueUsername();
        await SeedCredentialWithRole(username, "ValidPassword123!");

        var request = CreateAuthRequest(username, "ValidPassword123!");
        request.RoleId = Guid.Empty;

        var (response, elapsed) = await TimedHttpPost("/api/auth/authenticate", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_CreatesSessionInDatabase()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (_, elapsed) = await TimedHttpPost("/api/auth/authenticate", CreateAuthRequest(username, password));

        await using var db = CreateDbContext();
        var session = await db.Set<Session>()
            .Where(s => s.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        session.Should().NotBeNull();
        LogTiming("HTTP", elapsed);
    }

    [Test]
    public async Task Http_Authenticate_LogsAuthorizationAttempt()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (_, elapsed) = await TimedHttpPost("/api/auth/authenticate", CreateAuthRequest(username, password));

        await using var db = CreateDbContext();
        var log = await db.Set<AuthorizationLog>()
            .Where(l => l.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        LogTiming("HTTP", elapsed);
    }

    #endregion

    #region StreamFlow Transport Tests (service wrapper → SignalR hub → handler → response)

    [Test]
    public async Task StreamFlow_Authenticate_WithValidCredentials_ReturnsTokenAndSession()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (result, elapsed) = await TimedStreamFlowCall(CreateAuthRequest(username, password));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().NotBeNullOrEmpty();
        result.Response.RefreshToken.Should().NotBeNullOrEmpty();
        result.Response.SessionId.Should().NotBeNull();
        result.Response.Credential.Should().NotBeNull();
        result.Response.Credential!.Id.Should().Be(credential.Id);

        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_WithWrongPassword_Returns400()
    {
        var username = UniqueUsername();
        await SeedCredentialWithRole(username, "CorrectPassword123!");

        var (result, elapsed) = await TimedStreamFlowCall(CreateAuthRequest(username, "WrongPassword!"));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_WithNonExistentUser_Returns404()
    {
        var (result, elapsed) = await TimedStreamFlowCall(
            CreateAuthRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "SomePassword!"));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_WithEmptyUsername_Returns400()
    {
        var (result, elapsed) = await TimedStreamFlowCall(CreateAuthRequest("", "SomePassword!"));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_WithEmptyPassword_Returns400()
    {
        var (result, elapsed) = await TimedStreamFlowCall(CreateAuthRequest(UniqueUsername(), ""));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_WithEmptyRoleId_Returns400()
    {
        var username = UniqueUsername();
        await SeedCredentialWithRole(username, "ValidPassword123!");

        var request = CreateAuthRequest(username, "ValidPassword123!");
        request.RoleId = Guid.Empty;

        var (result, elapsed) = await TimedStreamFlowCall(request);

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_CreatesSessionInDatabase()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (_, elapsed) = await TimedStreamFlowCall(CreateAuthRequest(username, password));

        await using var db = CreateDbContext();
        var session = await db.Set<Session>()
            .Where(s => s.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        session.Should().NotBeNull();
        LogTiming("StreamFlow", elapsed);
    }

    [Test]
    public async Task StreamFlow_Authenticate_LogsAuthorizationAttempt()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (_, elapsed) = await TimedStreamFlowCall(CreateAuthRequest(username, password));

        await using var db = CreateDbContext();
        var log = await db.Set<AuthorizationLog>()
            .Where(l => l.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        LogTiming("StreamFlow", elapsed);
    }

    #endregion

    #region Helpers

    private async Task<(HttpResponseMessage Response, TimeSpan Elapsed)> TimedHttpPost<T>(string url, T request)
    {
        var sw = Stopwatch.StartNew();
        var response = await HttpClient.PostAsJsonAsync(url, request);
        sw.Stop();
        return (response, sw.Elapsed);
    }

    /// <summary>
    /// Calls IdentityServer via the generated service wrapper through the full StreamFlow transport.
    /// Test client → SignalR → StreamFlow hub → IdentityServer handler → response back through SignalR.
    /// </summary>
    private static async Task<(QueryResponse<AuthenticateIdentityResponse>? Result, TimeSpan Elapsed)> TimedStreamFlowCall(
        AuthenticateIdentityRequest request)
    {
        var sw = Stopwatch.StartNew();
        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);
        sw.Stop();
        return (result, sw.Elapsed);
    }

    private static void LogTiming(string path, TimeSpan elapsed)
    {
        TestContext.Out.WriteLine($"[{path}] {TestContext.CurrentContext.Test.Name} — {elapsed.TotalMilliseconds:F1}ms");
    }

    private static AuthenticateIdentityRequest CreateAuthRequest(string username, string password) => new()
    {
        UserName = username,
        Password = password,
        RoleId = TestData.RoleTypeId,
        AuthorizationType = AuthorizationType.Default,
        GenerateToken = true,
        Metadata = CreateMetadata()
    };

    private static RequestMetadata CreateMetadata() => new()
    {
        TenantId = IntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "IntegrationTest",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };

    private async Task<IdentityCredential> SeedCredentialWithRole(string username, string password)
    {
        await using var db = CreateDbContext();

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = username,
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        var role = new IdentityRole
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            TypeId = TestData.RoleTypeId,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityRole>().Add(role);

        await db.SaveChangesAsync();
        return credential;
    }

    #endregion
}
