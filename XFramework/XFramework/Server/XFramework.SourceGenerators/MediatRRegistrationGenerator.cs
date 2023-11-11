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
        var serviceName = context.Compilation.AssemblyName?.Split(".").First();
        var models = GetModels(context);
        var codeBuilder = new StringBuilder();

        if (models.Count == 0)
        {
            return;
        }
        
        codeBuilder.AppendLine($@"
        using MediatR;
        using Microsoft.Extensions.DependencyInjection;
        using XFramework.Core.DataAccess.Commands;
        using XFramework.Core.DataAccess.Query;
        using XFramework.Domain.Generic.Contracts;
        using XFramework.Domain.Generic.Contracts.Requests;

        namespace {serviceName}.Api.Extensions
        {{
            public static class MediatRServiceExtensions
            {{
                public static void AddMediatRHandlers(this IServiceCollection services)
                {{
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
    
    private static List<string> GetModels(GeneratorExecutionContext context)
    {
        List<string> models = new();

        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            var classNodes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

            
            foreach (var classNode in classNodes)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
                var attributeSyntax = classNode.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString().Contains("GenerateApiFromNamespace"));

                if (attributeSyntax != null)
                {
                    var namespaceArgumentSyntax = attributeSyntax.ArgumentList.Arguments[0];
                    var typesArgumentSyntax = attributeSyntax.ArgumentList.Arguments[1];

                    var namespaceValue = namespaceArgumentSyntax.Expression.NormalizeWhitespace().ToFullString();
                    var typesArraySyntax = typesArgumentSyntax.Expression as ImplicitArrayCreationExpressionSyntax;

                    if (typesArraySyntax != null)
                    {
                        foreach (var typeArgumentSyntax in typesArraySyntax.Initializer.Expressions)
                        {
                            if (typeArgumentSyntax is InvocationExpressionSyntax invocationExpression &&
                                invocationExpression.Expression is IdentifierNameSyntax identifierName &&
                                identifierName.Identifier.Text == "nameof")
                            {
                                var nameofArgument = invocationExpression.ArgumentList.Arguments[0].Expression;
                                if (nameofArgument is IdentifierNameSyntax identifierArgument)
                                {
                                    var typeValue = identifierArgument.Identifier.Text;
                                    models.Add(typeValue);
                                    // Do something with typeValue
                                }
                            }
                        }
                    }
                }
            }
        }

        return models;
    }
}