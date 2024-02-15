namespace XFramework.Integration.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class StreamFlowWrapperAttribute(string @namespace, string[] types) : Attribute;