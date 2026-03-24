namespace XFramework.Integration.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class BoltWrapperAttribute(string @namespace, string[] types) : Attribute;