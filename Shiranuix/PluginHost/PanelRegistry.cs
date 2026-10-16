using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Plugins;
using Shiranuix.Plugins.UI;

namespace Shiranuix.PluginHost;

/// <summary>
/// The panels the window is showing, and whether the column they sit in is
/// open at all.
/// </summary>
/// <remarks>
/// <para>
/// The whole of the editor's side of panels, and none of it needs a window: the
/// registry is a list and a flag, and the view binds to it. That is deliberate
/// — a panel is somebody else's code with a real control in it, and the part
/// of the editor that decides what is shown must be the part that can be tested
/// without drawing anything.
/// </para>
/// <para>
/// First panel in, first shown: opening the window with two plugins to choose
/// between should show one of them rather than an empty column. First id wins
/// as everywhere else, and the loser is told so rather than silently dropped.
/// </para>
/// </remarks>
public sealed partial class PanelRegistry : ObservableObject, IPluginPanelHost {
    /// <summary>
    /// The panels on show, in the order they arrived.
    /// </summary>
    public ObservableCollection<IPluginPanel> Panels { get; } = [];

    /// <summary>The panel the column is showing, or null when it is empty.</summary>
    [ObservableProperty]
    private IPluginPanel? _selected;

    /// <summary>Whether the column is open.</summary>
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>How many panels are on show.</summary>
    public int Count => Panels.Count;

    /// <inheritdoc />
    public PluginOutcome Show(IPluginPanel panel) {
        ArgumentNullException.ThrowIfNull(panel);

        if (string.IsNullOrWhiteSpace(panel.Id)) {
            return PluginOutcome.Failure("A panel that does not say who it is is not a panel.");
        }

        if (Panels.Any(shown => string.Equals(shown.Id, panel.Id, StringComparison.Ordinal))) {
            return PluginOutcome.Failure($"A panel is already on show as '{panel.Id}'.");
        }

        Panels.Add(panel);

        // The first panel in is the panel shown: a window that opened with two
        // plugins to choose between should show one of them.
        Selected ??= panel;
        IsOpen = true;

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarFloor));
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Hide(string id) {
        if (string.IsNullOrWhiteSpace(id)) {
            return PluginOutcome.Failure("A panel's id is what is taken out by.");
        }

        var panel = Panels.FirstOrDefault(shown =>
            string.Equals(shown.Id, id, StringComparison.Ordinal));

        if (panel is null) {
            return PluginOutcome.Failure($"No panel is on show as '{id}'.");
        }

        Panels.Remove(panel);

        if (ReferenceEquals(Selected, panel)) {
            Selected = Panels.FirstOrDefault();
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarFloor));
        return PluginOutcome.Ok();
    }

    /// <summary>
    /// Opens or closes the column, the way a toolbar button and a chord both
    /// ask for.
    /// </summary>
    public void Toggle() {
        if (Panels.Count > 0) {
            IsOpen = !IsOpen;
        }
    }

    partial void OnSelectedChanged(IPluginPanel? value) => IsOpen = Panels.Count > 0;

    partial void OnIsOpenChanged(bool value) {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarFloor));
    }

    /// <summary>
    /// Whether the column has something to show at all — no panels means
    /// nothing to open.
    /// </summary>
    public bool IsVisible => IsOpen && Panels.Count > 0;

    /// <summary>
    /// How wide the column starts, and how wide it goes back to.
    /// </summary>
    public double SidebarWidth { get; private set; } = Default;

    /// <summary>The narrowest the column may be dragged.</summary>
    public double SidebarFloor => IsVisible ? Narrowest : 0;

    /// <summary>The widest the column may be dragged.</summary>
    public double SidebarCeiling => Widest;

    /// <summary>
    /// Puts the column back to the width it started at.
    /// </summary>
    /// <remarks>
    /// The splitter drags the column's own definition, which is what keeps it
    /// inside its bounds; this is the way back, and the double press on the
    /// splitter every editor's side bar answers to. Going through here rather
    /// than writing the width straight into the column means the number a
    /// writer can see and the number the window uses are the same one.
    /// </remarks>
    public void ResetSidebarWidth() {
        SidebarWidth = Default;
        OnPropertyChanged(nameof(SidebarWidth));
    }

    /// <summary>The width a fresh window opens at.</summary>
    public const double Default = 240;

    /// <summary>Below this a name cannot be read, so this is the floor.</summary>
    public const double Narrowest = 180;

    /// <summary>Above this a row is one long sentence, so this is the ceiling.</summary>
    public const double Widest = 360;
}
