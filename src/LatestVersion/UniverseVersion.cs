public class UniverseVersion: IVersionedEntity<int>, IIdEntity<int>
{
    public int Id {get;set;}
    public int Version {get; set;}

    public string Name {get;set;}
}

