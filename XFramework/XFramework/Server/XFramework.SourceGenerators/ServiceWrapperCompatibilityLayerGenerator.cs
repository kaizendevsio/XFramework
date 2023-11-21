/*using System.Text;
using Microsoft.CodeAnalysis;

namespace XFramework.SourceGenerators;

[Generator]
public class ServiceWrapperCompatibilityLayerGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var serviceName = context.Compilation.AssemblyName?.Split(".").First();
        var models = BaseSourceGenerator.GetModels(context);
        var codeBuilder = new StringBuilder();

        if (models.Count == 0)
        {
            return;
        }
        
    }
}*/