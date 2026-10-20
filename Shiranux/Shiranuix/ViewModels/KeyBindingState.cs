using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;
using Shiranuix.Persistence;
using Shiranuix.Utilities;

// Avalonia.Input has a KeyBinding of its own — this is the row that lives
// in the store.
using KeyBinding = Shiranuix.Models.KeyBinding;

namespace Shiranuix.ViewModels;

/// <summary>
/// What each command answers to on the keyboard: the defaults as code
/// says them, the writer's moves over them, and the chords the window
/// actually acts on.
/// </summary>
/// <remarks>
/// The defaults come from <see cref="KeyCommands"/> and the moves from
/// the store — only the disagreements, so a command added in a later
/// build arrives already working. Every move lands on three things at
/// once: this state's answer, the row the settings draw, and the store's
/// set of overrides. When two commands would answer to the same chord the
/// later one takes it and the earlier becomes unassigned, because one
/// chord with two answers is a shortcut the writer cannot predict.
///
/// Unassigning is an ordinary move: the toolbar still offers the command,
/// and the row says so plainly. The parsed chords are kept beside the
/// written ones and rebuilt after every change — routing asks them on
/// every press, and a press must not have to parse anything.
///
/// The rows are made once and live as long as the window; what changes is
/// what a row says, and the row rings for itself.
/// </remarks>
public sealed partial class KeyBindingState : ObservableObject {
    /// <summary>The chord each command answers to, canonical — "" when it answers to none.</summary>
    private readonly Dictionary<string, string> _gestures = new();

    /// <summary>Where the writer's moves live.</summary>
    private readonly IKeyBindingsStore _store;

    /// <summary>The settings' rows, by command id.</summary>
    private readonly Dictionary<string, KeyBindingRow> _rows = new();

    /// <summary>The rows in catalogue order, handed to the settings once.</summary>
    private readonly IReadOnlyList<KeyBindingRow> _rowList;

    /// <summary>
    /// The chords as routing reads them — parsed once per change rather
    /// than per press.
    /// </summary>
    private List<(string Command, Key Key, KeyModifiers Modifiers)> _parsed = new();

    /// <summary>
    /// Builds the state over the writer's stored moves: the defaults first,
    /// then whatever the store says has been moved.
    /// </summary>
    /// <param name="store">Where the overrides live.</param>
    public KeyBindingState(IKeyBindingsStore store) {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        foreach (var command in KeyCommands.All) {
            _gestures[command.Id] = command.DefaultGesture ?? string.Empty;
        }

        foreach (var stored in _store.LoadAll()) {
            // An id this build does not know was written by one with more
            // commands — not answered here, and not corrected either: the
            // file says what some version meant, and guessing at it would
            // be worse than leaving the words alone.
            if (KeyCommands.Find(stored.Command) is { } command
                && (stored.Gesture.Length == 0 || KeyGestures.TryParse(stored.Gesture, out _, out _))) {
                _gestures[command.Id] = stored.Gesture;
            }
        }

        foreach (var command in KeyCommands.All) {
            var row = new KeyBindingRow(command, _gestures[command.Id]);
            _rows[command.Id] = row;
        }

        _rowList = KeyCommands.All.Select(command => _rows[command.Id]).ToArray();
        Rebuild();
    }

    /// <summary>The seven rows, in catalogue order, for the settings window.</summary>
    public IReadOnlyList<KeyBindingRow> Rows => _rowList;

    /// <summary>
    /// The chord a command answers to, canonical — or the empty string
    /// when it answers to none.
    /// </summary>
    /// <param name="commandId">The command's id.</param>
    /// <returns>The chord, or the empty string.</returns>
    public string GestureOf(string commandId) =>
        _gestures.TryGetValue(commandId, out var gesture) ? gesture : string.Empty;

