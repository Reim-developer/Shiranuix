using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shiranuix.FileTree.Io;
using Shiranuix.FileTree.Nodes;

namespace Shiranuix.FileTree.Views;

/// <summary>
/// Moving a file or folder by dragging it onto another one.
/// </summary>
/// <remarks>
/// <para>
/// The way a file moves in every editor a writer has ever used: it is picked
/// up, carried, and put down on a folder. Nothing is asked before the drag
/// starts - the writer is already saying what they mean - but a drop is only
/// ever offered onto a folder that can hold it: never onto itself, never onto
/// something inside itself, because the disk would allow both and the result
/// would be a tree that contains itself.
/// </para>
/// <para>
/// The tree asks the editor before the move actually happens, so the
/// confirmation reads the same way whether the folder was dragged to or
/// picked from a dialog.
/// </para>
/// </remarks>
public static class TreeDrag {
    /// <summary>
    /// The format this tree's own drags carry, so nothing else can be mistaken
    /// for one.
    /// </summary>
    /// <remarks>
    /// An in-process format: a row is a live object belonging to this tree, and
    /// nothing outside it has any business holding one. Nothing is written to a
    /// file and nothing goes near the system clipboard either - a tree's drag
    /// says "this row", not "this path", because the row is what the writer
    /// pointed at.
    /// </remarks>
    public static DataFormat<IReadOnlyList<FileTreeItem>> Format { get; } =
        DataFormat.CreateInProcessFormat<IReadOnlyList<FileTreeItem>>(
            "Shiranuix.FileTree/rows");

    /// <summary>How far the pointer must travel before a press becomes a drag.</summary>
    private const int Threshold = 4;

    /// <summary>
    /// Whether what is being carried may be put down on a row.
    /// </summary>
    /// <param name="carried">The row that was picked up.</param>
    /// <param name="under">The row it is over.</param>
    /// <returns>True when the drop makes sense.</returns>
    public static bool CanDropOn(FileTreeItem carried, FileTreeItem under) {
        ArgumentNullException.ThrowIfNull(carried);
        ArgumentNullException.ThrowIfNull(under);

        // Only folders hold things; a row is not dropped on itself; a folder is
        // never dropped inside itself; and a row already in that folder has
        // nowhere to go, so the caret is not even shown over its own parent.
        return under.IsDirectory
            && !ReferenceEquals(carried, under)
            && !carried.IsSelfOrAncestorOf(under)
            && !IsAlreadyIn(carried, under);
    }

    /// <summary>
    /// Whether a row already sits in the folder it would be dropped on.
    /// </summary>
    /// <param name="carried">The row that is being carried.</param>
    /// <param name="folder">The folder it is over.</param>
    /// <returns>True when it is already there.</returns>
    private static bool IsAlreadyIn(FileTreeItem carried, FileTreeItem folder) =>
        carried.Node.Parent is { } parent
        && string.Equals(parent.FullPath, folder.FullPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Packs rows into something a drag can carry.
    /// </summary>
    /// <param name="rows">The rows being carried.</param>
    /// <returns>The transfer, ready to hand to the drag.</returns>
    public static DataTransfer Pack(IReadOnlyList<FileTreeItem> rows) {
        ArgumentNullException.ThrowIfNull(rows);

        var item = new DataTransferItem();
        item.Set(Format, rows);

        var transfer = new DataTransfer();
        transfer.Add(item);
        return transfer;
    }

    /// <summary>
    /// The rows a drag is carrying.
    /// </summary>
    /// <param name="data">What the drag carries.</param>
    /// <returns>The rows, or an empty list when it is not one of ours.</returns>
    public static IReadOnlyList<FileTreeItem> Carried(IDataTransfer? data) =>
        data?.TryGetValue(Format) ?? [];

    /// <summary>
    /// Whether the pointer has travelled far enough to be a drag rather than a
    /// click.
    /// </summary>
    /// <param name="start">Where the press began.</param>
    /// <param name="now">Where the pointer is.</param>
    /// <returns>True once it has moved far enough.</returns>
    /// <remarks>
    /// Distance, not movement on both axes: a drag down a list of folders is
    /// almost entirely vertical, and one that has to travel sideways too would
    /// never begin.
    /// </remarks>
    public static bool HasTravelled(Point start, Point now) {
        var across = now.X - start.X;
        var down = now.Y - start.Y;

        return (across * across) + (down * down) >= Threshold * Threshold;
    }

    /// <summary>
    /// Asks the tree's host to move one row into a folder, and says what happened.
    /// </summary>
    /// <param name="host">The host that owns the tree.</param>
    /// <param name="carried">What is being moved.</param>
    /// <param name="onto">The folder it was put on.</param>
    /// <returns>A task that finishes once the disk has been told.</returns>
    public static Task MoveAsync(FileTreeHost host, FileTreeItem carried, FileTreeItem onto) {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(carried);
        ArgumentNullException.ThrowIfNull(onto);

        return host.MoveAsync(carried.Node, onto.FullPath, CancellationToken.None);
    }
}
