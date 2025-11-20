namespace Wallets.Domain.Shared.Contracts.Requests;

/// <summary>
/// Represents an error that occurred during batch processing for a specific item
/// </summary>
[MemoryPackable]
public partial record BatchOperationError
{
    /// <summary>
    /// The index of the item in the batch that failed
    /// </summary>
    public int ItemIndex { get; set; }
    
    /// <summary>
    /// The ID of the wallet that failed (if applicable)
    /// </summary>
    public Guid? WalletId { get; set; }
    
    /// <summary>
    /// The error message describing what went wrong
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// The error code (if applicable)
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Reference number of the failed operation (if provided)
    /// </summary>
    public string? ReferenceNumber { get; set; }
}