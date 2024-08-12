using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

[Generator]
public class ServiceWrapperGenerator : ISourceGenerator
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
        
        var namespaceName = BaseSourceGenerator.GetNamespace(context, "StreamFlowWrapper");
        var classes = BaseSourceGenerator.GetClasses(context, "StreamFlowWrapper", "ServiceWrapper");
        var serviceId = serviceName.ToSha256();
        
        foreach (var item in classes)
        {
            Generate(context, namespaceName, serviceId, item.ClassDeclarationSyntax);
        }
    }

    private static void Generate(GeneratorExecutionContext context, string namespaceName, string serviceId, ClassDeclarationSyntax classDeclarationSyntax)
    {
        var models = BaseSourceGenerator.GetModels(classDeclarationSyntax, "StreamFlowWrapper");
        var codeBuilder = new StringBuilder();
        var serviceName = classDeclarationSyntax.Identifier.Text.Replace("ServiceWrapper", string.Empty);
        
        if (models.Count == 0)
        {
            return;
        }
        
        codeBuilder.AppendLine(
            $$"""
                         using XFramework.Domain.Shared.BusinessObjects;
                         using XFramework.Domain.Shared.Contracts;
                         using XFramework.Domain.Shared.Contracts.Requests;
                         using XFramework.Domain.Shared.Contracts.Responses;
                         using XFramework.Domain.Shared.Interfaces;
                         using XFramework.Integration.Drivers;
                         using XFramework.Integration.Abstractions;
                         using XFramework.Integration.Abstractions.Wrappers;
                         using Microsoft.Extensions.DependencyInjection;
                         using Microsoft.Extensions.Configuration;
                         using XFramework;
                         using System.Linq.Expressions;
                         using Serilog;
                         using System;
                         using System.Net;
                         
              
              
                         namespace {{serviceName}}.Integration.Drivers
                         {
                         using {{namespaceName}};
              """);

        codeBuilder.AppendLine(
            $$"""
                         public partial interface I{{serviceName}}ServiceWrapper : IXFrameworkService, IServiceWrapper
                         {
              """);
        foreach (var model in models)
        {
            codeBuilder.AppendLine(
                $$"""
                             public I{{model}}CrudService {{model}} { get; init; }
                  """);
        }
        codeBuilder.AppendLine("            }");

        foreach (var model in models)
        {
            codeBuilder.AppendLine($"public interface I{model}CrudService : ICrudService<{model}>;");
        }
        
        codeBuilder.AppendLine($"public partial record {serviceName}ServiceWrapper(");
        foreach (var model in models)
        {
            codeBuilder.AppendLine($"I{model}CrudService {model}{(models.Last() == model ? "" : "," )}");
        }
        codeBuilder.AppendLine($"{(models.Any() ? "," : string.Empty)} IMessageBusWrapper messageBusDriver, IConfiguration configuration");
        codeBuilder.AppendLine($") : DriverBase(messageBusDriver, configuration), I{serviceName}ServiceWrapper");
        
        codeBuilder.AppendLine($$"""
                                 {
                                     public override void Initialize()
                                     {
                                         TargetClient = "{{serviceId}}";
                                     }
                                 }
                                 """);
        
        // Generate implementation start

        foreach (var model in models)
        {
            codeBuilder.AppendLine(
                $$"""
                  public record {{model}}CrudService : DriverBase, I{{model}}CrudService, IServiceWrapper
                  {
                      public {{model}}CrudService(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
                      {
                           MessageBusDriver = messageBusDriver;
                           Configuration = configuration;
                           TargetClient = "{{serviceId}}";
                      }
                      
                      public async Task<CmdResponse<{{model}}>> Create({{model}} entity)
                      {
                          var t = await SendVoidAsync<Create<{{model}}>, {{model}}>(new Create<{{model}}>(entity));
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message,
                              Response = t?.Response
                          };
                      }
                  
                      public async Task<CmdResponse<{{model}}>> Patch({{model}} entity)
                      {
                          var t = await SendVoidAsync<Patch<{{model}}>, {{model}}>(new Patch<{{model}}>(entity));
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message,
                              Response = t?.Response
                          };
                      }
                  
                      public async Task<CmdResponse<{{model}}>> Replace({{model}} entity)
                      {
                          var t = await SendVoidAsync<Replace<{{model}}>, {{model}}>(new Replace<{{model}}>(entity));
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message,
                              Response = t?.Response
                          };
                      }
                  
                      public async Task<CmdResponse> Delete({{model}} entity)
                      {
                          var t = await SendVoidAsync(new Delete<{{model}}>(entity));
                          return new CmdResponse
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message
                          };
                      }
                  
                      public async Task<QueryResponse<PaginatedResult<{{model}}>>> GetList(
                        int pageSize, 
                        int pageNumber, 
                        Guid? tenantId = null, 
                        bool noCache = true, 
                        int navigationDepth = 1,
                        bool? includeNavigations = false,
                        List<QueryFilter>? filter = null,
                        List<string>? includes = null)
                      {
                          return await SendAsync<GetList<{{model}}>, PaginatedResult<{{model}}>>(new GetList<{{model}}>(
                            PageSize: pageSize, 
                            PageNumber: pageNumber, 
                            TenantId: tenantId, 
                            NoCache: noCache, 
                            IncludeNavigations: includeNavigations,
                            NavigationDepth: navigationDepth,
                            Filter: filter,
                            Includes: includes
                            ));
                      }
                  
                      public async Task<QueryResponse<{{model}}>> Get(
                        Guid id, 
                        Guid? tenantId = null, 
                        bool noCache = true,
                        int navigationDepth = 1,
                        bool? includeNavigations = null,
                        List<string>? includes = null)
                      {
                          return await SendAsync<Get<{{model}}>, {{model}}>(new Get<{{model}}>(
                            Id: id, 
                            TenantId: tenantId, 
                            NoCache: noCache, 
                            IncludeNavigations: includeNavigations,
                            NavigationDepth: navigationDepth,
                            Includes: includes
                            ));
                      }
                  }
                  """);   
        }

        
        // Generate implementation end
        
        // Generate Service Registration Extension

        codeBuilder.AppendLine($$"""

                                 public static class {{serviceName}}ServiceWrapperExtensions
                                 {

                                 public static void Add{{serviceName}}WrapperServices(this IServiceCollection services)
                                  {
                                      Serilog.Log.Information("Registering {{serviceName}}ServiceWrapper services");
                                      services.AddSingleton<I{{serviceName}}ServiceWrapper, {{serviceName}}ServiceWrapper>();
                                 """);

        
        foreach (var model in models)
        {
            codeBuilder.AppendLine($$"""
                                      
                                     services.AddSingleton<I{{model}}CrudService, {{model}}CrudService>();

                                     """);
        }

        codeBuilder.AppendLine("}}");
        // Generate Service Registration Extension
        
        codeBuilder.AppendLine("}");

        // Generate the file
        context.AddSource($"{serviceName}ServiceWrapperGenerator.g.cs",
            SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }
}