namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class StreamflowRequestAttribute(string @namespace, List<(Type requestModel, Type responseModel)> types) : Attribute;