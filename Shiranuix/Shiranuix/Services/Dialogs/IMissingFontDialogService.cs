namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Asks which typeface a file should be opened in when the one it names is
/// not on this machine.
/// </summary>
/// <remarks>
/// <para>
/// A file may name a typeface nobody has: the name travelled with the
/// words, and the machine now reading them may have nothing by that name.
/// Rather than opening in some face nobody chose, this asks — with the
/// file's own words shown in each face as it is picked, so the answer is
/// made by looking rather than by guessing.
/// </para>
/// <para>
/// Abstracted from any one window for the same reason the export's chooser
/// is: opening a file is exercised headlessly in tests, and what a test
/// hears is whatever the fake was told to answer.
/// </para>
/// </remarks>
public interface IMissingFontDialogService {
    /// <summary>
    /// Shows the dialog.
    /// </summary>
    /// <param name="missing">The typeface the file names, which this
    /// machine does not carry.</param>
    /// <param name="size">The point size the file stands at, which is
    /// carried whether or not the face is.</param>
    /// <param name="sample">Some of the file's own writing, to judge the
    /// faces by.</param>
    /// <param name="initial">The face to open on: the typeface this
    /// document is standing on now.</param>
    /// <returns>
    /// The typeface to open with, or null when the opening was called off.
    /// </returns>
    Task<string?> AskAsync(string missing, double size, string sample, string initial);
}
