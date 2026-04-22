namespace XFramework.Domain.Shared.DataContext;

public interface IEntityChangeTracker<T> where T : class
{
    object Snapshot(T entity);
    FieldPatch? Diff(T current, object snapshot);
    Guid GetPrimaryKey(T entity);
}
