namespace XFramework.Core.DataContext;

public interface IRemoteDataContextEntityValidator
{
    Type EntityType { get; }

    Task<IReadOnlyList<string>> ValidateAsync(
        object entity,
        CancellationToken ct = default);
}
