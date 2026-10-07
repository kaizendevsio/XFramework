using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using POS.Api.Features.Scanner.Claim;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace POS.Api.Services;

// Transient coordination, not a cache: never restore a pairing after a restart or lease expiry.
public sealed class PosScannerPairingStore(TimeProvider clock)
{
    internal const int QueueLimit = 32;
    internal const int ScanLimit = 1024;
    internal const int ShortClaimLimit = 5;
    internal const int GlobalShortClaimLimit = 500;
    internal static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan PairingTtl = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan DesktopLease = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Pairing> _pairings = [];
    private readonly Dictionary<string, DateTimeOffset> _reservedCodes = [];
    private readonly Dictionary<Guid, ClaimBudget> _claimBudgets = [];
    private DateTimeOffset _globalClaimWindowEnd;
    private int _globalClaims;
    private readonly Func<string> _newCode = static () =>
        RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    internal PosScannerPairingStore(TimeProvider clock, Func<string> newCode) : this(clock) => _newCode = newCode;

    internal Result<PosScannerPairingResponse> Create(ScannerActor actor, Guid registerId, string registerName)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            Prune(now);
            if (_pairings.Count >= 4096 || _reservedCodes.Count >= 4096 ||
                _pairings.Values.Count(p => p.TenantId == actor.TenantId && p.ActorId == actor.CredentialId) >= 8)
                return Result<PosScannerPairingResponse>.Failure("Too many active scanner pairings", 429);

            string? code = null;
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var candidate = _newCode();
                if (ClaimPosScannerPairingValidator.IsPairingCode(candidate) && !_reservedCodes.ContainsKey(candidate))
                { code = candidate; break; }
            }
            if (code is null)
                return Result<PosScannerPairingResponse>.Failure("Pairing codes are busy. Try again shortly.", 429);

            var pairing = new Pairing
            {
                Id = Guid.NewGuid(), TenantId = actor.TenantId, ActorId = actor.CredentialId,
                DesktopSessionId = actor.SessionId, RegisterId = registerId, RegisterName = registerName,
                DesktopKey = NewKey(), Challenge = NewKey(), PairingCode = code,
                ChallengeExpiresAt = now + ChallengeTtl, ExpiresAt = now + PairingTtl,
                LeaseExpiresAt = now + DesktopLease
            };
            _pairings.Add(pairing.Id, pairing);
            // Keep consumed/revoked codes reserved through hard expiry so stale codes cannot claim replacements.
            _reservedCodes.Add(code, pairing.ExpiresAt);
            return Result<PosScannerPairingResponse>.Success(new(pairing.Id, pairing.DesktopKey,
                pairing.Challenge, pairing.ChallengeExpiresAt, code));
        }
    }

    internal Result<PosScannerPhoneResponse> Claim(ScannerActor actor, string challenge)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            Prune(now);
            var shortCode = ClaimPosScannerPairingValidator.IsPairingCode(challenge);
            if (!shortCode && !ClaimPosScannerPairingValidator.IsChallenge(challenge))
                return Result<PosScannerPhoneResponse>.Failure("Enter exactly six digits or use a valid pairing QR.", 400);
            if (shortCode && !AllowShortClaim(actor.CredentialId, now))
                return Result<PosScannerPhoneResponse>.Failure("Too many pairing attempts. Try again after two minutes.", 429);
            var pairing = _pairings.Values.FirstOrDefault(p =>
                p.TenantId == actor.TenantId && p.ActorId == actor.CredentialId &&
                !p.PhoneSessionId.HasValue && p.ChallengeExpiresAt > now &&
                (shortCode ? Matches(p.PairingCode, challenge, 6) : Matches(p.Challenge, challenge)));
            if (pairing is null)
                return Result<PosScannerPhoneResponse>.Forbidden("Pairing is unavailable; request a new QR on the desktop");

            // A separate login session is mandatory, even for another tab owned by the cashier.
            if (actor.SessionId == pairing.DesktopSessionId)
                return Result<PosScannerPhoneResponse>.Forbidden("Sign in independently on the phone");
            pairing.PhoneSessionId = actor.SessionId;
            pairing.PhoneKey = NewKey();
            pairing.Challenge = "";
            pairing.PairingCode = "";
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
        foreach (var code in _reservedCodes.Where(p => p.Value <= now).Select(p => p.Key).ToArray())
            _reservedCodes.Remove(code);
        foreach (var actor in _claimBudgets.Where(p => p.Value.ExpiresAt <= now).Select(p => p.Key).ToArray())
            _claimBudgets.Remove(actor);
    }

    private bool AllowShortClaim(Guid actor, DateTimeOffset now)
    {
        if (_globalClaimWindowEnd <= now)
        { _globalClaimWindowEnd = now + ChallengeTtl; _globalClaims = 0; }
        if (!_claimBudgets.TryGetValue(actor, out var budget))
        {
            if (_claimBudgets.Count >= 4096) return false;
            budget = new(now + ChallengeTtl);
            _claimBudgets.Add(actor, budget);
        }
        if (budget.Attempts >= ShortClaimLimit || _globalClaims >= GlobalShortClaimLimit) return false;
        budget.Attempts++;
        _globalClaims++;
        return true;
    }

    private static bool Owns(Pairing pairing, ScannerActor actor) =>
        pairing.TenantId == actor.TenantId && pairing.ActorId == actor.CredentialId;
    private static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static bool Matches(string expected, string? supplied, int length = 64) =>
        expected.Length == length && supplied?.Length == length &&
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
        public required string PairingCode { get; set; }
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

    private sealed class ClaimBudget(DateTimeOffset expiresAt)
    {
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public int Attempts { get; set; }
    }
}

internal sealed record ScannerActor(Guid TenantId, Guid CredentialId, Guid SessionId);
