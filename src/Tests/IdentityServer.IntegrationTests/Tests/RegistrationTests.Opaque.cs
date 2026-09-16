using System.Diagnostics;
using System.Net;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Enums;

namespace IdentityServer.IntegrationTests.Tests;

public sealed partial class RegistrationTests
{
    [Test]
    public async Task OpaqueAuth_AnonymousRegistration_AtomicBackupAndVerifiedLogin_NoLegacyFallback()
    {
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        using var browser = new OpaqueBrowser();
        var username = UniqueUsername();
        const string password = "opaque test password only";
        var request = new OpaqueAuthRequest { Stage = "register-start", UserName = username,
            DisplayName = "OPAQUE test", RoleId = IntegrationTestFixture.RegistrationRoleId,
            Metadata = Request().Metadata };
        var start = await browser.Call("startRegistration", new { password });
        request.Message = start.GetProperty("registrationRequest").GetString()!;
        var registration = await wrapper.OpaqueAuth(request);
        registration.IsSuccess.Should().BeTrue(registration.Message);
        var response = registration.Response!;
        var credentialId = Guid.Parse(response.Client.Split(':')[1]);
        var record = await browser.Call("finishRegistration", new { password,
            clientRegistrationState = start.GetProperty("clientRegistrationState").GetString(),
            registrationResponse = response.Message, identifiers = new { client = response.Client, server = response.Server } });
        await using var db = CreateDbContext();
        (await db.Set<IdentityCredential>().AnyAsync(x => x.Id == credentialId)).Should().BeFalse();
        var probe = await browser.Call("startLogin", new { password });
        var directory = OpaqueDirectory();
        var archive = OpaqueArmor("MESSAGE", "encrypted archive test fixture");
        var verify = await wrapper.OpaqueAuth(request with { Stage = "enroll-verify", ExchangeId = response.ExchangeId,
            Record = record.GetProperty("registrationRecord").GetString(), WrappedRecovery = WrappedFixture(),
            Directory = directory, RecoveryArchive = archive, Message = probe.GetProperty("startLoginRequest").GetString()! });
        verify.IsSuccess.Should().BeTrue(verify.Message);
        var proof = await browser.Call("finishLogin", new { password, clientLoginState = probe.GetProperty("clientLoginState").GetString(),
            loginResponse = verify.Response!.Message, identifiers = new { client = response.Client, server = response.Server } });
        var finish = request with { Stage = "enroll-finish", ExchangeId = verify.Response.ExchangeId,
            Message = proof.GetProperty("finishLoginRequest").GetString()!, RecoveryArchive = archive };
        var created = await wrapper.OpaqueAuth(finish);
        created.IsSuccess.Should().BeTrue(created.Message);
        (await wrapper.OpaqueAuth(finish)).IsSuccess.Should().BeFalse("registration state is single use");
        (await db.Set<IdentityCredential>().AsNoTracking().SingleAsync(x => x.Id == credentialId)).PasswordByte.Should().BeNull();
        (await db.Set<OpaqueCredential>().SingleAsync(x => x.CredentialId == credentialId)).WrappedRecovery.Should().Be(WrappedFixture());
        (await db.Set<EncryptionAccount>().SingleAsync(x => x.CredentialId == credentialId)).RecoveryArchive.Should().Be(archive);
        var authenticated = await OpaqueLogin(wrapper, browser, request, password);
        authenticated.Authentication!.Credential!.Id.Should().Be(credentialId);
        authenticated.WrappedRecovery.Should().Be(WrappedFixture());
        (await wrapper.AuthenticateIdentity(new() { UserName = username, Password = password, RoleId = request.RoleId,
            AuthorizationType = AuthorizationType.Username, Metadata = request.Metadata })).IsSuccess.Should().BeFalse();
        // Invalid proofs consume their exchange and enter the existing account lockout accounting.
        var badStart = await browser.Call("startLogin", new { password });
        var bad = await wrapper.OpaqueAuth(request with { Stage = "login-start", Message = badStart.GetProperty("startLoginRequest").GetString()! });
        (await wrapper.OpaqueAuth(request with { Stage = "login-finish", ExchangeId = bad.Response!.ExchangeId, Message = "invalid" })).IsSuccess.Should().BeFalse();
        (await db.Set<IdentityCredential>().AsNoTracking().SingleAsync(x => x.Id == credentialId)).FailedLoginAttempts.Should().BeGreaterThan(0);
        await db.Set<IdentityCredential>().Where(x => x.Id == credentialId).ExecuteUpdateAsync(x => x.SetProperty(c => c.IsEnabled, false));
        var disabledStart = await browser.Call("startLogin", new { password });
        var disabled = await wrapper.OpaqueAuth(request with { Stage = "login-start", Message = disabledStart.GetProperty("startLoginRequest").GetString()! });
        // Disabled identities are either rejected directly or served an unknown-user response.
        if (disabled.IsSuccess)
        {
            var rejected = await browser.Call("finishLogin", new { password, clientLoginState = disabledStart.GetProperty("clientLoginState").GetString(),
                loginResponse = disabled.Response!.Message, identifiers = new { client = disabled.Response.Client, server = disabled.Response.Server } });
            if (rejected.ValueKind != JsonValueKind.Null && rejected.TryGetProperty("finishLoginRequest", out var final))
                (await wrapper.OpaqueAuth(request with { Stage = "login-finish", ExchangeId = disabled.Response.ExchangeId, Message = final.GetString()! })).IsSuccess.Should().BeFalse();
        }
    }

