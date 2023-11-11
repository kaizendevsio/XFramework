namespace XFramework.Integration.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateStreamFlowWrapperAttribute(string @namespace, string[] types) : Attribute;