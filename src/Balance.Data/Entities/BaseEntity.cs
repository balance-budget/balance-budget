namespace Balance.Data.Entities;

public abstract class BaseEntity<TId>
    where TId : struct
{
    public TId Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
}
