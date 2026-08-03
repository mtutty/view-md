namespace ViewMd.Models;

public enum RecentEntryKind
{
    File,
    Folder
}

public sealed class RecentEntry
{
    public required string Path { get; init; }
    public required RecentEntryKind Kind { get; init; }
    public required DateTimeOffset LastOpened { get; init; }
}
