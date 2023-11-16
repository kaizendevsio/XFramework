using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;


[Generator]
public class SignalRHandlerGenerator : ISourceGenerator
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
        using Microsoft.AspNetCore.Builder;
        using XFramework.Core.DataAccess.Commands;
        using XFramework.Core.DataAccess.Query;
        using XFramework.Domain.Generic.Contracts;
        using XFramework.Domain.Generic.Contracts.Requests;
        using XFramework.Domain.Generic.Contracts.Responses;
        using XFramework.Domain.Generic.BusinessObjects;
        using XFramework.Integration.Extensions;
        using XFramework.Integration.Drivers;
        using XFramework.Integration.Services.Helpers;
        using MediatR;
        using System;
        using System.Linq.Expressions;
        using Microsoft.AspNetCore.SignalR.Client;
        using Asp.Versioning;
        using Asp.Versioning.Conventions;
        using Microsoft.AspNetCore.Components;

        namespace {serviceName}.Api.SignalR.Handlers
        {{
               
                    ");
        
        codeBuilder.AppendLine("");
        
        foreach (var model in models)
        {
            codeBuilder.Append($"                    // SignalR Handler for {model}");
            codeBuilder.AppendLine("");
            codeBuilder.Append("                    ");
            codeBuilder.AppendLine("");
            
            // GetList
            
            codeBuilder.AppendLine($"                        public class Get{model}ListHandler : BaseSignalRHandler, ISignalREventHandler");
            codeBuilder.AppendLine("                         {");
            codeBuilder.AppendLine("                             public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)");
            codeBuilder.AppendLine("                             {");
            codeBuilder.AppendLine($"                                 HandleRequestQuery<GetList<{model}>, PaginatedResult<{model}>>(connection, mediator, logger, metricsMonitor, scopeFactory);");
            codeBuilder.AppendLine("                             }");
            codeBuilder.AppendLine("                         }");
            
            
            // Get
            
            codeBuilder.AppendLine($"                        public class Get{model}Handler : BaseSignalRHandler, ISignalREventHandler");
            codeBuilder.AppendLine("                         {");
            codeBuilder.AppendLine("                             public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)");
            codeBuilder.AppendLine("                             {");
            codeBuilder.AppendLine($"                                 HandleRequestQuery<Get<{model}>, {model}>(connection, mediator, logger, metricsMonitor, scopeFactory);");
            codeBuilder.AppendLine("                             }");
            codeBuilder.AppendLine("                         }");
            

            // Create
            
            codeBuilder.AppendLine($"                        public class Create{model}Handler : BaseSignalRHandler, ISignalREventHandler");
            codeBuilder.AppendLine("                         {");
            codeBuilder.AppendLine("                             public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)");
            codeBuilder.AppendLine("                             {");
            codeBuilder.AppendLine($"                                 HandleRequestCmd<Create<{model}>, {model}>(connection, mediator, logger, metricsMonitor, scopeFactory);");
            codeBuilder.AppendLine("                             }");
            codeBuilder.AppendLine("                         }");

            // Replace
            
            codeBuilder.AppendLine($"                        public class Replace{model}Handler : BaseSignalRHandler, ISignalREventHandler");
            codeBuilder.AppendLine("                         {");
            codeBuilder.AppendLine("                             public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)");
            codeBuilder.AppendLine("                             {");
            codeBuilder.AppendLine($"                                 HandleRequestCmd<Replace<{model}>, {model}>(connection, mediator, logger, metricsMonitor, scopeFactory);");
            codeBuilder.AppendLine("                             }");
            codeBuilder.AppendLine("                         }");

            // Patch
            
            codeBuilder.AppendLine($"                        public class Patch{model}Handler : BaseSignalRHandler, ISignalREventHandler");
            codeBuilder.AppendLine("                         {");
            codeBuilder.AppendLine("                             public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)");
            codeBuilder.AppendLine("                             {");
            codeBuilder.AppendLine($"                                 HandleRequestCmd<Patch<{model}>, {model}>(connection, mediator, logger, metricsMonitor, scopeFactory);");
            codeBuilder.AppendLine("                             }");
            codeBuilder.AppendLine("                         }");

            // Delete
            
            codeBuilder.AppendLine($"                        public class Delete{model}Handler : BaseSignalRHandler, ISignalREventHandler");
            codeBuilder.AppendLine("                         {");
            codeBuilder.AppendLine("                             public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)");
            codeBuilder.AppendLine("                             {");
            codeBuilder.AppendLine($"                                 HandleRequestCmd<Delete<{model}>, {model}>(connection, mediator, logger, metricsMonitor, scopeFactory);");
            codeBuilder.AppendLine("                             }");
            codeBuilder.AppendLine("                         }");
            
        }
        codeBuilder.AppendLine(@"
         
        }");

        context.AddSource("SignalRHandlerGenerator.g.cs", SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
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