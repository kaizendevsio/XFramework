using System.Diagnostics.Metrics;

namespace XFramework.Core.Observability;

/// <summary>
/// Provides custom metrics for monitoring XFramework application performance and business operations.
/// Includes counters for tracking events and histograms for measuring durations.
/// </summary>
public static class XFrameworkMetrics
{
    /// <summary>
    /// Meter name for all XFramework custom metrics
    /// </summary>
    private const string MeterName = "XFramework";
    
    /// <summary>
    /// Version for the meter
    /// </summary>
    private const string Version = "1.0.0";
    
    /// <summary>
    /// The meter instance used to create all metrics
    /// </summary>
    private static readonly Meter Meter = new(MeterName, Version);
    
    #region Product/Inventory Metrics
    
    /// <summary>
    /// Counts the number of products created
    /// </summary>
    public static readonly Counter<long> ProductsCreated = 
        Meter.CreateCounter<long>(
            "xframework.products.created",
            unit: "count",
            description: "Total number of products created");
    
    /// <summary>
    /// Counts the number of products updated
    /// </summary>
    public static readonly Counter<long> ProductsUpdated = 
        Meter.CreateCounter<long>(
            "xframework.products.updated",
            unit: "count",
            description: "Total number of products updated");
    
    /// <summary>
    /// Measures the duration of product creation operations
    /// </summary>
    public static readonly Histogram<double> ProductCreationDuration = 
        Meter.CreateHistogram<double>(
            "xframework.products.creation.duration",
            unit: "ms",
            description: "Duration of product creation operations in milliseconds");
    
    #endregion
    
    #region Wallet Metrics
    
    /// <summary>
    /// Counts the total number of wallet transactions (increment, decrement, transfer)
    /// </summary>
    public static readonly Counter<long> WalletTransactions = 
        Meter.CreateCounter<long>(
            "xframework.wallet.transactions",
            unit: "count",
            description: "Total number of wallet transactions");
    
    /// <summary>
    /// Counts the number of wallet balance increments
    /// </summary>
    public static readonly Counter<long> WalletIncrements = 
        Meter.CreateCounter<long>(
            "xframework.wallet.increments",
            unit: "count",
            description: "Total number of wallet balance increments");
    
    /// <summary>
    /// Counts the number of wallet balance decrements
    /// </summary>
    public static readonly Counter<long> WalletDecrements = 
        Meter.CreateCounter<long>(
            "xframework.wallet.decrements",
            unit: "count",
            description: "Total number of wallet balance decrements");
    
    /// <summary>
    /// Measures the duration of wallet operations
    /// </summary>
    public static readonly Histogram<double> WalletOperationDuration = 
        Meter.CreateHistogram<double>(
            "xframework.wallet.operation.duration",
            unit: "ms",
            description: "Duration of wallet operations in milliseconds");
    
    /// <summary>
    /// Tracks the amount of currency processed in wallet transactions
    /// </summary>
    public static readonly Histogram<decimal> WalletTransactionAmount = 
        Meter.CreateHistogram<decimal>(
            "xframework.wallet.transaction.amount",
            unit: "currency",
            description: "Amount of currency in wallet transactions");
    
    #endregion
    
    #region Authentication Metrics
    
    /// <summary>
    /// Counts the total number of authentication attempts
    /// </summary>
    public static readonly Counter<long> AuthenticationAttempts = 
        Meter.CreateCounter<long>(
            "xframework.auth.attempts",
            unit: "count",
            description: "Total number of authentication attempts");
    
    /// <summary>
    /// Counts the number of successful authentications
    /// </summary>
    public static readonly Counter<long> AuthenticationSuccesses = 
        Meter.CreateCounter<long>(
            "xframework.auth.successes",
            unit: "count",
            description: "Total number of successful authentications");
    
    /// <summary>
    /// Counts the number of failed authentication attempts
    /// </summary>
    public static readonly Counter<long> AuthenticationFailures = 
        Meter.CreateCounter<long>(
            "xframework.auth.failures",
            unit: "count",
            description: "Total number of failed authentication attempts");
    
    /// <summary>
    /// Measures the duration of authentication operations
    /// </summary>
    public static readonly Histogram<double> AuthenticationDuration = 
        Meter.CreateHistogram<double>(
            "xframework.auth.duration",
            unit: "ms",
            description: "Duration of authentication operations in milliseconds");
    
    #endregion
    
    #region Communications Metrics
    
    /// <summary>
    /// Counts the number of messages sent
    /// </summary>
    public static readonly Counter<long> MessagesSent = 
        Meter.CreateCounter<long>(
            "xframework.messages.sent",
            unit: "count",
            description: "Total number of messages sent");
    
    /// <summary>
    /// Counts the number of SMS messages sent
    /// </summary>
    public static readonly Counter<long> SmsSent = 
        Meter.CreateCounter<long>(
            "xframework.sms.sent",
            unit: "count",
            description: "Total number of SMS messages sent");
    
    /// <summary>
    /// Measures the duration of message send operations
    /// </summary>
    public static readonly Histogram<double> MessageSendDuration = 
        Meter.CreateHistogram<double>(
            "xframework.messages.send.duration",
            unit: "ms",
            description: "Duration of message send operations in milliseconds");
    
    #endregion
    
    #region Cache Metrics
    
    /// <summary>
    /// Counts the number of cache hits
    /// </summary>
    public static readonly Counter<long> CacheHits = 
        Meter.CreateCounter<long>(
            "xframework.cache.hits",
            unit: "count",
            description: "Total number of cache hits");
    
    /// <summary>
    /// Counts the number of cache misses
    /// </summary>
    public static readonly Counter<long> CacheMisses = 
        Meter.CreateCounter<long>(
            "xframework.cache.misses",
            unit: "count",
            description: "Total number of cache misses");
    
    /// <summary>
    /// Measures the duration of cache operations
    /// </summary>
    public static readonly Histogram<double> CacheOperationDuration = 
        Meter.CreateHistogram<double>(
            "xframework.cache.operation.duration",
            unit: "ms",
            description: "Duration of cache operations in milliseconds");
    
    #endregion
    
    #region Database Metrics
    
    /// <summary>
    /// Counts the total number of database queries executed
    /// </summary>
    public static readonly Counter<long> DatabaseQueries = 
        Meter.CreateCounter<long>(
            "xframework.database.queries",
            unit: "count",
            description: "Total number of database queries executed");
    
    /// <summary>
    /// Measures the duration of database operations
    /// </summary>
    public static readonly Histogram<double> DatabaseOperationDuration = 
        Meter.CreateHistogram<double>(
            "xframework.database.operation.duration",
            unit: "ms",
            description: "Duration of database operations in milliseconds");
    
    #endregion
    
    #region API Metrics
    
    /// <summary>
    /// Counts the number of API errors (4xx and 5xx responses)
    /// </summary>
    public static readonly Counter<long> ApiErrors = 
        Meter.CreateCounter<long>(
            "xframework.api.errors",
            unit: "count",
            description: "Total number of API errors");
    
    /// <summary>
    /// Measures the size of API request payloads
    /// </summary>
    public static readonly Histogram<long> ApiRequestSize = 
        Meter.CreateHistogram<long>(
            "xframework.api.request.size",
            unit: "bytes",
            description: "Size of API request payloads in bytes");
    
    /// <summary>
    /// Measures the size of API response payloads
    /// </summary>
    public static readonly Histogram<long> ApiResponseSize = 
        Meter.CreateHistogram<long>(
            "xframework.api.response.size",
            unit: "bytes",
            description: "Size of API response payloads in bytes");
    
    #endregion
}