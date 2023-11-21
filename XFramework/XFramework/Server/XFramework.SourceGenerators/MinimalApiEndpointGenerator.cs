using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;


[Generator]
public class MinimalApiEndpointGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var serviceName = context.Compilation.AssemblyName?.Split(".").First();
        var models = BaseSourceGenerator.GetModels(context, "GenerateApiFromNamespace");
        var codeBuilder = new StringBuilder();

        if (models.Count == 0)
        {
            return;
        }
        
        codeBuilder.AppendLine($@"
        using Microsoft.AspNetCore.Builder;
        using XFramework.Core.DataAccess.Commands;
        using XFramework.Core.DataAccess.Query;
        using XFramework.Domain.Generic.Contracts;
        using XFramework.Domain.Generic.BusinessObjects;
        using XFramework.Domain.Generic.Contracts.Requests;
        using XFramework.Integration.Extensions;
        using System.Text.Json;
        using System;
        using MediatR;
        using System.Linq.Expressions;
        using Asp.Versioning;
        using Asp.Versioning.Conventions;

        namespace {serviceName}.Api.Generators
        {{
            public static partial class MinimalApiGenerator
            {{
                public static WebApplication GenerateMinimalApi(this WebApplication app)
                {{
                    ");

        codeBuilder.AppendLine(@"
                    var versionSet = app.NewApiVersionSet()
                    .HasApiVersion(3.0)
                    .ReportApiVersions()
                    .Build();");
        
        codeBuilder.AppendLine("");
        
        foreach (var model in models)
        {
            GenerateHandler(codeBuilder, model);
        }
        codeBuilder.AppendLine(@"
                return app;
                }
            }
        }");

        context.AddSource("MinimalApiEndpointGenerator.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }

    private static void GenerateHandler(StringBuilder codeBuilder, string model)
    {
        codeBuilder.Append($"                    // API Handler for {model}");
        codeBuilder.AppendLine("");
        codeBuilder.Append("                    ");
        codeBuilder.AppendLine("");
            
        // GetList
        codeBuilder.Append("                    ");
        codeBuilder.AppendLine($@"var {model.ToLower()}Model = app.MapGroup(""/{model}"")
                        .WithApiVersionSet(versionSet)
                        //.RequireAuthorization()
                        .WithTags(""{model}"")  
                        .IsApiVersionNeutral();");

        codeBuilder.AppendLine("");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapGet(""/"", async ([FromServices] IMediator mediator, [FromQuery] int pageSize, [FromQuery] int pageNumber, [FromQuery] bool? includeNavigations, [FromQuery] string filter, [FromQuery] Guid tenantId) =>");
            
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var filters = JsonSerializer.Deserialize<List<QueryFilter>>(filter);
                            var request = new GetList<{model}>(pageSize, pageNumber, tenantId, includeNavigations, filters);
                            return await mediator.Send(request);
                        }});");

        // Get
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapGet(""/{{id}}"", async (IMediator mediator, Guid id, bool? includeNavigations, [FromQuery] Guid tenantId) =>");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var request = new Get<{model}>(id, tenantId, includeNavigations);
                            return await mediator.Send(request);
                        }});");

        // Create
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapPost(""/"", async (IMediator mediator, {model} model, [FromQuery] Guid tenantId) =>");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var request = new Create<{model}>(model);
                            return await mediator.Send(request);
                        }});");

        // Replace
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapPut(""/{{id}}"", async (IMediator mediator, Guid id, {model} model, [FromQuery] Guid tenantId) =>");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var replaceRequest = new Replace<{model}>(model);
                            return await mediator.Send(replaceRequest);
                        }});");

        // Patch
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapPatch(""/{{id}}"", async (IMediator mediator, Guid id, {model} model, [FromQuery] Guid tenantId) =>");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var patchRequest = new Patch<{model}>(model);
                            return await mediator.Send(patchRequest);
                        }});");

        // Delete
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapDelete(""/{{id}}"", async (IMediator mediator, Guid id, [FromQuery] Guid tenantId) =>");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var modelToDelete = new {model} {{ Id = id }};
                            var deleteRequest = new Delete<{model}>(modelToDelete);
                            return await mediator.Send(deleteRequest);
                        }});");
        codeBuilder.Append("                    ");
    }
}