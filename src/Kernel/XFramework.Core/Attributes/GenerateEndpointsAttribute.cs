namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateEndpointsAttribute(string @namespace, string[] types) : Attribute;