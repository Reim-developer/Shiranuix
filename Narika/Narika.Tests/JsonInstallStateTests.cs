using Narika.Services.Install;

namespace Narika.Tests;

/// <summary>
/// Tests for <see cref="JsonInstallState"/>: the receipt the next run of the
/// launcher reads, written and read back entirely inside a folder of this
/// test's own.
/// </summary>
public sealed class JsonInstallStateTests : IDisposable {
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "narika-tests", Guid.NewGuid().ToString("N"));

    private string Receipt => Path.Combine(_folder, "install.json");

    /// <summary>
    /// The folder this test's receipt lives in, taken back whole — the
    /// receipt, the program it names, and everything else beside them.
    /// </summary>
    public void Dispose() {
        try {
            if (Directory.Exists(_folder)) {
                Directory.Delete(_folder, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    [Fact]
    public void A_First_Run_Says_Nothing_Is_Installed() {
        var state = new JsonInstallState(Receipt);

        Assert.Null(state.ExecutablePath);
        Assert.False(state.IsInstalled);
    }

    [Fact]
    public void What_Was_Marked_Is_What_The_Next_Reader_Believes() {
        var program = WriteProgram();

        new JsonInstallState(Receipt).MarkInstalled(program);

        var next = new JsonInstallState(Receipt);

        Assert.Equal(program, next.ExecutablePath);
        Assert.True(next.IsInstalled);
    }

    [Fact]
    public void Marking_Tells_The_Launcher_The_Moment_It_Changed() {
        var program = WriteProgram();
        var state = new JsonInstallState(Receipt);
        var raised = false;
        state.Changed += (_, _) => raised = true;

        state.MarkInstalled(program);

        Assert.True(raised);
    }

    [Fact]
    public void A_Receipt_Whose_Program_Is_Gone_Says_Not_Installed() {
        // The receipt is not the truth on its own: a person who deleted the
        // program by hand has uninstalled it, and the card offers Download
        // again rather than Launch at something that is not there.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(
            Receipt, """{"ExecutablePath":"C:\\gone\\Shiranuix.exe"}""");

        var state = new JsonInstallState(Receipt);

        Assert.Equal(@"C:\gone\Shiranuix.exe", state.ExecutablePath);
        Assert.False(state.IsInstalled);
    }

    [Fact]
    public void A_Receipt_That_Cannot_Be_Read_Says_Nothing() {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Receipt, "this is not a receipt");

        var state = new JsonInstallState(Receipt);

        Assert.Null(state.ExecutablePath);
        Assert.False(state.IsInstalled);
    }

    /// <summary>
    /// A file at the program's usual path — not a program, but on disk
    /// wherever an installed one would be, which is all the receipt asks.
    /// </summary>
    private string WriteProgram() {
        Directory.CreateDirectory(_folder);

        var program = Path.Combine(_folder, "Shiranuix.exe");
        File.WriteAllText(program, "a program, of sorts");

        return program;
    }
}
