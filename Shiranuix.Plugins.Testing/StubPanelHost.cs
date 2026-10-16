using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Services;
using Shiranuix.Plugins.UI;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// The panel column, as a plugin finds it in a test: a list, and a way to see
/// what was put in it.
/// </summary>
public sealed class StubPanelHost : IPluginPanelHost {
    /// <summary>The panels on show, in the order they arrived.</summary>
    public List<IPluginPanel> Shown { get; } = [];

    /// <summary>Every refusal this door handed out, in order.</summary>
    public List<PluginOutcome> Refusals { get; } = [];

    /// <inheritdoc />
    public PluginOutcome Show(IPluginPanel panel) {
        if (string.IsNullOrWhiteSpace(panel.Id)) {
            return Refuse(PluginOutcome.Failure("A panel that does not say who it is is not a panel."));
        }

        if (Shown.Any(shown => string.Equals(shown.Id, panel.Id, StringComparison.Ordinal))) {
            return Refuse(PluginOutcome.Failure($"A panel is already on show as '{panel.Id}'."));
        }

        Shown.Add(panel);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Hide(string id) {
        var panel = Shown.FirstOrDefault(shown =>
            string.Equals(shown.Id, id, StringComparison.Ordinal));

        return panel is null
            ? Refuse(PluginOutcome.Failure($"No panel is on show as '{id}'."))
            : OutcomeOf(Shown.Remove(panel));
    }

    private PluginOutcome OutcomeOf(bool taken) =>
        taken ? PluginOutcome.Ok() : PluginOutcome.Failure("That panel would not go.");

    private PluginOutcome Refuse(PluginOutcome outcome) {
        Refusals.Add(outcome);
        return outcome;
    }
}
