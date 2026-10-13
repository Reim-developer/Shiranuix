using System.Text;

namespace Shiranuix.Services;

/// <summary>
/// The real file service, backed by <see cref="File"/>.
/// </summary>
/// <remarks>
/// Reads and writes UTF-8 without a byte-order mark. A BOM is a legacy
/// artefact that confuses `diff` and `git status`, and no editor in 2026
/// needs one; detecting it on read and omitting it on write keeps files
/// clean whether or not the author's previous tool added one.
/// </remarks>
public sealed class FileService : IFileService {
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public string ReadAllText(string path) {
        // File.ReadAllText auto-detects a BOM and strips it; reading bytes and
        // decoding explicitly is what lets us guarantee "no BOM on write".
        var bytes = File.ReadAllBytes(path);

        if (bytes.Length == 0) {
            return string.Empty;
        }

        return Utf8NoBom.GetString(bytes);
    }

    /// <inheritdoc />
    public void WriteAllText(string path, string text) =>
        File.WriteAllText(path, text, Utf8NoBom);

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path);
}
