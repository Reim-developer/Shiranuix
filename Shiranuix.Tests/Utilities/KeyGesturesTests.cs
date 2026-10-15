using Avalonia.Input;
using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for <see cref="KeyGestures"/>: the shortcut's written shape —
/// what one is, what one is not, and that both halves of the round trip
/// agree.
/// </summary>
/// <remarks>
/// The rule these tests guard more than any other: no Ctrl or Alt, no
/// chord. A writing tool that answered to a bare letter would save
/// mid-sentence.
/// </remarks>
public sealed class KeyGesturesTests {
    [Fact]
    public void The_Modifiers_Are_Written_In_Canonical_Order() {
        var chord = KeyGestures.Format(Key.S, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Equal("Ctrl+Shift+S", chord);
    }

    [Fact]
    public void Alt_Rides_Alone_With_Shift() {
        var chord = KeyGestures.Format(Key.Z, KeyModifiers.Alt | KeyModifiers.Shift);

        Assert.Equal("Alt+Shift+Z", chord);
    }

    [Fact]
    public void A_Press_With_No_Ctrl_Or_Alt_Is_Not_A_Chord() {
        Assert.Equal(string.Empty, KeyGestures.Format(Key.S, KeyModifiers.None));
        Assert.Equal(string.Empty, KeyGestures.Format(Key.S, KeyModifiers.Shift));
    }

    [Fact]
    public void A_Modifier_Pressed_On_Its_Own_Is_Not_A_Chord() {
        Assert.Equal(string.Empty, KeyGestures.Format(Key.LeftCtrl, KeyModifiers.Control));
        Assert.Equal(string.Empty, KeyGestures.Format(Key.RightAlt, KeyModifiers.Alt));
    }

    [Fact]
    public void A_Chord_Round_Trips() {
        var parsed = KeyGestures.TryParse("Ctrl+Shift+S", out var key, out var modifiers);

        Assert.True(parsed);
        Assert.Equal(Key.S, key);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, modifiers);
        Assert.Equal("Ctrl+Shift+S", KeyGestures.Format(key, modifiers));
    }

    [Fact]
    public void Modifiers_Are_Read_Without_Case() {
        var parsed = KeyGestures.TryParse("alt+z", out var key, out var modifiers);

        Assert.True(parsed);
        Assert.Equal(Key.Z, key);
        Assert.Equal(KeyModifiers.Alt, modifiers);
        Assert.Equal("Alt+Z", KeyGestures.Format(key, modifiers));
    }

    [Fact]
    public void A_Lone_Key_Is_Not_A_Chord() {
        Assert.False(KeyGestures.TryParse("S", out _, out _));
        Assert.False(KeyGestures.TryParse("", out _, out _));
    }

    [Fact]
    public void A_Chord_With_No_Ctrl_Or_Alt_Is_Not_A_Chord() {
        Assert.False(KeyGestures.TryParse("Shift+S", out _, out _));
    }

    [Fact]
    public void Parts_Nobody_Named_Are_Not_A_Chord() {
        Assert.False(KeyGestures.TryParse("Ctrl+Bogus", out _, out _));

        // Enum.TryParse also reads a number cast to the enum — a chord
        // spelled with digits is a key nobody named, and must not be one.
        Assert.False(KeyGestures.TryParse("Ctrl+999", out _, out _));
    }

    [Fact]
    public void The_Win_Key_Does_Not_Reach_A_Chord() {
        var clean = KeyGestures.Normalize(KeyModifiers.Control | KeyModifiers.Meta);

        Assert.Equal(KeyModifiers.Control, clean);
        Assert.False(KeyGestures.HasModifier(KeyModifiers.Meta));
    }
}
