public class PorfolioVersion: IIdEntity<int>, IVersionedEntity<int>
{
    public int Id {get;set;}
    public int Version {get;set;}

    public decimal Value {get;set;}
}