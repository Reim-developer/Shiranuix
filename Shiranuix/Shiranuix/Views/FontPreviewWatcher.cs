using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Shiranuix.Views;

/// <summary>
/// Follows the font picker's highlight into a preview card: the card's place
/// and open state first, then the list's own events.
/// </summary>
/// <remarks>
/// The picker's list lives in the combo box's popup — a separate visual
/// root — so pointer and focus over it never reach the window. The card
/// therefore listens to the list itself, wired the first time the picker
/// opens. Nothing here writes a font anywhere: the highlight is browsing,
/// not a choice, and the click remains the only thing that chooses.
///
/// One watch per window, with the little state it needs — whether the list's
/// handlers have been attached — kept beside the handlers that need it, the
/// same way the editor's composition is watched.
/// </remarks>
public static class FontPreviewWatcher {
    /// <summary>
    /// Opens and closes the preview card with the picker's dropdown, and
    /// keeps the sample in the typeface under the pointer or the keyboard
    /// cursor.
    /// </summary>
    /// <param name="picker">The font picker's combo box.</param>
    /// <param name="card">The preview popup, placed over the picker.</param>
    /// <param name="sample">The text drawn in the browsed typeface.</param>
    public static void Watch(ComboBox picker, Popup card, TextBlock sample) {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sample);

        // The card is a sample, never a target: focus must stay with the
        // list so the keyboard can still walk it.
        Popup.SetTakesFocusFromNativeControl(card, false);
        card.PlacementTarget = picker;

        var wired = false;

        picker.DropDownOpened += (_, _) => {
            WireList();
            card.IsOpen = true;
            Show(picker.SelectedItem as string);
        };

        picker.DropDownClosed += (_, _) => card.IsOpen = false;

        // Attaches the preview's listeners to the picker's list — the popup's
        // child, the one element through which every item under the pointer or
        // the keyboard announces itself. Called on every open until the
        // template has been found. Handlers go on the child rather than on
        // the items because items are virtualized and recycled beneath it; the
        // child survives, so the handlers are attached exactly once and
        // bubbling does the rest.
        void WireList() {
            if (wired) {
                return;
            }

            var popup = picker.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
            if (popup?.Child is not { } list) {
                return;
            }

            wired = true;
            list.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
            list.AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        }

        // Draws the sample in the typeface under the mouse — hovering never
        // selects, so the preview is the only thing that follows.
        void OnPointerMoved(object? sender, PointerEventArgs e) {
            if (e.Source is StyledElement { DataContext: string family }) {
                Show(family);
            }
        }

        // Draws the sample in the typeface under the keyboard cursor: the
        // arrows move focus through the list without committing a selection,
        // so focus is the news that matters.
        void OnGotFocus(object? sender, FocusChangedEventArgs e) {
            if (e.Source is StyledElement { DataContext: string family }) {
                Show(family);
            }
        }

        // Sets the sample's typeface — the pangram's own family, which is
        // the one thing the card is there to show. A family that is not
        // installed draws in the same fallback the document would use, so
        // the preview never lies about what the pick would do.
        void Show(string? family) {
            if (!string.IsNullOrWhiteSpace(family)) {
                sample.FontFamily = new FontFamily(family);
            }
        }
    }
}
