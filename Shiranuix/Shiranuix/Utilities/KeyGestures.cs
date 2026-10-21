using Avalonia.Input;

namespace Shiranuix.Utilities;

/// <summary>
/// The keyboard shortcut's written shape: how a chord becomes text, how the
/// text is read back, and the one rule every chord the application answers
/// to obeys.
/// </summary>
/// <remarks>
/// A chord is written as its parts joined by '+', the modifiers first in
/// the order Control, Alt, Shift, and the key last — "Ctrl+Shift+S". The
/// modifier rule belongs to the format itself rather than to its callers:
/// a chord without Ctrl or Alt would fire while the writer is typing, and
/// a writing tool must never save a document because someone typed an "s".
///
/// A key event can carry more than the three modifiers — the Win key has a
/// say of its own, and nothing here is interested in it — so everything
/// outside Ctrl, Alt and Shift is dropped before a press is compared. The
/// three that are left are the whole vocabulary.
/// </remarks>
public static class KeyGestures {
    /// <summary>
    /// The three modifiers a chord is written in, in this order — which is
    /// what makes the writing canonical: one chord, one spelling.
    /// </summary>
    private static readonly (string Name, KeyModifiers Flag)[] Modifiers = {
        ("Ctrl", KeyModifiers.Control),
        ("Alt", KeyModifiers.Alt),
        ("Shift", KeyModifiers.Shift)
    };

    /// <summary>The three modifiers as a mask: everything else a press carries is noise.</summary>
    private const KeyModifiers Meaningful =
        KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

    /// <summary>
    /// The keys that are modifiers rather than presses: a chord may name
    /// them, but pressing one is not a chord of its own.
    /// </summary>
    private static readonly Key[] ModifierKeys = {
        Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
        Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin
    };

    /// <summary>
    /// Whether the modifiers carry a Ctrl or an Alt — the rule a press
    /// obeys before it may count as a chord.
    /// </summary>
    /// <param name="modifiers">The modifiers of the press.</param>
    /// <returns>True when a chord could be built from them.</returns>
    public static bool HasModifier(KeyModifiers modifiers) =>
        (modifiers & Meaningful & (KeyModifiers.Control | KeyModifiers.Alt)) != 0;

    /// <summary>
    /// The modifiers as they count for a chord: the three, nothing else.
    /// </summary>
    /// <param name="modifiers">The modifiers of the press.</param>
    /// <returns>The press with the noise dropped.</returns>
    public static KeyModifiers Normalize(KeyModifiers modifiers) => modifiers & Meaningful;

    /// <summary>
    /// Writes a press in canonical form — "Ctrl+Shift+S" — or as nothing
    /// when there is no chord to write: no Ctrl or Alt rides along, or the
    /// "key" is one of the modifiers standing alone.
    /// </summary>
    /// <param name="key">The key of the press.</param>
    /// <param name="modifiers">The modifiers of the press.</param>
    /// <returns>The chord, or the empty string when the press is not one.</returns>
    public static string Format(Key key, KeyModifiers modifiers) {
        if (!HasModifier(modifiers) || ModifierKeys.Contains(key)) {
            return string.Empty;
        }

        var parts = new List<string>(4);
        foreach (var (name, flag) in Modifiers) {
            if ((modifiers & Meaningful & flag) != 0) {
                parts.Add(name);
            }
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>
    /// Reads a chord back — the other half of the round trip the store and
    /// the settings window each take. False when the text is not a chord:
    /// an unknown part, a key named by digits, or no Ctrl/Alt to speak of.
    /// </summary>
    /// <param name="text">The chord's written shape, modifiers in canonical order.</param>
    /// <param name="key">The chord's key, when it has one.</param>
    /// <param name="modifiers">The chord's modifiers, when it has them.</param>
    /// <returns>True when the text was a chord.</returns>
    public static bool TryParse(string? text, out Key key, out KeyModifiers modifiers) {
        key = default;
        modifiers = KeyModifiers.None;

        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        var parts = text.Split('+');
        if (parts.Length < 2) {
            // A lone key is not a chord — that is the whole modifier rule,
            // arriving as the format's shape.
            return false;
        }

        foreach (var part in parts[..^1]) {
            var found = false;
            foreach (var (name, flag) in Modifiers) {
                if (string.Equals(part, name, StringComparison.OrdinalIgnoreCase)) {
                    // The same modifier twice is not a stronger chord, just
                    // a spelling nobody wrote on purpose.
                    modifiers |= flag;
                    found = true;
                    break;
                }
            }

            if (!found) {
                return false;
            }
        }

        var keyName = parts[^1].Trim();
        if (keyName.Length == 0 || char.IsDigit(keyName[0])
            || !Enum.TryParse(keyName, ignoreCase: true, out key)) {
            return false;
        }

        // Enum.TryParse also reads a number as a cast — "Ctrl+999" would be
        // a Key nobody named. Only names count.
        if (!Enum.IsDefined(key)) {
            return false;
        }

        return HasModifier(modifiers);
    }
}
