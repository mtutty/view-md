using ViewMd.Models;

namespace ViewMd.Services;

public static class MarkdownFileTreeBuilder
{
    private static readonly string[] MarkdownExtensions = [".md", ".markdown"];

    public static MarkdownTreeNode? Build(string rootDirectory)
    {
        var root = new MarkdownTreeNode { Name = Path.GetFileName(rootDirectory.TrimEnd(Path.DirectorySeparatorChar)), FullPath = rootDirectory, IsDirectory = true };
        return Populate(root) ? root : null;
    }

    // Returns false (and leaves node childless) when the subtree contains no Markdown files,
    // so empty directories get pruned out of the tree entirely.
    private static bool Populate(MarkdownTreeNode node)
    {
        var hasContent = false;

        IEnumerable<string> subdirectories;
        IEnumerable<string> files;
        try
        {
            subdirectories = Directory.EnumerateDirectories(node.FullPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
            files = Directory.EnumerateFiles(node.FullPath)
                .Where(f => MarkdownExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }

        foreach (var dir in subdirectories)
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.StartsWith('.'))
            {
                continue; // skip hidden dirs (.git, .charter, etc.)
            }

            var childNode = new MarkdownTreeNode { Name = dirName, FullPath = dir, IsDirectory = true };
            if (Populate(childNode))
            {
                node.Children.Add(childNode);
                hasContent = true;
            }
        }

        foreach (var file in files)
        {
            node.Children.Add(new MarkdownTreeNode { Name = Path.GetFileName(file), FullPath = file, IsDirectory = false });
            hasContent = true;
        }

        return hasContent;
    }
}
