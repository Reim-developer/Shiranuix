using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;

namespace Shiranuix.FileTree.Views;

/// <summary>
/// The naming half of the file tree's view: how a row is told its new name,
/// and how a row that is not on the disk yet is given one.
/// </summary>
/// <remarks>
/// Naming is its own story. A name is typed while the tree itself is in use
/// around the typing, given up with Escape, and becomes a file or folder when
/// it is accepted; keeping those threads apart from the tree's own pressing
/// and stepping is what keeps both readable.
/// </remarks>
public partial class FileTreeView {
    /// <summary>
    /// Whether the name editor closed by Escape: the words typed then are not
    /// a request, and the blur that follows the closing must not act as one.
    /// </summary>
    private bool _theNameLeftAsItWas;

    /// <summary>
    /// A key in the row's name editor: Enter closes it, and Escape closes it
    /// saying nothing - the words typed before Escape are not a request.
    /// </summary>
    /// <param name="sender">The editor in the row.</param>
    /// <param name="e">The key.</param>
    /// <remarks>
    /// The row is taken from the editor's own data rather than the tree's
    /// selection: focus may have been on a row whose selection the tree
    /// never changed, and a name belongs to the row it was typed in.
    /// </remarks>
    private void OnRenameKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key is not (Key.Escape or Key.Enter)) {
            return;
        }

        var editor = sender as TextBox;

        if (TreeState is not { } tree
            || editor is null
            || editor.DataContext is not FileTreeItem row) {
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape) {
            _theNameLeftAsItWas = true;

            if (row.IsDraft) {
                // Nothing was made, so there is nothing to undo: the row
                // simply leaves the tree.
                tree.DiscardDraft(row);
            } else {
                row.IsEditing = false;
            }

            return;
        }

        if (row.IsDraft) {
            // Enter gives the name: the file or folder is made, and a refused
            // name finds the row still open, waiting for the next try.
            row.IsEditing = false;
            _ = CommitDraftAsync(tree, row, editor.Text);
        }
    }

    /// <summary>
    /// The writer finished typing a name: the row the name belongs to is
    /// found from the editor, not from wherever the selection has wandered.
    /// A row that is already on the disk is told its new name; a draft gives
    /// up its name quietly or becomes the file or folder itself.
    /// </summary>
    /// <param name="sender">The editor in the row.</param>
    /// <param name="e">The event (unused).</param>
    private void OnRenameFinished(object? sender, RoutedEventArgs e) {
        if (_theNameLeftAsItWas) {
            _theNameLeftAsItWas = false;
            return;
        }

        var editor = sender as TextBox;

        if (TreeState is not { } tree
            || editor is null
            || editor.DataContext is not FileTreeItem row) {
            return;
        }

        row.IsEditing = false;
        var wanted = editor.Text?.Trim() ?? string.Empty;

        if (row.IsDraft) {
            if (string.IsNullOrWhiteSpace(wanted)) {
                tree.DiscardDraft(row);
                return;
            }

            _ = CommitDraftAsync(tree, row, wanted);
            return;
        }

        if (wanted.Length > 0
            && !string.Equals(wanted, row.Name, StringComparison.Ordinal)) {
            _ = _host.RenameAsync(row.Node, wanted, CancellationToken.None);
        }
    }

    /// <summary>
    /// Sends the name a draft was given to the host, and keeps the row open
    /// for another try when the disk refuses it.
    /// </summary>
    /// <param name="tree">The tree the draft stands in.</param>
    /// <param name="draft">The row waiting for its name.</param>
    /// <param name="wanted">The name as it was typed.</param>
    private async Task CommitDraftAsync(FileTreeState tree, FileTreeItem draft, string? wanted) {
        if (tree.Draft != draft || string.IsNullOrWhiteSpace(wanted)) {
            return;
        }

        var outcome = await _host.FinishCreateAsync(draft.Node, wanted.Trim(), CancellationToken.None);

        if (outcome.IsSuccess || tree.Draft != draft) {
            return;
        }

        tree.Status = outcome.Error ?? "That did not go.";
        draft.IsEditing = true;
        CareForTheNameEditor(draft);
    }

    /// <summary>
    /// Runs the command that makes room for a name, and puts the writer where
    /// the typing goes.
    /// </summary>
    /// <param name="command">The tree command that begins a name.</param>
    private async void BeginANewName(string command) {
        await RunAsync(command);

        if (TreeState?.Draft is { } drafted) {
            OpenTheNameEditor(drafted);
        }
    }

    /// <summary>
    /// Opens a row's name editor, and puts the writer in it: focused, with
    /// the name selected, so typing replaces it instead of joining it.
    /// </summary>
    /// <param name="row">The row being named.</param>
    private void OpenTheNameEditor(FileTreeItem row) {
        row.IsEditing = true;

        // The editor is inside the row only once the layout has realised the
        // row, which is at least one pass away from here.
        Dispatcher.UIThread.Post(
            () => CareForTheNameEditor(row),
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Finds the row's editor among the rows that are on show, gives it the
    /// focus and selects the name in it.
    /// </summary>
    /// <param name="row">The row whose editor is wanted.</param>
    private void CareForTheNameEditor(FileTreeItem row) {
        var editor = Rows.GetSelfAndVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, row));

        if (editor is null) {
            return;
        }

        editor.Focus();
        editor.SelectAll();
    }
}
