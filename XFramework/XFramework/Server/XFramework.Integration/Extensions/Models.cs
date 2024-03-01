namespace XFramework.Integration.Extensions;

public static class Models
{
    public static IEnumerable<(string Name, object? Value)> GetIdPropertyNames<T>(this T model)
        where T : class
    {
        return model.GetType()
            .GetProperties()
            .Where(prop => prop.Name.EndsWith("Id", StringComparison.Ordinal))
            .Select(prop => (prop.Name, prop.GetValue(model)));
    }

}