using Avalonia.Input;
using Shiranuix.Models;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

// Avalonia.Input has a KeyBinding of its own — this is the row that lives
// in the store.
using KeyBinding = Shiranuix.Models.KeyBinding;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="KeyBindingState"/>: the defaults, the writer's
/// moves over them, the rule that one chord has one answer, and what the
/// store is told.
/// </summary>
/// <remarks>
/// The store is faked, so these run without a database. What is worth
/// checking here is the bookkeeping a writer never sees directly: that a
/// chord taken from one command arrives unassigned on the row that held
/// it, and that only the disagreements with the defaults reach the store.
/// </remarks>
public sealed class KeyBindingStateTests {
    private readonly FakeKeyBindingsStore _store = new();

    private KeyBindingState CreateState() => new(_store);

    [Fact]
    public void Every_Command_Starts_On_Its_Default_Chord() {
        var state = CreateState();

        Assert.Equal(8, state.Rows.Count);
        Assert.Equal("Ctrl+S", state.GestureOf("Save"));
        Assert.Equal("Ctrl+Shift+S", state.GestureOf("SaveAs"));
        Assert.Equal("Ctrl+F", state.GestureOf("Find"));
        Assert.Equal("Alt+Z", state.GestureOf("ToggleWordWrap"));
        Assert.All(state.Rows, row => Assert.True(row.IsAssigned));
    }

    [Fact]
    public void The_Writer_s_Moves_Override_The_Defaults() {
        _store.Seeded.Add(new KeyBinding { Command = "Redo", Gesture = "Ctrl+Shift+Z" });

        var state = CreateState();

        Assert.Equal("Ctrl+Shift+Z", state.GestureOf("Redo"));
        Assert.Equal("Ctrl+Shift+Z", state.Rows.Single(row => row.CommandId == "Redo").Gesture);
    }

    [Fact]
    public void Moves_From_A_Later_Build_Are_Left_Alone() {
        // A command this build does not know was written by one with more
        // of them. Not answered here, not corrected — the file says what
        // some version meant.
        _store.Seeded.Add(new KeyBinding { Command = "FutureCommand", Gesture = "Ctrl+F12" });

        var state = CreateState();

        Assert.Equal(8, state.Rows.Count);
        Assert.Null(state.CommandFor(Key.F12, KeyModifiers.Control));
    }

    [Fact]
    public void A_Chord_Moves_Off_One_Command_On_To_Another() {
        var state = CreateState();

        // Ctrl+O is Open's. Giving it to Save must leave Open with nothing.
        var taken = state.Assign("Save", Key.O, KeyModifiers.Control);

        Assert.True(taken);
        Assert.Equal("Ctrl+O", state.GestureOf("Save"));
        Assert.Equal(string.Empty, state.GestureOf("Open"));
        Assert.Equal("Save", state.CommandFor(Key.O, KeyModifiers.Control));
        Assert.Null(state.CommandFor(Key.S, KeyModifiers.Control));

        var openRow = state.Rows.Single(row => row.CommandId == "Open");
        Assert.False(openRow.IsAssigned);
        Assert.Equal("Not assigned", openRow.ChipText);

        // The chip asks while a chord is being pressed for, and the ask is
        // what replaced the chord being gone with — one text, said once.
        openRow.IsCapturing = true;
        Assert.Equal("Press a chord…", openRow.ChipText);
    }

    [Fact]
    public void A_Press_With_No_Modifier_Changes_Nothing() {
        var state = CreateState();

        var taken = state.Assign("Save", Key.S, KeyModifiers.None);

        Assert.False(taken);
        Assert.Equal("Ctrl+S", state.GestureOf("Save"));
        Assert.Null(_store.Written);
    }

    [Fact]
    public void A_Modifier_On_Its_Own_Changes_Nothing() {
        var state = CreateState();

        Assert.False(state.Assign("Save", Key.LeftCtrl, KeyModifiers.Control));
        Assert.Equal("Ctrl+S", state.GestureOf("Save"));
    }

    [Fact]
    public void An_Id_Nobody_Answers_To_Changes_Nothing() {
        var state = CreateState();

        Assert.False(state.Assign("FutureCommand", Key.K, KeyModifiers.Control));
        Assert.Null(_store.Written);
    }

    [Fact]
    public void Moving_A_Binding_Writes_Only_The_Disagreements() {
        var state = CreateState();

        state.Assign("Save", Key.K, KeyModifiers.Control);

        var written = Assert.Single(_store.Written!);
        Assert.Equal("Save", written.Command);
        Assert.Equal("Ctrl+K", written.Gesture);

        state.Assign("Redo", Key.Z, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Equal(2, _store.Written!.Count);
        Assert.Contains(_store.Written, binding =>
            binding.Command == "Redo" && binding.Gesture == "Ctrl+Shift+Z");
    }

    [Fact]
    public void Clearing_A_Chord_Keeps_The_Clearing() {
        var state = CreateState();

        state.Clear("Save");

        Assert.Equal(string.Empty, state.GestureOf("Save"));
        Assert.Null(state.CommandFor(Key.S, KeyModifiers.Control));

        // The clearing is a disagreement too, and rides the store the same
        // way a move does — an empty gesture, not a missing row.
        var written = Assert.Single(_store.Written!);
        Assert.Equal("Save", written.Command);
        Assert.Equal(string.Empty, written.Gesture);
    }

    [Fact]
    public void Reset_Puts_Every_Command_Back_And_Empties_The_Store() {
        _store.Seeded.Add(new KeyBinding { Command = "Redo", Gesture = "Ctrl+Shift+Z" });
        var state = CreateState();
        state.Assign("Save", Key.K, KeyModifiers.Control);

        state.ResetToDefaults();

        Assert.Equal("Ctrl+Y", state.GestureOf("Redo"));
        Assert.Equal("Ctrl+S", state.GestureOf("Save"));
        Assert.Null(state.CommandFor(Key.K, KeyModifiers.Control));
        Assert.Empty(_store.Written!);
        Assert.All(state.Rows, row => Assert.True(row.IsAssigned));
    }

    [Fact]
    public void Routing_Finds_The_Command_A_Chord_Belongs_To() {
        var state = CreateState();

        Assert.Equal("Save", state.CommandFor(Key.S, KeyModifiers.Control));
        Assert.Equal("SaveAs", state.CommandFor(Key.S, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Null(state.CommandFor(Key.Q, KeyModifiers.Control));
    }
}
