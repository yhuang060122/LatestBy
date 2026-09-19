public interface IIdEntity<T>
    where T: struct
{
    T Id { get; }
}