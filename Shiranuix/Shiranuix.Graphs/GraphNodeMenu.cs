using Avalonia.Controls;

namespace Shiranuix.Graphs;

/// <summary>
/// The small vocabulary a node has, asked at the place the hand pointed:
/// to join it to somebody, to change what it says about itself, or to take
/// it away whole.
/// </summary>
public static class GraphNodeMenu {
    /// <summary>
    /// Opens the menu over the glass, wherever the hand asked the question.
    /// </summary>
    /// <param name="host">The glass the menu opens over.</param>
    /// <param name="state">The state the menu's answers go through.</param>
    /// <param name="node">The node the hand asked about.</param>
    public static void Show(Control host, GraphState state, GraphNodeViewModel node) {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);

        var connect = new MenuItem {
            Header = "Connect from here\u2026",
            IsEnabled = state.Nodes.Count >= 2
        };
        connect.Click += (_, _) => _ = state.ConnectFromAsync(node);

        var edit = new MenuItem { Header = "Character information\u2026" };
        edit.Click += (_, _) => {
            state.SelectNode(node);
            _ = state.EditCommand.ExecuteAsync(null);
        };

        var erase = new MenuItem { Header = "Delete" };
        erase.Click += (_, _) => {
            state.SelectNode(node);
            _ = state.DeleteSelectionCommand.ExecuteAsync(null);
        };

        var menu = new ContextMenu { ItemsSource = new object[] { connect, edit, erase } };
        menu.Open(host);
    }
}
