namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateApiFromNamespaceAttribute(string namespaceString) : Attribute
{
    public string Namespace { get; } = namespaceString;
}

  