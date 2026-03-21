using System.Linq.Expressions;

namespace XFramework.Integration.DataContext.ExpressionVisitor;

internal static class SortExpressionParser
{
    public static string GetPropertyName<T, TKey>(Expression<Func<T, TKey>> keySelector)
    {
        return keySelector.Body switch
        {
            MemberExpression member => MemberAccessParser.GetPropertyPath(member),
            UnaryExpression { NodeType: ExpressionType.Convert } unary => MemberAccessParser.GetPropertyPath(unary.Operand),
            _ => throw new NotSupportedException(
                $"Sort expression must be a property access, got {keySelector.Body.NodeType}")
        };
    }
}
