using System.Runtime.CompilerServices;

namespace Narika.Tests;

/// <summary>
/// A fact that runs where the rule it tests stands: some refusals are the
/// Windows disk's own — a folder kept busy by a file open elsewhere, which
/// an unlink takes away without a word on Unix — so on every other system
/// the test says so plainly and stands down rather than failing on a rule
/// that was never there. On Windows it is a fact like any other, and it
/// runs with all the rest.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute {
    /// <summary>
    /// Stands down off Windows; the source information the testing platform
    /// asks for is carried up to the fact itself.
    /// </summary>
    /// <param name="sourceFilePath">Filled in by the compiler.</param>
    /// <param name="sourceLineNumber">Filled in by the compiler.</param>
    public WindowsFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber) {
        if (!OperatingSystem.IsWindows()) {
            Skip = "A folder kept busy by an open file is the Windows disk's refusal.";
        }
    }
}
