using Shiranuix.CoreFormat;
using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The document as its file side reaches it: what a file arriving asks of
/// the document, and what a write leaves behind to put right.
/// </summary>
/// <remarks>
/// The mirror of <see cref="Shiranuix.Services.IWindowStage"/>: the
/// navigator plans a window and the stage puts it on screen, and
/// <see cref="DocumentFile"/> reads or writes a file and this stage adopts
/// the outcome. The file side holds the story — which file, which
/// convention, which clean point — and every move that touches the
/// document itself comes through this door, so the ordering of a rebegun
/// or a save is one conversation rather than two halves of a class.
/// </remarks>
public interface IFileStage {
    /// <summary>The line-break convention the file is written in.</summary>
    Newline Newline { get; }

    /// <summary>
    /// How the document is set — the typeface it stands in and the point
    /// size it stands at — which is what writing it lays into the file.
    /// </summary>
    DocumentTypography Typography { get; }

    /// <summary>The window's text, as the document shows it.</summary>
    string Text { get; }

    /// <summary>
    /// The moves standing against the window, laid over the spans they fall
    /// on — what writing the file lays down.
    /// </summary>
    IReadOnlyList<TextReplacement> PendingChanges();

    /// <summary>
    /// Adopts a file arriving: every move standing against the document
    /// cleared, the window reset to the file's first lines, and the text
    /// and path replaced. None of it is a move the writer made.
    /// </summary>
    /// <param name="path">The file that arrived.</param>
    /// <param name="scan">What a scan said of the whole file.</param>
    /// <param name="text">The first window's text.</param>
    /// <param name="more">Whether the file carries lines past the window.</param>
    void AdoptFile(string path, ScanResult scan, string text, bool more);

    /// <summary>
    /// Adopts how a file says it is set: the typeface and point size it
    /// carries become the document's, so the words are drawn the way they
    /// were meant to be read. A file saying nothing about its setting leaves
    /// the document on the setting it already had — nothing said is not
    /// nothing chosen, it is only nothing carried.
    /// </summary>
    /// <param name="said">What the file carried, or null when it carried nothing.</param>
    void AdoptTypography(DocumentTypography? said);

    /// <summary>
    /// Adopts a blank document: the rebegun of a document with no file
    /// behind it, on the convention the file side names.
    /// </summary>
    /// <param name="scan">The scan of nothing.</param>
    /// <param name="newline">The convention a blank document starts on.</param>
    void AdoptBlank(ScanResult scan, Newline newline);

    /// <summary>
    /// Marks the point the file now holds: the text saved, and the
    /// convention it was saved in.
    /// </summary>
    /// <param name="newline">The convention the file went out in.</param>
    void MarkSaved(Newline newline);

    /// <summary>
    /// Re-seats the caret in whatever text stands now — a file having just
    /// arrived — without reading the landing as the writer asking for a
    /// slide.
    /// </summary>
    void ReseatCaret();

    /// <summary>
    /// Puts the document right after a successful write: the path points at
    /// the file, every position slid to where its stretch of file now
    /// stands, and the moves behind the written text collapsed into the new
    /// zero.
    /// </summary>
    /// <param name="path">The file that was written.</param>
    /// <param name="written">What the file says now, whole.</param>
    void ApplySave(string path, ScanResult written);
}
