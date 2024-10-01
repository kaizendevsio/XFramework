namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
#pragma warning disable CS9113 // Parameter is unread.
public class GenerateEndpointsAttribute(string @namespace, string[] types) : Attribute;
#pragma warning restore CS9113 // Parameter is unread.
