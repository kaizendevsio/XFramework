using System.Diagnostics;

namespace XFramework.Core.Observability;

/// <summary>
/// Provides domain-specific ActivitySource instances for distributed tracing across XFramework modules.
/// Each ActivitySource represents a distinct domain or service boundary.
/// </summary>
public static class ActivitySources
{
    /// <summary>
    /// Base service name for all XFramework activities
    /// </summary>
    public const string ServiceName = "XFramework";
    
    /// <summary>
    /// Version for all activity sources
    /// </summary>
    public const string Version = "1.0.0";
    
    // Domain-specific activity sources
    
    /// <summary>
    /// ActivitySource for product/inventory operations
    /// </summary>
    public static readonly ActivitySource Product = new($"{ServiceName}.Product", Version);
    
    /// <summary>
    /// ActivitySource for wallet/balance operations
    /// </summary>
    public static readonly ActivitySource Wallet = new($"{ServiceName}.Wallet", Version);
    
    /// <summary>
    /// ActivitySource for authentication and authorization operations
    /// </summary>
    public static readonly ActivitySource Auth = new($"{ServiceName}.Auth", Version);
    
    /// <summary>
    /// ActivitySource for Bolt messaging operations
    /// </summary>
    public static readonly ActivitySource Bolt = new($"{ServiceName}.Bolt", Version);
    
    /// <summary>
    /// ActivitySource for SMS gateway operations
    /// </summary>
    public static readonly ActivitySource Sms = new($"{ServiceName}.Sms", Version);
    
    /// <summary>
    /// ActivitySource for messaging operations
    /// </summary>
    public static readonly ActivitySource Messaging = new($"{ServiceName}.Messaging", Version);
    
    /// <summary>
    /// ActivitySource for community/social operations
    /// </summary>
    public static readonly ActivitySource Community = new($"{ServiceName}.Community", Version);
    
    /// <summary>
    /// ActivitySource for blockchain/crypto operations
    /// </summary>
    public static readonly ActivitySource Blockchain = new($"{ServiceName}.Blockchain", Version);
    
    /// <summary>
    /// ActivitySource for payment gateway operations
    /// </summary>
    public static readonly ActivitySource Payment = new($"{ServiceName}.Payment", Version);
    
    /// <summary>
    /// ActivitySource for core infrastructure operations (caching, database, etc.)
    /// </summary>
    public static readonly ActivitySource Infrastructure = new($"{ServiceName}.Infrastructure", Version);
}