    /// <summary>
    /// The command a press answers to, or null: no binding claims the
    /// chord, and the window lets the press go on its way.
    /// </summary>
    /// <param name="key">The key of the press.</param>
    /// <param name="modifiers">The modifiers of the press.</param>
    /// <returns>The command's id, or null.</returns>
    public string? CommandFor(Key key, KeyModifiers modifiers) {
        var clean = KeyGestures.Normalize(modifiers);
        foreach (var (command, boundKey, boundModifiers) in _parsed) {
            if (boundKey == key && boundModifiers == clean) {
                return command;
            }
        }

        return null;
    }

    /// <summary>
    /// Gives a chord to a command. The press must be a chord — a Ctrl or
    /// Alt rides along — and any other command holding it loses it, so one
    /// chord never has two answers.
    /// </summary>
    /// <param name="commandId">The command to give it to.</param>
    /// <param name="key">The key of the press.</param>
    /// <param name="modifiers">The modifiers of the press.</param>
    /// <returns>False when the press is no chord at all: nothing was changed.</returns>
    public bool Assign(string commandId, Key key, KeyModifiers modifiers) {
        if (KeyCommands.Find(commandId) is not { } command) {
            return false;
        }

        var gesture = KeyGestures.Format(key, modifiers);
        if (gesture.Length == 0) {
            return false;
        }

        // Last press wins. The chord is spoken for, and the command that
        // held it must not hold it still.
        foreach (var other in KeyCommands.All) {
            if (other.Id != command.Id && _gestures[other.Id] == gesture) {
                Set(other.Id, string.Empty);
            }
        }

        Set(command.Id, gesture);
        Save();
        return true;
    }

    /// <summary>
    /// Takes a command's chord away entirely. The command stands: the
    /// toolbar still offers it, and the settings say plainly that it
    /// answers to nothing.
    /// </summary>
    /// <param name="commandId">The command to unbind.</param>
    public void Clear(string commandId) {
        if (KeyCommands.Find(commandId) is not { } command || _gestures[command.Id].Length == 0) {
            return;
        }

        Set(command.Id, string.Empty);
        Save();
    }

    /// <summary>
    /// Puts every command back on the chord it started with — which is the
    /// whole override set going.
    /// </summary>
    public void ResetToDefaults() {
        foreach (var command in KeyCommands.All) {
            Set(command.Id, command.DefaultGesture ?? string.Empty);
        }

        _store.ReplaceAll(Array.Empty<KeyBinding>());
        Rebuild();
    }

    /// <summary>Moves one command, and rings its row for it.</summary>
    private void Set(string commandId, string gesture) {
        if (_gestures[commandId] == gesture) {
            return;
        }

        _gestures[commandId] = gesture;
        _rows[commandId].Gesture = gesture;
    }

    /// <summary>
    /// Writes the disagreements with the defaults as the whole override
    /// set, and re-parses the chords routing will read.
    /// </summary>
    private void Save() {
        var overrides = new List<KeyBinding>();
        foreach (var command in KeyCommands.All) {
            var gesture = _gestures[command.Id];
            if (gesture != (command.DefaultGesture ?? string.Empty)) {
                overrides.Add(new KeyBinding { Command = command.Id, Gesture = gesture });
            }
        }

        _store.ReplaceAll(overrides);
        Rebuild();
    }

    /// <summary>
    /// Parses every assigned chord once. A chord that will not parse is a
    /// default written wrong by hand — the load already set aside stored
    /// words that make no sense — so it throws rather than quietly
    /// unbinding something the file promised.
    /// </summary>
    private void Rebuild() {
        var parsed = new List<(string, Key, KeyModifiers)>();
        foreach (var command in KeyCommands.All) {
            var gesture = _gestures[command.Id];
            if (gesture.Length == 0) {
                continue;
            }

            if (!KeyGestures.TryParse(gesture, out var key, out var modifiers)) {
                throw new InvalidOperationException(
                    $"The default chord '{gesture}' for '{command.Id}' is not a chord.");
            }

            parsed.Add((command.Id, key, modifiers));
        }

        _parsed = parsed;
    }
}
