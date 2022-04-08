using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace XFramework.Api.Options;

public class ODataOperationOptions : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        var descriptor = context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;
        if (descriptor == null || !descriptor.FilterDescriptors.Any(filter => filter.Filter is EnableQueryAttribute)) return;
        
        operation.Parameters.Add(new OpenApiParameter()
        {
            Name = "$select",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
            Description = "Returns only the selected properties. (ex. FirstName, LastName, City)",
            Required = false
        });

        operation.Parameters.Add(new OpenApiParameter()
        {
            Name = "$expand",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
            Description = "Include only the selected objects. (ex. Childrens, Locations)",
            Required = false
        });

        operation.Parameters.Add(new OpenApiParameter()
        {
            Name = "$filter",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
            Description = "Filter the response with OData filter queries.",
            Required = false
        });

        operation.Parameters.Add(new OpenApiParameter()
        {
            Name = "$top",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
            Description = "Number of objects to return. (ex. 25)",
            Required = false
        });

        operation.Parameters.Add(new OpenApiParameter()
        {
            Name = "$skip",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
            Description = "Number of objects to skip in the current order (ex. 50)",
            Required = false
        });

        operation.Parameters.Add(new OpenApiParameter()
        {
            Name = "$orderby",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema
            {
                Type = "string",
            },
            Description = "Define the order by one or more fields (ex. LastModified)",
            Required = false
        });
    }
}