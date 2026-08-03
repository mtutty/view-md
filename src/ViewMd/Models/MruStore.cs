namespace ViewMd.Models;

// Serialized as-is to ~/.config/view-md/mru.json.
public sealed class MruStore
{
    public List<RecentEntry> Files { get; set; } = [];
    public List<RecentEntry> Folders { get; set; } = [];
}
