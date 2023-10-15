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
        // Assuming you have a custom attribute that indicates which types should have APIs generated.
        // You can retrieve those types and pass them into the 'models' list.
        // For simplicity, I'm using a hardcoded list here.
        
        // Retrieve classes with the 'GenerateApiFrom' attribute.
        var classWithAttribute = context.Compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>())
            .FirstOrDefault(m => m.AttributeLists
                .Any(a => a.Attributes
                    .Any(attr => attr.Name.ToString().Contains("GenerateApiFromNamespace"))));

        if (classWithAttribute is null)
        {
            return;
        }
        
        var attributeSyntax = classWithAttribute.AttributeLists
            .SelectMany(a => a.Attributes)
            .First(attr => attr.Name.ToString().Contains("GenerateApiFromNamespace"));
        
        var namespaceArgument = attributeSyntax.ArgumentList?.Arguments.FirstOrDefault();
        var namespaceStr = namespaceArgument.Expression.NormalizeWhitespace().ToFullString().Trim('"');

        var currentNamespace = context.Compilation.GlobalNamespace;
        var namespaceParts = namespaceStr.Split('.');

        foreach (var part in namespaceParts)
        {
            currentNamespace = currentNamespace.GetNamespaceMembers().FirstOrDefault(i => i.Name == part);
            if (currentNamespace == null) break; // Exit if any namespace part is not found
        }
        
        var models = currentNamespace?
            .GetMembers()
            .Where(i => i.IsType)
            .Select(i => i.Name)
            .ToList();

        var codeBuilder = new StringBuilder();

        codeBuilder.AppendLine(@"
        using Microsoft.AspNetCore.Builder;
        using XFramework.Core.DataAccess.Commands;
        using XFramework.Core.DataAccess.Query;
        using XFramework.Domain.Generic.Contracts;
        using XFramework.Domain.Generic.BusinessObjects;
        using XFramework.Integration.Extensions;
        using MediatR;
        using System;
        using System.Linq.Expressions;
        using Asp.Versioning;
        using Asp.Versioning.Conventions;

        namespace XFramework.Api.Generators
        {
            public static partial class MinimalApiGenerator
            {
                public static WebApplication GenerateMinimalApi(this WebApplication app, Type[] allowedModels)
                {
                    ");

        codeBuilder.AppendLine(@"
                    var versionSet = app.NewApiVersionSet()
                    .HasApiVersion(3.0)
                    .ReportApiVersions()
                    .Build();");
        
        codeBuilder.AppendLine("");
        
        foreach (var model in models)
        {

            codeBuilder.Append($"                    // API Handler for {model}");
            codeBuilder.AppendLine("");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"if(allowedModels.Contains(typeof({model}))) {{");
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
                            var filterExpression = HelperExtensions.DeserializeFilter<{model}>(filter); 
                            var request = new GetList<{model}>(pageSize, pageNumber, tenantId, includeNavigations, filterExpression);
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
            codeBuilder.AppendLine("}");
        }
        codeBuilder.AppendLine(@"
                return app;
                }
            }
        }");

        context.AddSource("MinimalApiEndpointGenerator.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }
}