
public interface IVersionedEntity<T> where T: struct
{
    T Version {get;}
}