using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using IdentityServer.Domain.Shared.Contracts;
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

            // Warmup Bolt transport path (service wrapper → SignalR hub → handler → response)
            try
            {
                await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
                    CreateAuthRequest(username, password));
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"[Warmup] Bolt warmup {i} failed: {ex.GetType().Name}: {ex.Message}");
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

        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(static property => property.Name).Should().BeEquivalentTo(
            "identity",
            "credential",
            "accessToken",
            "tokenType",
            "expiresIn",
            "refreshToken",
            "sessionId");
        document.RootElement.TryGetProperty("data", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("isSuccess", out _).Should().BeFalse();

        var result = JsonSerializer.Deserialize<AuthenticateIdentityResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().BeGreaterThan(0);
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.SessionId.Should().NotBeNull().And.NotBe(Guid.Empty);
        result.Credential.Should().NotBeNull();
        result.Credential!.Id.Should().Be(credential.Id);

        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        result.ExpiresIn.Should().Be((int)(accessToken.ValidTo - accessToken.ValidFrom).TotalSeconds);

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

    #region Bolt Transport Tests (service wrapper → SignalR hub → handler → response)

    [Test]
    public async Task Bolt_Authenticate_WithValidCredentials_ReturnsTokenAndSession()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (result, elapsed) = await TimedBoltCall(CreateAuthRequest(username, password));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().NotBeNullOrEmpty();
        result.Response.RefreshToken.Should().NotBeNullOrEmpty();
        result.Response.SessionId.Should().NotBeNull();
        result.Response.Credential.Should().NotBeNull();
        result.Response.Credential!.Id.Should().Be(credential.Id);

        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_WithWrongPassword_Returns400()
    {
        var username = UniqueUsername();
        await SeedCredentialWithRole(username, "CorrectPassword123!");

        var (result, elapsed) = await TimedBoltCall(CreateAuthRequest(username, "WrongPassword!"));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_WithNonExistentUser_Returns404()
    {
        var (result, elapsed) = await TimedBoltCall(
            CreateAuthRequest("nonexistent_" + Guid.NewGuid().ToString("N"), "SomePassword!"));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_WithEmptyUsername_Returns400()
    {
        var (result, elapsed) = await TimedBoltCall(CreateAuthRequest("", "SomePassword!"));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_WithEmptyPassword_Returns400()
    {
        var (result, elapsed) = await TimedBoltCall(CreateAuthRequest(UniqueUsername(), ""));

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_WithEmptyRoleId_Returns400()
    {
        var username = UniqueUsername();
        await SeedCredentialWithRole(username, "ValidPassword123!");

        var request = CreateAuthRequest(username, "ValidPassword123!");
        request.RoleId = Guid.Empty;

        var (result, elapsed) = await TimedBoltCall(request);

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_CreatesSessionInDatabase()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (_, elapsed) = await TimedBoltCall(CreateAuthRequest(username, password));

        await using var db = CreateDbContext();
        var session = await db.Set<Session>()
            .Where(s => s.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        session.Should().NotBeNull();
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_LogsAuthorizationAttempt()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);

        var (_, elapsed) = await TimedBoltCall(CreateAuthRequest(username, password));

        await using var db = CreateDbContext();
        var log = await db.Set<AuthorizationLog>()
            .Where(l => l.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        LogTiming("Bolt", elapsed);
    }

    [Test]
    public async Task Bolt_Authenticate_WithLongIpAddress_PersistsAuthorizationLog()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var credential = await SeedCredentialWithRole(username, password);
        var request = CreateAuthRequest(username, password);
        request.Metadata.IpAddress = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

        var (result, elapsed) = await TimedBoltCall(request);

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var log = await db.Set<AuthorizationLog>()
            .Where(l => l.CredentialId == credential.Id)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log!.Ipaddress.Should().Be(request.Metadata.IpAddress);
        LogTiming("Bolt", elapsed);
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
    /// Calls IdentityServer via the generated service wrapper through the full Bolt transport.
    /// Test client → SignalR → Bolt hub → IdentityServer handler → response back through SignalR.
    /// </summary>
    private static async Task<(QueryResponse<AuthenticateIdentityResponse>? Result, TimeSpan Elapsed)> TimedBoltCall(
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
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityRole>().Add(role);

        await db.SaveChangesAsync();
        return credential;
    }

    #endregion
}
