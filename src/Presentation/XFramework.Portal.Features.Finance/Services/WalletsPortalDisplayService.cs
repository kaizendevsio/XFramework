using Microsoft.EntityFrameworkCore;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Portal.Features.Finance.Services;

public sealed class WalletsPortalDisplayService(IDataContext dataContext)
{
    public async Task<IReadOnlyDictionary<Guid, string>> LoadWalletLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> walletIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(walletIds);
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var wallets = await dataContext.Query<Wallet>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.WalletType)
            .Include(x => x.Credential)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return wallets.ToDictionary(x => x.Id, BuildWalletLabel);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> LoadCredentialLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> credentialIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(credentialIds);
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var credentials = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.IdentityInfo)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return credentials.ToDictionary(x => x.Id, BuildCredentialLabel);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> LoadOperationLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> operationIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(operationIds);
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var operations = await dataContext.Query<WalletOperation>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return operations.ToDictionary(x => x.Id, BuildOperationLabel);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> LoadTransactionLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> transactionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(transactionIds);
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var transactions = await dataContext.Query<WalletTransaction>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return transactions.ToDictionary(x => x.Id, BuildTransactionLabel);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> LoadTransferLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> transferIds,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(transferIds);
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var transfers = await dataContext.Query<WalletTransfer>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return transfers.ToDictionary(x => x.Id, BuildTransferLabel);
    }

    public string WalletLabel(Guid? id, IReadOnlyDictionary<Guid, string> labels) =>
        LabelOrFallback(id, labels, "Wallet");

    public string CredentialLabel(Guid? id, IReadOnlyDictionary<Guid, string> labels) =>
        LabelOrFallback(id, labels, "Credential");

    public string OperationLabel(Guid? id, IReadOnlyDictionary<Guid, string> labels) =>
        LabelOrFallback(id, labels, "Operation");

    public string TransactionLabel(Guid? id, IReadOnlyDictionary<Guid, string> labels) =>
        LabelOrFallback(id, labels, "Transaction");

    public string TransferLabel(Guid? id, IReadOnlyDictionary<Guid, string> labels) =>
        LabelOrFallback(id, labels, "Transfer");

    public static string ShortId(Guid? id) => id is Guid value ? value.ToString("N")[..8] : "N/A";

    public static string SafeFailureSummary(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "No failure recorded" : "Failure recorded";

    public static string SafeActionFailure(string noun) => $"{noun} could not be completed.";

    public static string BuildWalletLabel(Wallet wallet)
    {
        var account = string.IsNullOrWhiteSpace(wallet.AccountNumber)
            ? $"Wallet {ShortId(wallet.Id)}"
            : wallet.AccountNumber;
        var type = wallet.WalletType?.Code ?? wallet.WalletType?.Name ?? "No type";
        var status = wallet.Status.ToString();
        var credential = wallet.Credential is null ? null : BuildCredentialLabel(wallet.Credential);

        return string.IsNullOrWhiteSpace(credential)
            ? $"{account} - {type} - {status}"
            : $"{account} - {credential} - {type} - {status}";
    }

    public static string BuildCredentialLabel(IdentityCredential credential)
    {
        var displayName = credential.IdentityInfo?.FullName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = credential.IdentityInfo?.IdentityName;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = credential.UserName ?? credential.UserAlias ?? "Unnamed user";
        }

        var login = credential.UserName ?? credential.UserAlias;
        return string.IsNullOrWhiteSpace(login)
            ? $"{displayName} ({ShortId(credential.Id)})"
            : $"{displayName} ({login})";
    }

    public static string BuildOperationLabel(WalletOperation operation)
    {
        var reference = string.IsNullOrWhiteSpace(operation.ReferenceNumber)
            ? $"Operation {ShortId(operation.Id)}"
            : operation.ReferenceNumber;
        return $"{reference} - {operation.OperationType} - {operation.Status}";
    }

    public static string BuildTransactionLabel(WalletTransaction transaction)
    {
        var reference = string.IsNullOrWhiteSpace(transaction.ReferenceNumber)
            ? $"Transaction {ShortId(transaction.Id)}"
            : transaction.ReferenceNumber;
        return $"{reference} - {transaction.TransactionType?.ToString() ?? "Unknown"} - {transaction.Amount:N2}";
    }

    public static string BuildTransferLabel(WalletTransfer transfer) =>
        $"Transfer {ShortId(transfer.Id)} - {transfer.TransactionPurpose} - fee {transfer.TransactionFee:N2}";

    private static string LabelOrFallback(Guid? id, IReadOnlyDictionary<Guid, string> labels, string noun)
    {
        if (id is not Guid value)
        {
            return "N/A";
        }

        return labels.TryGetValue(value, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : $"{noun} {ShortId(value)}";
    }

    private static Guid[] NormalizeIds(IEnumerable<Guid?> ids) =>
        ids.Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
}
