namespace Wallets.Domain.Shared.Contracts.Requests;

/// <summary>
/// Represents a single wallet transfer operation within a batch
/// </summary>
[MemoryPackable]
public partial record BatchTransferRequest
{
    /// <summary>
    /// The ID of the source wallet (wallet to deduct from)
    /// </summary>
    public Guid FromWalletId { get; set; }
    
    /// <summary>
    /// The ID of the destination wallet (wallet to credit to)
    /// </summary>
    public Guid ToWalletId { get; set; }
    
    /// <summary>
    /// The credential ID of the source wallet owner
    /// </summary>
    public Guid FromCredentialId { get; set; }
    
    /// <summary>
    /// The credential ID of the destination wallet owner
    /// </summary>
    public Guid ToCredentialId { get; set; }
    
    /// <summary>
    /// The amount to transfer
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Transaction fee for this operation
    /// </summary>
    public decimal Fee { get; set; }
    
    /// <summary>
    /// Remarks for this transaction
    /// </summary>
    public string? Remarks { get; set; }
    
    /// <summary>
    /// Reference number for tracking this transaction
    /// </summary>
    public string? ReferenceNumber { get; set; }
}