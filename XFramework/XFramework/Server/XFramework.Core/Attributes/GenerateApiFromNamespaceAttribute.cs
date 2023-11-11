namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateApiFromNamespaceAttribute(string @namespace, string[] types) : Attribute;