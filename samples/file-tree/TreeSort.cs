namespace FileTree;

/// <summary>
/// The orders a tree's rows can be in.
/// </summary>
/// <remarks>
/// Three, because three are the ones a writer actually reaches for, and each
/// of them can run either way. Folders stay above files in every one: a writer
/// looking for a file should never have to look past a wall of folders to find
/// it, even in a tree sorted by date.
/// </remarks>
public enum TreeSort {
    /// <summary>By name, as it reads.</summary>
    Name,

    /// <summary>By what it is, then by name.</summary>
    Kind,

    /// <summary>By when the disk last saw it change.</summary>
    Date
}
