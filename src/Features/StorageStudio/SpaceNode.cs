namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// One folder or file in the space tree that <see cref="SpaceScanner"/> builds. Folders carry
/// running totals - size, file and folder counts, size per <see cref="FileCategory"/> and the newest
/// modified time beneath them - which are kept current as files are added, so a tree from a stopped
/// scan is still internally consistent. The root's <see cref="Name"/> is the full root path; every
/// other node's is its own name, and <see cref="FullPath"/> is rebuilt from the chain of parents.
/// </summary>
public sealed class SpaceNode
{
    private List<SpaceNode>? _children;

    private SpaceNode(string name, SpaceNode? parent, bool isFolder)
    {
        Name = name;
        Parent = parent;
        IsFolder = isFolder;
        CategorySizes = isFolder ? new long[FileTypeClassifier.CategoryCount] : [];
        Depth = parent is null ? 0 : parent.Depth + 1;
    }

    public string Name { get; }
    public SpaceNode? Parent { get; }
    public bool IsFolder { get; }

    /// <summary>0 for the root, 1 for its direct children, and so on.</summary>
    public int Depth { get; }

    /// <summary>File size, or for a folder the total size of every file beneath it.</summary>
    public long Size { get; private set; }

    /// <summary>For a file, its modified time; for a folder, the newest file time beneath it.</summary>
    public DateTime LastWriteUtc { get; private set; }

    /// <summary>The file's category; folders report <see cref="FileCategory.Other"/>.</summary>
    public FileCategory Category { get; private init; } = FileCategory.Other;

    /// <summary>Files beneath a folder at any depth (a file counts itself as 1).</summary>
    public int FileCount { get; private set; }

    /// <summary>Folders beneath a folder at any depth (not counting itself).</summary>
    public int FolderCount { get; private set; }

    /// <summary>Bytes beneath a folder per category, indexed by <c>(int)FileCategory</c>. Empty for files.</summary>
    public long[] CategorySizes { get; }

    /// <summary>Direct children in the order they were added (see <see cref="SortBySize"/>).</summary>
    public IReadOnlyList<SpaceNode> Children => (IReadOnlyList<SpaceNode>?)_children ?? [];

    /// <summary>The absolute path, rebuilt from the root path and the names below it.</summary>
    public string FullPath => Parent is null ? Name : Path.Combine(Parent.FullPath, Name);

    /// <summary>The category holding the most bytes in a folder, or the file's own category.</summary>
    public FileCategory DominantCategory
    {
        get
        {
            if (!IsFolder)
            {
                return Category;
            }
            var best = FileCategory.Other;
            long bestSize = -1;
            for (var i = 0; i < CategorySizes.Length; i++)
            {
                if (CategorySizes[i] > bestSize)
                {
                    bestSize = CategorySizes[i];
                    best = (FileCategory)i;
                }
            }
            return best;
        }
    }

    /// <summary>Creates the root folder of a tree for <paramref name="rootPath"/>.</summary>
    public static SpaceNode CreateRoot(string rootPath) => new(rootPath, null, isFolder: true);

    /// <summary>Adds an (initially empty) sub-folder and counts it on every ancestor.</summary>
    public SpaceNode AddFolder(string name)
    {
        RequireFolder();
        var folder = new SpaceNode(name, this, isFolder: true);
        (_children ??= []).Add(folder);
        for (var node = this; node is not null; node = node.Parent)
        {
            node.FolderCount++;
        }
        return folder;
    }

    /// <summary>
    /// Adds a file and rolls its size, count, category and time up through every ancestor.
    /// A negative size is treated as zero.
    /// </summary>
    public SpaceNode AddFile(string name, long size, DateTime lastWriteUtc)
    {
        RequireFolder();
        var bytes = Math.Max(0, size);
        var category = FileTypeClassifier.Classify(name);
        var file = new SpaceNode(name, this, isFolder: false)
        {
            Category = category,
            Size = bytes,
            LastWriteUtc = lastWriteUtc,
            FileCount = 1
        };
        (_children ??= []).Add(file);

        for (var node = this; node is not null; node = node.Parent)
        {
            node.Size += bytes;
            node.FileCount++;
            node.CategorySizes[(int)category] += bytes;
            if (lastWriteUtc > node.LastWriteUtc)
            {
                node.LastWriteUtc = lastWriteUtc;
            }
        }
        return file;
    }

    /// <summary>Orders every folder's children largest first (ties by name), recursively.</summary>
    public void SortBySize()
    {
        if (_children is null)
        {
            return;
        }
        _children.Sort(static (a, b) =>
        {
            var bySize = b.Size.CompareTo(a.Size);
            return bySize != 0 ? bySize : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        foreach (var child in _children)
        {
            child.SortBySize();
        }
    }

    /// <summary>Every node beneath this one (not including it), depth-first, without recursion.</summary>
    public IEnumerable<SpaceNode> Descendants()
    {
        var stack = new Stack<SpaceNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (var child in node.Children)
            {
                yield return child;
                if (child.IsFolder)
                {
                    stack.Push(child);
                }
            }
        }
    }

    /// <summary>The path of this node relative to <paramref name="ancestor"/> ("" for itself).</summary>
    public string RelativeTo(SpaceNode ancestor)
    {
        var parts = new List<string>();
        for (var node = this; node is not null && node != ancestor; node = node.Parent)
        {
            parts.Add(node.Name);
        }
        parts.Reverse();
        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    private void RequireFolder()
    {
        if (!IsFolder)
        {
            throw new InvalidOperationException("Only a folder can have children.");
        }
    }
}