    [Test]
    public async Task OpaqueAuth_UnexpectedServiceCaller_IsForbidden()
    {
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var result = await IntegrationTestFixture.ServiceWrapper.OpaqueAuth(new OpaqueAuthRequest {
            Stage = "options", UserName = UniqueUsername(), RoleId = IntegrationTestFixture.RegistrationRoleId, Metadata = Request().Metadata });
        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task OpaqueAuth_MigrationRequiresBackup_PasswordChangePreservesKeysAndRevokesSessions()
    {
        using var anonymous = IntegrationTestFixture.SuppressActorAccessToken();
        var wrapper = await IntegrationTestFixture.CreateRegistrationServiceWrapper();
        var legacy = Request();
        var created = await wrapper.RegisterIdentity(legacy);
        created.IsSuccess.Should().BeTrue(created.Message);
        var plain = await wrapper.AuthenticateIdentity(new() { UserName = legacy.UserName, Password = legacy.Password,
            RoleId = IntegrationTestFixture.RegistrationRoleId, AuthorizationType = AuthorizationType.Username, Metadata = legacy.Metadata });
        plain.IsSuccess.Should().BeTrue(plain.Message);
        var credential = created.Response!.CredentialId;
        using var browser = new OpaqueBrowser();
        var request = new OpaqueAuthRequest { UserName = legacy.UserName, RoleId = IntegrationTestFixture.RegistrationRoleId, Metadata = legacy.Metadata };
        var registration = await browser.Call("startRegistration", new { password = legacy.Password });
        var begin = request with { Stage = "enroll-start", LegacyPassword = legacy.Password, Message = registration.GetProperty("registrationRequest").GetString()! };
        using (IntegrationTestFixture.UseActorAccessToken(plain.Response!.AccessToken!))
            (await wrapper.OpaqueAuth(begin)).HttpStatusCode.Should().Be(HttpStatusCode.Conflict, "migration must not retire a password before keys are backed up");
        var directory = OpaqueDirectory(); var archive = OpaqueArmor("MESSAGE", "existing backup");
        await using var db = CreateDbContext();
        db.Add(new EncryptionAccount { TenantId = created.Response.TenantId, CredentialId = credential, DirectoryRevision = 1,
            RootPublicKey = directory.RootPublicKey, Roster = directory.Roster, DevicesJson = JsonSerializer.Serialize(directory.Devices), RecoveryArchive = archive, RecoveryRevision = 1 });
        await db.SaveChangesAsync();
        using (IntegrationTestFixture.UseActorAccessToken(plain.Response.AccessToken!))
        {
            var started = await wrapper.OpaqueAuth(begin);
            started.IsSuccess.Should().BeTrue(started.Message);
            await FinishEnrollment(wrapper, browser, request, registration, started.Response!, legacy.Password);
        }
        (await db.Set<IdentityCredential>().AsNoTracking().SingleAsync(x => x.Id == credential)).PasswordByte.Should().BeNull();
        (await db.Set<EncryptionAccount>().AsNoTracking().SingleAsync(x => x.CredentialId == credential)).RecoveryArchive.Should().Be(archive);
        var signedIn = await OpaqueLogin(wrapper, browser, request, legacy.Password);
        const string nextPassword = "replacement password test only";
        var next = await browser.Call("startRegistration", new { password = nextPassword });
        using (IntegrationTestFixture.UseActorAccessToken(signedIn.Authentication!.AccessToken!))
        {
            var started = await wrapper.OpaqueAuth(request with { Stage = "change-start", ExchangeId = signedIn.ExchangeId, Message = next.GetProperty("registrationRequest").GetString()! });
            started.IsSuccess.Should().BeTrue(started.Message);
            await FinishEnrollment(wrapper, browser, request, next, started.Response!, nextPassword);
        }
        (await db.Set<EncryptionAccount>().AsNoTracking().SingleAsync(x => x.CredentialId == credential)).RecoveryArchive.Should().Be(archive);
        (await db.Set<Session>().AsNoTracking().CountAsync(x => x.CredentialId == credential && x.Status == CurrentSessionState.Active)).Should().Be(0);
        await OpaqueLogin(wrapper, browser, request, nextPassword);
        var wrong = await browser.Call("startLogin", new { password = legacy.Password });
        var challenge = await wrapper.OpaqueAuth(request with { Stage = "login-start", Message = wrong.GetProperty("startLoginRequest").GetString()! });
        (await browser.Call("finishLogin", new { password = legacy.Password, clientLoginState = wrong.GetProperty("clientLoginState").GetString(),
            loginResponse = challenge.Response!.Message, identifiers = new { client = challenge.Response.Client, server = challenge.Response.Server } })).ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static async Task FinishEnrollment(IIdentityServerServiceWrapper wrapper, OpaqueBrowser browser,
        OpaqueAuthRequest request, JsonElement registration, OpaqueAuthResponse started, string password)
    {
        var registered = await browser.Call("finishRegistration", new { password,
            clientRegistrationState = registration.GetProperty("clientRegistrationState").GetString(), registrationResponse = started.Message,
            identifiers = new { client = started.Client, server = started.Server } });
        var login = await browser.Call("startLogin", new { password });
        var challenge = await wrapper.OpaqueAuth(request with { Stage = "enroll-verify", ExchangeId = started.ExchangeId,
            Record = registered.GetProperty("registrationRecord").GetString(), WrappedRecovery = WrappedFixture(), Message = login.GetProperty("startLoginRequest").GetString()! });
        challenge.IsSuccess.Should().BeTrue(challenge.Message);
        var finished = await browser.Call("finishLogin", new { password, clientLoginState = login.GetProperty("clientLoginState").GetString(),
            loginResponse = challenge.Response!.Message, identifiers = new { client = started.Client, server = started.Server } });
        var result = await wrapper.OpaqueAuth(request with { Stage = "enroll-finish", ExchangeId = challenge.Response.ExchangeId,
            Message = finished.GetProperty("finishLoginRequest").GetString()! });
        result.IsSuccess.Should().BeTrue(result.Message);
    }

    private static async Task<OpaqueAuthResponse> OpaqueLogin(IIdentityServerServiceWrapper wrapper, OpaqueBrowser browser, OpaqueAuthRequest request, string password)
    {
        var start = await browser.Call("startLogin", new { password });
        var challenge = await wrapper.OpaqueAuth(request with { Stage = "login-start", Message = start.GetProperty("startLoginRequest").GetString()! });
        challenge.IsSuccess.Should().BeTrue(challenge.Message);
        var response = challenge.Response!;
        var proof = await browser.Call("finishLogin", new { password, clientLoginState = start.GetProperty("clientLoginState").GetString(),
            loginResponse = response.Message, identifiers = new { client = response.Client, server = response.Server } });
        var finish = request with { Stage = "login-finish", ExchangeId = response.ExchangeId, Message = proof.GetProperty("finishLoginRequest").GetString()! };
        var result = await wrapper.OpaqueAuth(finish);
        result.IsSuccess.Should().BeTrue(result.Message);
        (await wrapper.OpaqueAuth(finish)).IsSuccess.Should().BeFalse("login proofs cannot be replayed");
        return result.Response!;
    }
    private static string WrappedFixture() => JsonSerializer.Serialize(new { v = 1, iv = Convert.ToBase64String(new byte[12]), ciphertext = Convert.ToBase64String(new byte[80]) });
    private static string OpaqueArmor(string kind, string value) => $"-----BEGIN PGP {kind}-----\n{value}\n-----END PGP {kind}-----";
    private static PutEncryptionDirectoryRequest OpaqueDirectory() => new() { RootPublicKey = OpaqueArmor("PUBLIC KEY BLOCK", "root"), Roster = OpaqueArmor("MESSAGE", "roster"), Devices = [new() {
        DeviceId = Guid.NewGuid(), SigningPublicKey = OpaqueArmor("PUBLIC KEY BLOCK", "signing"), EncryptionPublicKey = OpaqueArmor("PUBLIC KEY BLOCK", "encryption"), Approval = OpaqueArmor("MESSAGE", "approval") }] };
    private sealed class OpaqueBrowser : IDisposable
    {
        private readonly Process process;
        public OpaqueBrowser()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
            var info = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(Path.Combine(root!.FullName, "src/Libraries/XFramework.Opaque.Native/test/client.mjs"));
            process = Process.Start(info)!;
        }
        public async Task<JsonElement> Call(string method, object args)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { method, args }));
            await process.StandardInput.FlushAsync();
            using var data = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)))!);
            return data.RootElement.Clone();
        }
        public void Dispose() { if (!process.HasExited) process.Kill(); process.Dispose(); }
    }
}
