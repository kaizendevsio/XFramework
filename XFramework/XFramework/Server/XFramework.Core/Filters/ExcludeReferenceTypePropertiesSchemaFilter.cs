using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace XFramework.Core.Filters;

public class ExcludeReferenceTypePropertiesSchemaFilter : ISchemaFilter
{
    private const int MaxDepth = 5;

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema == null || context.Type == null)
            return;

        ProcessProperties(schema, context, depth: 1);
    }

    private void ProcessProperties(OpenApiSchema schema, SchemaFilterContext context, int depth)
    {
        // If it's a reference type, then follow the reference
        if (schema.Reference != null)
        {
            if (context.SchemaRepository.Schemas.TryGetValue(schema.Reference.Id, out var refSchema))
            {
                ProcessProperties(refSchema, context, depth);
            }
            return;
        }

        // If it's an array, then check the items of the array
        if (schema.Type == "array" && schema.Items != null)
        {
            ProcessProperties(schema.Items, context, depth + 1);
        }

        // Truncate properties if they exceed the maximum depth
        if (depth > MaxDepth)
        {
            schema.Properties?.Clear();
            return;
        }

        // Check each property and recursively evaluate depth
        if (schema.Properties != null)
        {
            foreach (var property in schema.Properties.ToList())
            {
                ProcessProperties(property.Value, context, depth + 1);
            }
        }
    }
}