using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

public static class FilterExpressionBuilder
{
    public static Expression<Func<T, bool>> Build<T>(FilterGroup filter)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var body = BuildGroup<T>(parameter, filter);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression BuildGroup<T>(
        ParameterExpression parameter,
        FilterGroup group
    )
    {
        Expression ? result = null;

        // Rules
        foreach(var rule in group.Rules)
        {
            var expr = BuildRule<T>(parameter, rule);
            result = Combine(result, expr, group.Operator);
        }

        // Children
        foreach(var child in group.Groups)
        {
            var expr = BuildGroup<T>(parameter, child);
            result = Combine(result, expr, group.Operator);
        }

        result ??= Expression.Constant(true);

        if(group.Not)
            result = Expression.Not(result);

        return result;
    }

    private static Expression Combine(
        Expression? left,
        Expression right,
        LogicalOperator op
    )
    {
        if (left == null)
        {
            return right;
        }

        return op == LogicalOperator.And
        ? Expression.AndAlso(left, right)
        : Expression.OrElse(left, right);
    }

    private static Expression BuildRule<T>(
        ParameterExpression parameter,
        FilterRule rule
    )
    {
        var property = Expression.Property(parameter, rule.Property);
        var targetType = Nullable.GetUnderlyingType(property.Type) ?? property.Type;

        var value = rule.Value == null
        ? null
        : Convert.ChangeType(rule.Value, targetType);

        var constant = Expression.Constant(value, property.Type);

        return rule.Operator switch
        {
            ComparisonOperator.Equal => 
                Expression.Equal(property, constant),
            
            ComparisonOperator.NotEqual =>
                Expression.NotEqual(property, constant),

            ComparisonOperator.GreaterThan =>
                Expression.GreaterThan(property, constant),

            ComparisonOperator.GreaterThanOrEqual =>
                Expression.GreaterThanOrEqual(property, constant),

            ComparisonOperator.LessThan =>
                Expression.LessThan(property, constant),

            ComparisonOperator.LessThanOrEqual =>
                Expression.LessThanOrEqual(property, constant),

            ComparisonOperator.Contains =>
                Expression.Call(
                    property,
                    nameof(string.Contains),
                    Type.EmptyTypes,
                    constant),

            ComparisonOperator.StartsWith =>
                Expression.Call(
                    property,
                    nameof(string.StartsWith),
                    Type.EmptyTypes,
                    constant),

            ComparisonOperator.EndsWith =>
                Expression.Call(
                    property,
                    nameof(string.EndsWith),
                    Type.EmptyTypes,
                    constant),

            _ => throw new NotSupportedException()
        };
    }
}