using System.Security.Cryptography;
using System.Text;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace POS.Api.Services;

// Transient coordination, not a cache: never restore a pairing after a restart or lease expiry.
public sealed class PosScannerPairingStore(TimeProvider clock)
{
    internal const int QueueLimit = 32;
    internal const int ScanLimit = 1024;
    internal static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan PairingTtl = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan DesktopLease = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Pairing> _pairings = [];

    internal Result<PosScannerPairingResponse> Create(ScannerActor actor, Guid registerId, string registerName)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            Prune(now);
            if (_pairings.Count >= 4096 ||
                _pairings.Values.Count(p => p.TenantId == actor.TenantId && p.ActorId == actor.CredentialId) >= 8)
                return Result<PosScannerPairingResponse>.Failure("Too many active scanner pairings", 429);

            var pairing = new Pairing
            {
                Id = Guid.NewGuid(), TenantId = actor.TenantId, ActorId = actor.CredentialId,
                DesktopSessionId = actor.SessionId, RegisterId = registerId, RegisterName = registerName,
                DesktopKey = NewKey(), Challenge = NewKey(),
                ChallengeExpiresAt = now + ChallengeTtl, ExpiresAt = now + PairingTtl,
                LeaseExpiresAt = now + DesktopLease
            };
            _pairings.Add(pairing.Id, pairing);
            return Result<PosScannerPairingResponse>.Success(new(pairing.Id, pairing.DesktopKey,
                pairing.Challenge, pairing.ChallengeExpiresAt));
        }
    }

    internal Result<PosScannerPhoneResponse> Claim(ScannerActor actor, string challenge)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            Prune(now);
            var pairing = _pairings.Values.FirstOrDefault(p =>
                p.TenantId == actor.TenantId && p.ActorId == actor.CredentialId &&
                !p.PhoneSessionId.HasValue && p.ChallengeExpiresAt > now &&
                Matches(p.Challenge, challenge));
            if (pairing is null)
                return Result<PosScannerPhoneResponse>.Forbidden("Pairing is unavailable; request a new QR on the desktop");

            // A separate login session is mandatory, even for another tab owned by the cashier.
            if (actor.SessionId == pairing.DesktopSessionId)
                return Result<PosScannerPhoneResponse>.Forbidden("Sign in independently on the phone");
            pairing.PhoneSessionId = actor.SessionId;
            pairing.PhoneKey = NewKey();
            pairing.Challenge = "";
            return Result<PosScannerPhoneResponse>.Success(new(pairing.Id, pairing.PhoneKey,
                pairing.RegisterName, pairing.ExpiresAt));
        }
    }

    internal Result<PosScannerSendResponse> Send(ScannerActor actor, Guid id, string key, long sequence, string code)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            Prune(now);
            if (!_pairings.TryGetValue(id, out var pairing) || !Owns(pairing, actor) ||
                pairing.PhoneSessionId != actor.SessionId || !Matches(pairing.PhoneKey, key))
                return Result<PosScannerSendResponse>.Forbidden("Scanner pairing ended; pair again");
            if (pairing.Paused)
                return Result<PosScannerSendResponse>.Failure("Desktop is busy; retry when payment closes", 423);
            if (string.IsNullOrWhiteSpace(code) || code.Length > 256 || code.Any(char.IsControl))
                return Result<PosScannerSendResponse>.Failure("Product code must be 1-256 printable characters", 400);
            if (sequence == pairing.LastSequence && code == pairing.LastCode)
                return Result<PosScannerSendResponse>.Success(new(sequence, true));
            if (sequence != pairing.LastSequence + 1 || sequence > ScanLimit)
                return Result<PosScannerSendResponse>.Conflict("Scan sequence is stale or exhausted; pair again");
            if (pairing.Codes.Count >= QueueLimit)
                return Result<PosScannerSendResponse>.Failure("Desktop scan queue is full; wait for the cashier", 429);

            pairing.LastSequence = sequence;
            pairing.LastCode = code;
            pairing.Codes.Add(new(sequence, code));
            return Result<PosScannerSendResponse>.Success(new(sequence, false));
        }
    }

    internal Result<PosScannerPollResponse> Poll(ScannerActor actor, Guid id, string key, long acknowledged, bool pauseDelivery = false)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            Prune(now);
            if (!_pairings.TryGetValue(id, out var pairing) || !Owns(pairing, actor) ||
                pairing.DesktopSessionId != actor.SessionId || !Matches(pairing.DesktopKey, key))
                return Result<PosScannerPollResponse>.Forbidden("Scanner pairing ended; create a new pairing");
            pairing.Paused = pauseDelivery;
            pairing.LeaseExpiresAt = now + DesktopLease;
            if (pauseDelivery)
                return Result<PosScannerPollResponse>.Success(new(pairing.PhoneSessionId.HasValue, pairing.ExpiresAt, []));
            if (acknowledged < pairing.Acknowledged || acknowledged > pairing.LastDelivered)
                return Result<PosScannerPollResponse>.Conflict("Invalid scan acknowledgement");
            pairing.Acknowledged = acknowledged;
            pairing.Codes.RemoveAll(item => item.Sequence <= acknowledged);
            pairing.LeaseExpiresAt = now + DesktopLease;
            var codes = pairing.Codes.ToList();
            if (codes.Count > 0)
                pairing.LastDelivered = codes[^1].Sequence;
            return Result<PosScannerPollResponse>.Success(new(pairing.PhoneSessionId.HasValue,
                pairing.ExpiresAt, codes));
        }
    }

    internal Result<bool> Revoke(ScannerActor actor, Guid id, string key)
    {
        lock (_gate)
        {
            Prune(clock.GetUtcNow());
            if (!_pairings.TryGetValue(id, out var pairing))
                return Result<bool>.Success(true);
            if (!Owns(pairing, actor) || pairing.DesktopSessionId != actor.SessionId ||
                !Matches(pairing.DesktopKey, key))
                return Result<bool>.Forbidden("Only the paired desktop can disconnect this scanner");
            _pairings.Remove(id);
            return Result<bool>.Success(true);
        }
    }

    internal Result<PosScannerPhoneResponse> Status(ScannerActor actor, Guid id, string key)
    {
        lock (_gate)
        {
            Prune(clock.GetUtcNow());
            if (!_pairings.TryGetValue(id, out var pairing) || !Owns(pairing, actor) ||
                pairing.PhoneSessionId != actor.SessionId || !Matches(pairing.PhoneKey, key))
                return Result<PosScannerPhoneResponse>.Forbidden("Scanner pairing ended; pair again");
            return Result<PosScannerPhoneResponse>.Success(new(pairing.Id, pairing.PhoneKey,
                pairing.RegisterName, pairing.ExpiresAt));
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var id in _pairings.Values.Where(p => p.ExpiresAt <= now || p.LeaseExpiresAt <= now ||
                     (!p.PhoneSessionId.HasValue && p.ChallengeExpiresAt <= now)).Select(p => p.Id).ToArray())
            _pairings.Remove(id);
    }

    private static bool Owns(Pairing pairing, ScannerActor actor) =>
        pairing.TenantId == actor.TenantId && pairing.ActorId == actor.CredentialId;
    private static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static bool Matches(string expected, string? supplied) =>
        expected.Length == 64 && supplied?.Length == 64 &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied));

    private sealed class Pairing
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid ActorId { get; init; }
        public Guid DesktopSessionId { get; init; }
        public Guid RegisterId { get; init; }
        public required string RegisterName { get; init; }
        public required string DesktopKey { get; init; }
        public required string Challenge { get; set; }
        public Guid? PhoneSessionId { get; set; }
        public string PhoneKey { get; set; } = "";
        public DateTimeOffset ChallengeExpiresAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset LeaseExpiresAt { get; set; }
        public long LastSequence { get; set; }
        public string LastCode { get; set; } = "";
        public long Acknowledged { get; set; }
        public long LastDelivered { get; set; }
        public bool Paused { get; set; }
        public List<PosScannerCodeResponse> Codes { get; } = [];
    }
}

internal sealed record ScannerActor(Guid TenantId, Guid CredentialId, Guid SessionId);
