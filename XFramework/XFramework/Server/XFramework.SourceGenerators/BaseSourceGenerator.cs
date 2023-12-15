using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XFramework.SourceGenerators;

public static class BaseSourceGenerator
{
    public static string GetNamespace(GeneratorExecutionContext context, string attribute)
    {
        return GetAttributeParameterValue(context, attribute, 0);
    }
    
    public static string GetAttributeParameterValue(GeneratorExecutionContext context, string attribute, int parameterIndex)
    {
        return (from attributeSyntax in (from syntaxTree in context.Compilation.SyntaxTrees
                let semanticModel = context.Compilation.GetSemanticModel(syntaxTree)
                let classNodes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                from classNode in classNodes
                let classSymbol = semanticModel.GetDeclaredSymbol(classNode)
                select Enumerable.SelectMany<AttributeListSyntax, AttributeSyntax>(classNode.AttributeLists, a => a.Attributes)
                    .FirstOrDefault(attr => attr.Name.ToString().Contains(attribute))).OfType<AttributeSyntax>()
            select attributeSyntax.ArgumentList.Arguments[parameterIndex]
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
    
    public static List<ClassDeclarationSyntax> GetClasses(GeneratorExecutionContext context, string attribute, string classNameSuffix)
    {
        List<ClassDeclarationSyntax> classes = new();

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
                    classes.Add(classNode);
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
        
        var namespaceArgumentSyntax = attributeSyntax.ArgumentList.Arguments[0];
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
}