using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Shiranuix.Views;

/// <summary>
/// The frame a window draws for itself: a border that reads the window it
/// stands in and carries what it reads as its own pseudo-classes.
/// </summary>
/// <remarks>
/// <para>
/// The edge's light answers the window's own states — brighter the while the
/// window holds the eye, strokeless and square the while the window fills the
/// room — and a pseudo-class may only be worn by the control that carries it,
/// so the frame does the reading itself instead of being told from outside.
/// </para>
/// </remarks>
[PseudoClasses(":active", ":maximized")]
public sealed class ChromeFrame : Border {

    /// <summary>The window whose states this frame reads, once attached.</summary>
    private Window? _owner;

    /// <summary>
    /// Begins reading the window the frame arrived beside: the states follow
    /// the window's for the whole time they stand together, and the frame's
    /// first answer is spoken here because nothing has read for it before.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);

        if (TopLevel.GetTopLevel(this) is Window window && !ReferenceEquals(_owner, window)) {
            if (_owner is not null) {
                _owner.PropertyChanged -= OnOwnerChanged;
            }

            _owner = window;
            window.PropertyChanged += OnOwnerChanged;
        }

        Retell();
    }

    /// <summary>
    /// Lets the reading go: the window outliving the frame is of no one's
    /// business, but the frame outliving a window is a leak.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        if (_owner is not null) {
            _owner.PropertyChanged -= OnOwnerChanged;
            _owner = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnOwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs change) {
        if (change.Property == WindowBase.IsActiveProperty ||
            change.Property == Window.WindowStateProperty) {
            Retell();
        }
    }

    /// <summary>
    /// Wears what the window's own states say: <c>:active</c> the while the
    /// window holds the eye, <c>:maximized</c> the while it fills the room.
    /// </summary>
    private void Retell() {
        PseudoClasses.Set(":active", _owner?.IsActive == true);
        PseudoClasses.Set(":maximized", _owner is { WindowState: WindowState.Maximized });
    }
}
