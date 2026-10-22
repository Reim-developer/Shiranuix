using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Graphs.Views;
using Shiranuix.Tests.Headless;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The character dialog standing in a headless window: what a writer types
/// stays typed when their attention walks to the next box — the whole of
/// the promise the input-method guard makes, kept here without any input
/// method at all, which is exactly the condition the promise must hold in
/// as well.
/// </summary>
/// <remarks>
/// The composition itself — the half-typed syllable an IME holds and used
/// to take away with it — needs a real IMM32 context and cannot stand in
/// headless; what these tests hold is the ordinary road the guard must not
/// have blocked on its way through.
/// </remarks>
[Collection("Headless UI")]
public sealed class CharacterDialogTests {
    [Fact]
    public void Tabbing_Away_From_The_Name_Keeps_The_Name() =>
        HeadlessUi.On(() => {
            var viewModel = new CharacterDialogViewModel(null, null, null);
            var dialog = new CharacterDialog(viewModel);
            dialog.Show();

            var (name, note) = TheTwoBoxes(dialog);
            name.Focus();
            dialog.KeyTextInput("Mara");
            Tab(dialog);

            Assert.Equal("Mara", viewModel.Name);
            Assert.Equal("Mara", name.Text);
            Assert.True(note.IsFocused);

            dialog.Close();
        });

    [Fact]
    public void Tabbing_Back_To_The_Name_Keeps_The_Note() =>
        HeadlessUi.On(() => {
            var viewModel = new CharacterDialogViewModel(null, null, null);
            var dialog = new CharacterDialog(viewModel);
            dialog.Show();

            var (name, note) = TheTwoBoxes(dialog);
            note.Focus();
            dialog.KeyTextInput("the witch of the fen");
            ShiftTab(dialog);

            Assert.Equal("the witch of the fen", viewModel.Note);
            Assert.Equal("the witch of the fen", note.Text);
            Assert.True(name.IsFocused);

            dialog.Close();
        });

    /// <summary>The two boxes the dialog asks in, in the order it asks them.</summary>
    private static (TextBox Name, TextBox Note) TheTwoBoxes(Window dialog) {
        var boxes = dialog.GetVisualDescendants().OfType<TextBox>().ToList();
        return (boxes[0], boxes[1]);
    }

    /// <summary>The tab key, walked down and up, as the hand would walk it.</summary>
    private static void Tab(TopLevel dialog) {
        dialog.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        dialog.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
    }

    /// <summary>The same key walked with shift, which goes the other way.</summary>
    private static void ShiftTab(TopLevel dialog) {
        dialog.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        dialog.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
    }
}
