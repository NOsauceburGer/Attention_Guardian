namespace AttentionGuardian.Core;

public abstract record TodoItem
{
    protected TodoItem(Guid id, string title, bool isMandatory)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A todo item must have a non-empty identifier.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A todo item must have a title.", nameof(title));
        }

        Id = id;
        Title = title.Trim();
        IsMandatory = isMandatory;
    }

    public Guid Id { get; }

    public string Title { get; }

    public bool IsMandatory { get; }
}
