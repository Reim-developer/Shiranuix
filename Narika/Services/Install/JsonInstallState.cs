using System.Text.Json;

namespace Narika.Services.Install;

/// <summary>
/// <see cref="IInstallState"/> written down where the next run of the
/// launcher will find it: one small receipt beside the application's own
/// settings, naming the program the last install left behind.
/// </summary>
/// <remarks>
/// The receipt is not the truth on its own — it is believed only while the
/// file it names is still there. A person who deletes the installed program
/// by hand has uninstalled it as far as this launcher is concerned, and the
/// card offers Download again rather than Launch at something that is gone.
/// A receipt that cannot be read says the same thing: nothing is installed,
/// and nothing above this class learns whether that was a deletion, a first
/// run, or a file that lost its meaning.
/// </remarks>
public sealed class JsonInstallState : IInstallState {
    private readonly string _receipt;
    private readonly string? _executablePath;

    /// <summary>
    /// Creates the state over one receipt file, reading it at once — the
    /// launcher asks whether anything is installed before it shows its
    /// first frame, so there is no later moment to read it in.
    /// </summary>
    /// <param name="receiptPath">Where the receipt is kept.</param>
    public JsonInstallState(string receiptPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptPath);

        _receipt = receiptPath;
        _executablePath = Read();
    }

    /// <inheritdoc />
    public string? ExecutablePath => _executablePath;

    /// <inheritdoc />
    public bool IsInstalled => _executablePath is { } path && File.Exists(path);

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void MarkInstalled(string executablePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var folder = Path.GetDirectoryName(_receipt);
        if (!string.IsNullOrEmpty(folder)) {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_receipt, JsonSerializer.Serialize(new Receipt(executablePath)));

        // The write is the whole act: the launcher hears about it through
        // the event, and the next run hears about it through the file.
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// What the receipt says, or nothing at all when it says nothing that
    /// can be read — see the remarks on why an unreadable receipt is not
    /// an error.
    /// </summary>
    private string? Read() {
        try {
            return File.Exists(_receipt)
                ? JsonSerializer.Deserialize<Receipt>(File.ReadAllText(_receipt))?.ExecutablePath
                : null;
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>
    /// The receipt itself: one line of JSON, named the way it is read.
    /// </summary>
    private sealed record Receipt(string ExecutablePath);
}
