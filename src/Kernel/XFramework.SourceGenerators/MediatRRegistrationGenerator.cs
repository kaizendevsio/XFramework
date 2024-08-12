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
        var assemblyName = context.Compilation.AssemblyName;
        var serviceName =  assemblyName!.Contains('.') 
            ? assemblyName.Split(".").First()
            : assemblyName;
        
        var namespaceName = BaseSourceGenerator.GetNamespace(context, "GenerateEndpoints");
        var models = BaseSourceGenerator.GetModels(context, "GenerateEndpoints", $"{serviceName}Endpoints");
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
        using XFramework.Domain.Shared.Contracts;
        using XFramework.Domain.Shared.Contracts.Requests;


        namespace {assemblyName}.Extensions
        {{
        using {namespaceName};
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
}