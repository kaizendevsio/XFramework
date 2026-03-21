using System.Linq.Expressions;

namespace XFramework.Integration.DataContext.ExpressionVisitor;

internal static class MemberAccessParser
{
    public static string GetPropertyPath(Expression expression)
    {
        return expression switch
        {
            MemberExpression member => GetMemberPath(member),
            UnaryExpression { NodeType: ExpressionType.Convert } unary => GetPropertyPath(unary.Operand),
            _ => throw new NotSupportedException(
                $"Cannot extract property path from expression of type {expression.NodeType}")
        };
    }

    private static string GetMemberPath(MemberExpression member)
    {
        var parts = new Stack<string>();
        Expression? current = member;

        while (current is MemberExpression m)
        {
            parts.Push(m.Member.Name);
            current = m.Expression;
        }

        return string.Join(".", parts);
    }
}
