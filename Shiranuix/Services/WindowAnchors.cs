using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// Every window start a document has stood at, with the file line it stood
/// on, in the order the positions fall.
/// </summary>
/// <remarks>
/// An undo made in a window that has since slid away is put back by walking
/// from the nearest of these: from the file's own start, every time, would
/// be the whole file. The memory is bounded the way the history is — a long
/// session remembers a thousand starts — while never dropping the file's own
/// start, which every walk falls back on. A lookup takes the nearest start
/// at or before a position or a line, so a walk covers only the stretch
/// between the two. Nothing here reads the file: it is remembering, and the
/// window does the walking.
/// </remarks>
public sealed class WindowAnchors {
    /// <summary>
    /// How many window starts are remembered for sliding back and for
    /// undo's return trips. Capped the way the history is, and never below
    /// the file's own start: every walk falls back on that one.
    /// </summary>
    private const int Limit = 1000;

    private readonly List<(LineAnchor At, int FileLine)> _items = new();

    /// <summary>
    /// Starts the memory over at one place — the way a new or newly opened
    /// document begins: this start, and nothing standing before it.
    /// </summary>
    /// <param name="at">Where the window begins in the file.</param>
    /// <param name="fileLine">The file line it begins on, from zero.</param>
    public void Reset(LineAnchor at, int fileLine) {
        _items.Clear();
        _items.Add((at, fileLine));
    }

    /// <summary>
    /// Remembers where a window stood, in the order the positions fall.
    /// </summary>
    /// <param name="at">Where the window began.</param>
    /// <param name="fileLine">The line it began on.</param>
    public void Add(LineAnchor at, int fileLine) {
        foreach (var anchor in _items) {
            if (anchor.At.Offset == at.Offset) {
                return;
            }
        }

        var index = _items.Count;

        while (index > 0 && _items[index - 1].At.Offset > at.Offset) {
            index--;
        }

        _items.Insert(index, (at, fileLine));

        // Undo cannot reach further back than the history itself, so the
        // windows it might return through stay bounded the same way — while
        // the file's own start, every walk falls back on, is never dropped.
        while (_items.Count > Limit) {
            _items.RemoveAt(1);
        }
    }

    /// <summary>
    /// The remembered window start at or before a file position, so a walk
    /// covers only the lines between the two.
    /// </summary>
    /// <param name="offset">The file position, in characters.</param>
    /// <returns>The nearest window start and the line it stood on.</returns>
    public (LineAnchor At, int FileLine) AtOrBefore(int offset) {
        var best = _items[0];

        foreach (var anchor in _items) {
            if (anchor.At.Offset > offset) {
                break;
            }

            best = anchor;
        }

        return best;
    }

    /// <summary>
    /// The remembered window start at or before a file line.
    /// </summary>
    /// <param name="fileLine">The line, from zero.</param>
    /// <returns>The nearest window start and the line it stood on.</returns>
    public (LineAnchor At, int FileLine) AtOrBeforeLine(int fileLine) {
        var best = _items[0];

        foreach (var anchor in _items) {
            if (anchor.FileLine > fileLine) {
                break;
            }

            best = anchor;
        }

        return best;
    }

    /// <summary>
    /// Slides every remembered start after what collapsed into the file that
    /// was just written — each by whatever the moves before it did to its
    /// place.
    /// </summary>
    /// <param name="ledger">The moves as they stood against the file when
    /// it was written, before the ledger takes the new file as its zero.</param>
    public void Shift(EditLedger ledger) {
        ArgumentNullException.ThrowIfNull(ledger);

        for (var index = 0; index < _items.Count; index++) {
            var anchor = _items[index];
            var shift = ledger.ShiftAt(anchor.At.Offset);

            _items[index] = (
                new LineAnchor(anchor.At.Byte + shift.Bytes, anchor.At.Offset + shift.Characters),
                anchor.FileLine + shift.Lines);
        }
    }
}
