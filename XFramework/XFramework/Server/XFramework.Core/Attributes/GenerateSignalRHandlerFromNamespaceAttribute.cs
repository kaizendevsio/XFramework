namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateSignalRHandlerFromNamespaceAttribute(string @namespace, string[] types) : Attribute;