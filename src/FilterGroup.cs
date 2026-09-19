public class FilterGroup
{
    public LogicalOperator Operator {get;set;} = LogicalOperator.And;

    public bool Not {get;set;}

    public List<FilterRule> Rules {get;set;}
    public List<FilterGroup> Groups {get;set;}
}