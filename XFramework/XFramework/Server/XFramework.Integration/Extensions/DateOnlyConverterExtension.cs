using System.Text.Json;
using XFramework.Integration.Converters;

namespace XFramework.Integration.Extensions;

public static class DateOnlyConverterExtension
{
    public static void AddDateOnlyConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new DateOnlyConverter());
        options.Converters.Add(new DateOnlyNullableConverter());
    }
}