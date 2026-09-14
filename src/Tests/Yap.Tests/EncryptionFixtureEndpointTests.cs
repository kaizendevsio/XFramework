using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

[TestFixture]
public sealed class EncryptionFixtureEndpointTests
{
    [Test]
    public async Task ThreeActors_PreserveDirectoryCas_EncryptedMessageFields_EventsAndLargeOpaqueAttachment()
    {
        // Fixture contract test only: these dummy envelopes are not cryptographic proof.
        // The separate real-browser smoke runs OpenPGP, recovery and SFrame on top of this host.
        using var certificate = Certificate();
        await using var app = UiFixture.Create(0, certificate: certificate, enableEncryption: true);
        await app.StartAsync();
        var origin = new UriBuilder(app.Urls.Single()) { Host = "localhost" }.Uri;
        using var a = await LoginAsync(origin, "fixture", certificate);
        using var b = await LoginAsync(origin, "callee", certificate);
        using var c = await LoginAsync(origin, "third", certificate);
        var clients = new[] { a, b, c };
        var ids = clients.Select(x => x.User.CredentialId).ToList();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(3));
        var chats = (await a.Http.GetFromJsonAsync<ChatPage<Conversation>>("api/chat/conversations"))!;
        Assert.That(chats.Items, Has.Count.EqualTo(1));
        var thread = chats.Items[0].Id;
        foreach (var (fileName, contentType, allowed) in new[]
        {
            ("photo.jpg", "image/jpeg", false),
            ("photo.jpg", "application/octet-stream", false),
            ("attachment.pgp", "image/jpeg", false),
            ("attachment.pgp", "application/octet-stream", true),
            ("voice.pgp", "application/octet-stream", true)
        })
        {
            using var multipart = new MultipartFormDataContent();
            var uploadContent = new ByteArrayContent([1, 2, 3]);
            uploadContent.Headers.ContentType = new(contentType);
            multipart.Add(uploadContent, "file", fileName);
            using var direct = await a.Http.PostAsync($"api/chat/uploads/{thread}", multipart);
            Assert.That(direct.StatusCode, Is.EqualTo(allowed ? HttpStatusCode.OK : HttpStatusCode.Conflict), fileName + " direct upload");
            using var session = await a.Http.PostAsJsonAsync($"api/chat/uploads/{thread}/session", new BeginUpload(fileName, contentType, 3));
            Assert.That(session.StatusCode, Is.EqualTo(allowed ? HttpStatusCode.OK : HttpStatusCode.Conflict), fileName + " resumable upload");
            if (allowed)
            {
                var ticket = (await session.Content.ReadFromJsonAsync<UploadTicket>())!;
                using var part = new ByteArrayContent([1, 2, 3]);
                (await a.Http.PostAsync($"api/chat/uploads/session/{ticket.UploadId}/parts/1?offset=0", part)).EnsureSuccessStatusCode();
                (await a.Http.PostAsJsonAsync($"api/chat/uploads/session/{ticket.UploadId}/complete", new { })).EnsureSuccessStatusCode();
            }
        }
        Assert.That((await a.Http.PostAsJsonAsync("api/chat/message-actions",
            new MessageAction(thread, Guid.NewGuid(), "edit", "stale client plaintext edit"))).StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict), "Encryption-enabled Yap rejects plaintext edits before forwarding them, regardless of legacy message state.");
        var devices = ids.ToDictionary(x => x, _ => Guid.NewGuid());
        foreach (var client in clients)
        {
            Assert.That((await client.Http.GetAsync("api/chat/encryption/directory")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var request = new PutEncryptionDirectoryRequest { RootPublicKey = $"public root {client.User.CredentialId}", Roster = "signed roster",
                Devices = [new() { DeviceId = devices[client.User.CredentialId], SigningPublicKey = "sign", EncryptionPublicKey = "encrypt", Approval = "approved" }] };
            var published = await PostAsync<EncryptionDirectoryResponse>(client.Http, "api/chat/encryption/directory", request);
            Assert.That(published.CredentialId, Is.EqualTo(client.User.CredentialId));
            Assert.That(published.Revision, Is.EqualTo(1));
            Assert.That((await client.Http.PostAsJsonAsync("api/chat/encryption/directory", request)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            if (client == b)
                Assert.That((await a.Http.GetAsync($"api/chat/conversations/{thread}/encryption")).StatusCode,
                    Is.EqualTo(HttpStatusCode.PreconditionRequired), "An unenrolled third recipient must block encryption without silently dropping them.");
        }
        await PostAsync<EncryptionRecoveryResponse>(a.Http, "api/chat/encryption/recovery", new PutEncryptionRecoveryRequest { Archive = "opaque recovery A" });
        Assert.That((await b.Http.GetFromJsonAsync<EncryptionRecoveryResponse>("api/chat/encryption/recovery"))!.Archive, Is.Null);
        Assert.That((await a.Http.GetFromJsonAsync<EncryptionRecoveryResponse>("api/chat/encryption/recovery"))!.Archive, Is.EqualTo("opaque recovery A"));
        Assert.That((await a.Http.PostAsJsonAsync("api/chat/encryption/recovery", new PutEncryptionRecoveryRequest { Archive = "stale" })).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var publicDirectories = (await c.Http.GetFromJsonAsync<EncryptionDirectoryResponse[]>($"api/chat/conversations/{thread}/encryption"))!;
        Assert.That(publicDirectories.Select(x => x.CredentialId), Is.EquivalentTo(ids));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var bEvents = await EventsAsync(b, timeout.Token);
        using var cEvents = await EventsAsync(c, timeout.Token);
        using var bReader = new StreamReader(await bEvents.Content.ReadAsStreamAsync(timeout.Token));
        using var cReader = new StreamReader(await cEvents.Content.ReadAsStreamAsync(timeout.Token));
        foreach (var reader in new[] { bReader, cReader })
        { Assert.That(await reader.ReadLineAsync(timeout.Token), Is.EqualTo(": connected")); await reader.ReadLineAsync(timeout.Token); }
        var messageId = Guid.NewGuid();
        var message = new SendMessage(messageId, thread, "Encrypted message", EncryptedEnvelope: "opaque signed envelope",
            RecipientCredentialIds: ids, EncryptionSenderDeviceId: devices[a.User.CredentialId], SenderDirectoryRevision: 1,
            RecipientDirectoryRevisions: ids.ToDictionary(x => x, _ => 1L));
        await PostAsync<MessageReceipt>(a.Http, "api/chat/messages", message);
        foreach (var reader in new[] { bReader, cReader })
            Assert.That(await reader.ReadLineAsync(timeout.Token), Is.EqualTo("data: refresh"));
        foreach (var client in clients)
        {
            var page = (await client.Http.GetFromJsonAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{thread}/messages"))!;
            Assert.That(page.Items.Single().EncryptedEnvelope, Is.EqualTo(message.EncryptedEnvelope));
            Assert.That(page.Items.Single().SenderId, Is.EqualTo(a.User.CredentialId));
            Assert.That(page.Items.Single().EncryptionSenderDeviceId, Is.EqualTo(devices[a.User.CredentialId]));
            Assert.That(page.Items.Single().AcceptedSenderDirectoryRevision, Is.EqualTo(1));
            Assert.That(page.Items.Single().AttachmentLinksReady, Is.False, "An encrypted envelope does not imply its later attachment link exists yet.");
        }
        // Idempotent replay preserves the stored envelope, but a changed recipient snapshot is rejected.
        await PostAsync<MessageReceipt>(a.Http, "api/chat/messages", message);
        Assert.That((await a.Http.PostAsJsonAsync("api/chat/messages", message with { Id = Guid.NewGuid(), RecipientDirectoryRevisions = [] })).StatusCode,
            Is.EqualTo(HttpStatusCode.Conflict));

        const int total = 80 * 1024 * 1024;
        var upload = await PostAsync<UploadTicket>(a.Http, $"api/chat/uploads/{thread}/session", new BeginUpload("attachment.pgp", "application/octet-stream", total));
        Assert.That(upload.ChunkSizeBytes, Is.EqualTo(ChatLimits.PreferredChunkBytes));
        var chunk = new byte[upload.ChunkSizeBytes]; RandomNumberGenerator.Fill(chunk);
        using var expectedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var offset = 0; offset < total; offset += chunk.Length)
        {
            using var response = await a.Http.PostAsync($"api/chat/uploads/session/{upload.UploadId}/parts/{offset / chunk.Length + 1}?offset={offset}", new ByteArrayContent(chunk));
            response.EnsureSuccessStatusCode(); expectedHash.AppendData(chunk);
        }
        var stored = await PostAsync<StoredFile>(a.Http, $"api/chat/uploads/session/{upload.UploadId}/complete", new { });
        (await a.Http.PostAsJsonAsync("api/chat/attachments", new AttachMessageFile(thread, messageId, stored.Id))).EnsureSuccessStatusCode();
        var attachments = (await c.Http.GetFromJsonAsync<ChatAttachment[]>($"api/chat/conversations/{thread}/messages/{messageId}/attachments"))!;
        Assert.That(attachments.Single().Size, Is.EqualTo(total));
        Assert.That(attachments[0].Id, Is.Not.EqualTo(stored.Id), "The signed storage ID precedes creation of its message attachment link.");
        var account = $"{c.User.TenantId:N}:{c.User.CredentialId:N}";
        Assert.That((await c.Http.GetAsync($"api/chat/conversations/{thread}/messages/{Guid.NewGuid()}/attachments/{stored.Id}?storageId=true&account={account}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound), "A storage ID cannot escape the authorized message's attachment list.");
        using var downloaded = await c.Http.GetAsync($"api/chat/conversations/{thread}/messages/{messageId}/attachments/{stored.Id}?storageId=true&account={account}", HttpCompletionOption.ResponseHeadersRead);
        downloaded.EnsureSuccessStatusCode();
        await using var bytes = await downloaded.Content.ReadAsStreamAsync();
        Assert.That(await SHA256.HashDataAsync(bytes), Is.EqualTo(expectedHash.GetHashAndReset()));
        var linked = (await c.Http.GetFromJsonAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{thread}/messages"))!.Items.Single();
        Assert.That(linked.AttachmentLinksReady, Is.True, "The later link notification can now release the recipient's skeleton.");

        var ownDirectory = (await a.Http.GetFromJsonAsync<EncryptionDirectoryResponse>("api/chat/encryption/directory"))!;
        var newDevice = Guid.NewGuid();
        ownDirectory.Devices.Add(new() { DeviceId = newDevice, SigningPublicKey = "new sign", EncryptionPublicKey = "new encrypt", Approval = "approved" });
        await PostAsync<EncryptionDirectoryResponse>(a.Http, "api/chat/encryption/directory", new PutEncryptionDirectoryRequest
        { ExpectedRevision = 1, RootPublicKey = ownDirectory.RootPublicKey, Roster = "updated signed roster", Devices = ownDirectory.Devices });
        var edit = new MessageAction(thread, messageId, "edit", "Encrypted message", EncryptedEnvelope: "edited opaque signed envelope",
            EncryptionSenderDeviceId: newDevice, SenderDirectoryRevision: 2,
            RecipientDirectoryRevisions: ids.ToDictionary(x => x, x => x == a.User.CredentialId ? 2L : 1L));
        Assert.That((await a.Http.PostAsJsonAsync("api/chat/message-actions", edit with { SenderDirectoryRevision = 1 })).StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        (await a.Http.PostAsJsonAsync("api/chat/message-actions", edit)).EnsureSuccessStatusCode();
        Assert.That((await b.Http.PostAsJsonAsync("api/chat/message-actions", edit)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await a.Http.PostAsJsonAsync("api/chat/message-actions", edit with { EncryptedEnvelope = null, Text = "plaintext replacement" })).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var edited = (await b.Http.GetFromJsonAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{thread}/messages"))!.Items.Single();
        Assert.That(edited.EncryptedEnvelope, Is.EqualTo(edit.EncryptedEnvelope));
        Assert.That(edited.Text, Is.EqualTo("Encrypted message"));
        Assert.That(edited.EncryptionSenderDeviceId, Is.EqualTo(newDevice));
        Assert.That(edited.AcceptedSenderDirectoryRevision, Is.EqualTo(2));
        Assert.That((await a.Http.PostAsJsonAsync("api/chat/messages", message)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict),
            "Replaying pre-edit ciphertext cannot overwrite the message's accepted edit.");
        bEvents.Dispose(); cEvents.Dispose(); timeout.Cancel();
        await app.StopAsync();
    }

    private static Task<HttpResponseMessage> EventsAsync(Account account, CancellationToken ct) => account.Http.GetAsync(
        $"api/chat/events?account={account.User.TenantId:N}:{account.User.CredentialId:N}", HttpCompletionOption.ResponseHeadersRead, ct);

    private static async Task<T> PostAsync<T>(HttpClient http, string path, object body)
    {
        using var result = await http.PostAsJsonAsync(path, body);
        Assert.That(result.IsSuccessStatusCode, Is.True, await result.Content.ReadAsStringAsync());
        return (await result.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<Account> LoginAsync(Uri origin, string name, X509Certificate2 certificate)
    {
        var http = new HttpClient(new HttpClientHandler { CookieContainer = new(),
            ServerCertificateCustomValidationCallback = (_, received, _, errors) => received is not null
                && (errors & (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable)) == 0
                && received.RawData.AsSpan().SequenceEqual(certificate.RawData) }) { BaseAddress = origin };
        var session = (await http.GetFromJsonAsync<SessionResponse>("api/session"))!;
        http.DefaultRequestHeaders.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
        http.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        (await http.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["username"] = name, ["password"] = Environment.GetEnvironmentVariable("YAP_FIXTURE_PASSWORD") ?? "fixture" }))).EnsureSuccessStatusCode();
        session = (await http.GetFromJsonAsync<SessionResponse>("api/session"))!;
        Assert.That(session.EncryptionRequired, Is.True);
        http.DefaultRequestHeaders.Remove("RequestVerificationToken");
        http.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        http.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        return new(http, session.User);
    }

    private sealed record Account(HttpClient Http, UserSession User) : IDisposable { public void Dispose() => Http.Dispose(); }

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }
}
