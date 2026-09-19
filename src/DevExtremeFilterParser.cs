using System.Text.Json.Nodes;

public static class DevExtremeFilterParser
{
    public static FilterGroup Parse(JsonArray filter)
    {
        return ParseGroup(filter);
    }

    private static FilterGroup ParseGroup(JsonArray array)
    {
        var group = new FilterGroup();

        LogicalOperator currentOperator = LogicalOperator.And;

        foreach(var node in array)
        {
            if (node == null)
                continue;

            // "and" / "or"
            if (node is JsonValue value &&
                value.TryGetValue<string>(out var op))
            {
                currentOperator = op.ToLower() == "or"
                    ? LogicalOperator.Or
                    : LogicalOperator.And;

                group.Operator = currentOperator;
                continue;
            }

            if (node is not JsonArray child)
                continue;

            // Rule : ["Age", ">", 30]
            if (IsRule(child))
            {
                group.Rules.Add(ParseRule(child));
            }
            // Nested Group
            else
            {
                group.Groups.Add(ParseGroup(child));
            }
        }

        return group;
    }

    private static bool IsRule(JsonArray array)
    {
        return array.Count == 3 && 
        array[0] is JsonValue &&
        array[1] is JsonValue;
    }

    private static FilterRule ParseRule(JsonArray array)
    {
        var property = array[0]!.GetValue<string>();
        var op = array[1]!.GetValue<string>();
        var value = GetValue(array[2]);

        return new FilterRule
        {
            Property = property,
            Operator = MapOperator(op),
            Value = value
        };
    }

    private static object? GetValue(JsonNode? node)
    {
        if (node == null) return null;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var s)) return s;
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l)) return l;
            if (value.TryGetValue<decimal>(out var d)) return d;
            if (value.TryGetValue<double>(out var db)) return db;
            if (value.TryGetValue<bool>(out var b)) return b;
        }

        return node.ToJsonString();
    }

    private static ComparisonOperator MapOperator(string op)
    {
        return op switch
        {
            "=" => ComparisonOperator.Equal,
            "<>" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessThanOrEqual,
            "contains" => ComparisonOperator.Contains,
            "startswith" => ComparisonOperator.StartsWith,
            "endswith" => ComparisonOperator.EndsWith,
            _ => throw new NotSupportedException($"Operator {op}")
        };
    }
}