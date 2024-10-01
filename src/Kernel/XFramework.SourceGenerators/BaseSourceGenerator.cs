using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XFramework.SourceGenerators;

public static class BaseSourceGenerator
{
    public static string? GetNamespace(GeneratorExecutionContext context, string attribute)
    {
        return GetAttributeParameterValue(context, attribute, 0);
    }

    private static string? GetAttributeParameterValue(GeneratorExecutionContext context, string attribute, int parameterIndex)
    {
        return (from attributeSyntax in (from syntaxTree in context.Compilation.SyntaxTrees
                let semanticModel = context.Compilation.GetSemanticModel(syntaxTree)
                let classNodes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                from classNode in classNodes
                let classSymbol = semanticModel.GetDeclaredSymbol(classNode)
                select Enumerable.SelectMany<AttributeListSyntax, AttributeSyntax>(classNode.AttributeLists, a => a.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute))).OfType<AttributeSyntax>()
            select attributeSyntax!.ArgumentList!.Arguments[parameterIndex]
            into namespaceArgumentSyntax
            select SyntaxNodeExtensions.NormalizeWhitespace<ExpressionSyntax>(namespaceArgumentSyntax.Expression).ToFullString()
            into parameterValue
            select parameterValue.Replace("\"", string.Empty)).FirstOrDefault();
    }
  
    public static List<string> GetModels(GeneratorExecutionContext context, string attribute, string className)
    {
        List<string> models = new();

        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            var classNodes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

            
            foreach (var classNode in classNodes.Where(i => i.Identifier.Text == className))
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
                var attributeSyntax = classNode.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute));

                if (attributeSyntax == null) continue;
                
                var namespaceArgumentSyntax = attributeSyntax.ArgumentList!.Arguments[0];
                var typesArgumentSyntax = attributeSyntax.ArgumentList.Arguments[1];

                var namespaceValue = namespaceArgumentSyntax.Expression.NormalizeWhitespace().ToFullString();
                var typesArraySyntax = typesArgumentSyntax.Expression as ImplicitArrayCreationExpressionSyntax;

                if (typesArraySyntax == null) continue;
                
                foreach (var typeArgumentSyntax in typesArraySyntax.Initializer.Expressions)
                {
                    if (typeArgumentSyntax is not InvocationExpressionSyntax invocationExpression ||
                        invocationExpression.Expression is not IdentifierNameSyntax identifierName ||
                        identifierName.Identifier.Text != "nameof") continue;
                    
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

        return models;
    }
    
    public static List<(ClassDeclarationSyntax ClassDeclarationSyntax, SemanticModel SemanticModel)> GetClasses(GeneratorExecutionContext context, string attribute, string classNameSuffix)
    {
        List<(ClassDeclarationSyntax, SemanticModel)> classes = new();

        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            var classNodes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
            
            foreach (var classNode in classNodes.Where(i => i.Identifier.Text.Contains(classNameSuffix, StringComparison.InvariantCultureIgnoreCase)))
            {
                var attributeSyntax = classNode.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute));

                if (attributeSyntax != null)
                {
                    classes.Add((classNode, semanticModel));
                }
            }
        }

        return classes;
    }
    
    public static List<string> GetModels(ClassDeclarationSyntax classDeclarationSyntax, string attribute)
    {
        List<string> models = new();
        
        var attributeSyntax = classDeclarationSyntax.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute));

        if (attributeSyntax == null) return models;
        
        var namespaceArgumentSyntax = attributeSyntax.ArgumentList!.Arguments[0];
        var typesArgumentSyntax = attributeSyntax.ArgumentList.Arguments[1];

        var namespaceValue = namespaceArgumentSyntax.Expression.NormalizeWhitespace().ToFullString();
        var typesArraySyntax = typesArgumentSyntax.Expression as ImplicitArrayCreationExpressionSyntax;

        if (typesArraySyntax == null) return models;
        
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

        return models;
    }
    
    public static string? GetAttributeParameterValue(ClassDeclarationSyntax classDeclarationSyntax, string attribute, int parameterIndex)
    {
        var syntax = Enumerable.SelectMany<AttributeListSyntax, AttributeSyntax>(classDeclarationSyntax.AttributeLists, a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute));
        var attributeSyntax = syntax as AttributeSyntax;
        if (attributeSyntax == null) return null;

        var namespaceArgumentSyntax = attributeSyntax.ArgumentList!.Arguments[parameterIndex];
        var parameterValue = SyntaxNodeExtensions.NormalizeWhitespace<ExpressionSyntax>(namespaceArgumentSyntax.Expression).ToFullString();
        var s = parameterValue.Replace("\"", string.Empty);
        return s;
    }
  
    public static string? GetAttributeParameterValueFromType(ClassDeclarationSyntax classDeclarationSyntax, SemanticModel semanticModel, string attribute, int parameterIndex)
    {
        var syntax = Enumerable.SelectMany(classDeclarationSyntax.AttributeLists, a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute));
        var attributeSyntax = syntax;
        if (attributeSyntax == null) return null;
        
        var namespaceArgumentSyntax = attributeSyntax.ArgumentList!.Arguments[parameterIndex].Expression as TypeOfExpressionSyntax;
        var typeInfo = semanticModel.GetTypeInfo(namespaceArgumentSyntax!.Type);
        return typeInfo.Type?.ToDisplayString();
    }
}