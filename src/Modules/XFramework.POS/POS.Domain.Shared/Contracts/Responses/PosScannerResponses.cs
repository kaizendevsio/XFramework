using MemoryPack;

namespace POS.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PosScannerPairingResponse(
    Guid PairingId, string DesktopKey, string Challenge, DateTimeOffset ChallengeExpiresAt);

[MemoryPackable]
public partial record PosScannerPhoneResponse(
    Guid PairingId, string PhoneKey, string RegisterName, DateTimeOffset ExpiresAt);

[MemoryPackable]
public partial record PosScannerCodeResponse(long Sequence, string Code);

[MemoryPackable]
public partial record PosScannerSendResponse(long Sequence, bool Duplicate);

[MemoryPackable]
public partial record PosScannerPollResponse(bool IsPaired, DateTimeOffset ExpiresAt,
    List<PosScannerCodeResponse> Codes);
