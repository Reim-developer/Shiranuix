namespace Shiranuix.Services;

/// <summary>
/// Reads and writes the document's bytes to and from disk.
/// </summary>
/// <remarks>
/// Kept behind an interface so the view model can be exercised headlessly --
/// the tests substitute an in-memory implementation and never touch the file
/// system, which is the same split Shiranuix uses for its services.
/// </remarks>
public interface IFileService {
    /// <summary>
    /// Reads a UTF-8 text file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The file's text.</returns>
    string ReadAllText(string path);

    /// <summary>
    /// Writes UTF-8 text, creating or truncating the file.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">The text to write.</param>
    void WriteAllText(string path, string text);

    /// <summary>
    /// Reports whether a file exists.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True when the file is present.</returns>
    bool Exists(string path);
}
