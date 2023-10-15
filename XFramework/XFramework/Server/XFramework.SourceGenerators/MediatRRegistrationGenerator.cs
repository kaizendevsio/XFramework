using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

[Generator]
public class MediatRRegistrationGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
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
        using MediatR;
        using Microsoft.Extensions.DependencyInjection;
        using XFramework.Core.DataAccess.Commands;
        using XFramework.Core.DataAccess.Query;
        using XFramework.Domain.Generic.Contracts;

        namespace XFramework.Api.Extensions
        {
            public static class MediatRServiceExtensions
            {
                public static void AddMediatRHandlers(this IServiceCollection services)
                {
                    ");

        foreach (var model in models)
        {
            codeBuilder.Append($"                    //CRUD Handler for {model}");
            codeBuilder.AppendLine("");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"services.AddTransient<IRequestHandler<Create<{model}>, CmdResponse<{model}>>, CreateHandler<{model}>>();");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"services.AddTransient<IRequestHandler<Delete<{model}>, CmdResponse<{model}>>, DeleteHandler<{model}>>();");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"services.AddTransient<IRequestHandler<Patch<{model}>, CmdResponse<{model}>>, PatchHandler<{model}>>();");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"services.AddTransient<IRequestHandler<Replace<{model}>, CmdResponse<{model}>>, ReplaceHandler<{model}>>();");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"services.AddTransient<IRequestHandler<Get<{model}>, QueryResponse<{model}>>, GetHandler<{model}>>();");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine($@"services.AddTransient<IRequestHandler<GetList<{model}>, QueryResponse<PaginatedResult<{model}>>>, GetListHandler<{model}>>();");
            codeBuilder.AppendLine("");
        }

        codeBuilder.AppendLine(@"
                }
            }
        }");

        context.AddSource("MediatRServiceExtensions.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }
}