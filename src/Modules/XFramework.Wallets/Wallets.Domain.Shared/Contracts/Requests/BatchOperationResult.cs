namespace Wallets.Domain.Shared.Contracts.Requests;

/// <summary>
/// Represents the result of a batch wallet operation
/// </summary>
[MemoryPackable]
public partial record BatchOperationResult
{
    /// <summary>
    /// Total number of items processed (attempted)
    /// </summary>
    public int TotalProcessed { get; set; }
    
    /// <summary>
    /// Number of items that succeeded
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of items that failed
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// List of errors that occurred during batch processing
    /// </summary>
    public List<BatchOperationError> Errors { get; set; } = new();
    
    /// <summary>
    /// Time taken to process the batch
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Whether all operations in the batch succeeded
    /// </summary>
    [MemoryPackIgnore]
    public bool AllSucceeded => FailureCount == 0;
    
    /// <summary>
    /// Whether any operations in the batch succeeded
    /// </summary>
    [MemoryPackIgnore]
    public bool AnySucceeded => SuccessCount > 0;
    
    /// <summary>
    /// Percentage of operations that succeeded
    /// </summary>
    [MemoryPackIgnore]
    public decimal SuccessRate => TotalProcessed > 0 
        ? (decimal)SuccessCount / TotalProcessed * 100 
        : 0;
}