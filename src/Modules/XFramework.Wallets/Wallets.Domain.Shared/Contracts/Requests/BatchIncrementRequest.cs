namespace Wallets.Domain.Shared.Contracts.Requests;

/// <summary>
/// Represents a single wallet increment operation within a batch
/// </summary>
[MemoryPackable]
public partial record BatchIncrementRequest
{
    /// <summary>
    /// The ID of the wallet to increment
    /// </summary>
    public Guid WalletId { get; set; }
    
    /// <summary>
    /// The wallet type ID (used if wallet doesn't exist and should be created)
    /// </summary>
    public Guid WalletTypeId { get; set; }
    
    /// <summary>
    /// The credential ID associated with the wallet
    /// </summary>
    public Guid CredentialId { get; set; }
    
    /// <summary>
    /// The amount to increment
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
    /// Whether this transaction should be held (not immediately available)
    /// </summary>
    public bool OnHold { get; set; }
    
    /// <summary>
    /// Reference number for tracking this transaction
    /// </summary>
    public string? ReferenceNumber { get; set; }
}