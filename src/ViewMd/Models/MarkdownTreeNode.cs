using System.Collections.ObjectModel;

namespace ViewMd.Models;

public sealed class MarkdownTreeNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public ObservableCollection<MarkdownTreeNode> Children { get; } = [];
}
