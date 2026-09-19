public class FilterRule
{
    public string Property {get;set;} = "";
    public ComparisonOperator Operator {get;set;}
    public object? Value {get;set;}
}