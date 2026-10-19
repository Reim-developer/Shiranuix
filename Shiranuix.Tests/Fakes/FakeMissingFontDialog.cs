using Shiranuix.Services.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// The missing typeface dialog as a test hears it: whatever it was told to
/// answer, and every question it was asked along the way.
/// </summary>
/// <remarks>
/// The dialog is the one step in opening a file that a headless test cannot
/// take, so the answer is told rather than made — and the questions are kept
/// so a test can assert the file was asked about at all, and asked with its
/// own words rather than somebody else's.
/// </remarks>
public sealed class FakeMissingFontDialog : IMissingFontDialogService {
    /// <summary>
    /// The answer to give: a typeface to open in, or null to call the
    /// opening off entirely.
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// Every question the dialog was asked, in the order it was asked.
    /// </summary>
    public List<(string Missing, double Size, string Sample, string Initial)> Asked { get; } = new();

    /// <summary>How many times the dialog was asked anything.</summary>
    public int Count => Asked.Count;

    /// <inheritdoc />
    public Task<string?> AskAsync(string missing, double size, string sample, string initial) {
        Asked.Add((missing, size, sample, initial));
        return Task.FromResult(Answer);
    }
}
