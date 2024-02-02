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
        var namespaceName = BaseSourceGenerator.GetNamespace(context, "GenerateApiFromNamespace");

        var models = BaseSourceGenerator.GetModels(context, "GenerateApiFromNamespace", $"{serviceName}ApiGenerator");
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
        using {namespaceName};
            public static partial class {serviceName}ApiGenerator
            {{
                public static IApplicationBuilder GenerateMinimalApi(this IApplicationBuilder appBuilder)
                {{

                    var app = appBuilder as WebApplication;
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
                return app as IApplicationBuilder;
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
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapGet(""/"", async (
            [FromServices] IMediator mediator, 
            [FromQuery] int pageSize, 
            [FromQuery] int pageNumber, 
            [FromQuery] bool noCache, 
            [FromQuery] bool? includeNavigations, 
            [FromQuery] string filter, 
            [FromQuery] Guid tenantId,
            [FromQuery] int navigationDepth = 1,
            [FromQuery] List<string>? includes = null) =>");
            
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var filters = JsonSerializer.Deserialize<List<QueryFilter>>(filter);
                            var request = new GetList<{model}>(
                                PageSize: pageSize, 
                                PageNumber: pageNumber, 
                                TenantId: tenantId, 
                                NoCache: noCache, 
                                NavigationDepth: navigationDepth,
                                IncludeNavigations: includeNavigations,
                                Filter: filters,
                                Includes: includes);
                            return await mediator.Send(request);
                        }});");

        // Get
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{model.ToLower()}Model.MapGet(""/{{id}}"", async (
            IMediator mediator, 
            Guid id, 
            [FromQuery] bool noCache, 
            [FromQuery] bool? includeNavigations,
            [FromQuery] Guid tenantId,      
            [FromQuery] int navigationDepth = 1, 
            [FromQuery] List<string>? includes = null) =>");
        codeBuilder.Append("                        ");
        codeBuilder.AppendLine($@"{{
                            var request = new Get<{model}>(
                                Id: id,
                                TenantId: tenantId, 
                                NoCache: noCache,
                                NavigationDepth: navigationDepth,
                                IncludeNavigations: includeNavigations,
                                Includes: includes);
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