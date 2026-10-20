using Shiranuix.Services.FileAssociations;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="FileAssociationPlan"/>: what this editor asks a
/// machine to keep, written out before any of it is kept anywhere.
/// </summary>
/// <remarks>
/// Two claims are being made here, and they are not the same size. A
/// <c>.shiranux</c> file becomes a Shiranuix Document outright, because it is
/// this editor's own format. A <c>.txt</c> file is offered the editor and
/// nothing more — and the tests below are as much about what the plan
/// declines to do as about what it does, since a text file is somebody else's
/// format and taking it would be the kind of thing this editor does not do.
/// </remarks>
public sealed class FileAssociationPlanTests {
    private const string Executable = @"C:\Apps\Shiranuix\Shiranuix.exe";
    private const string Version = "0.5.12";

    private static IReadOnlyList<RegisteredValue> Plan() =>
        FileAssociationPlan.For(Executable, Version);

    [Fact]
    public void A_Document_File_Is_Named_And_Opened() {
        var plan = Plan();

        Assert.Equal(
            FileAssociationPlan.FriendlyName,
            Value(@"Software\Classes\Shiranuix.Document", plan));
        Assert.Equal(
            "\"C:\\Apps\\Shiranuix\\Shiranuix.exe\" \"%1\"",
            Value(@"Software\Classes\Shiranuix.Document\shell\open\command", plan));
    }

    [Fact]
    public void A_Document_File_Takes_The_Editors_Own_Mark() {
        // The mark is lifted out of the executable rather than carried beside
        // it as a second file that could be separated from the first.
        Assert.Equal(
            "\"C:\\Apps\\Shiranuix\\Shiranuix.exe\",0",
            Value(@"Software\Classes\Shiranuix.Document\DefaultIcon", Plan()));
    }

    [Fact]
    public void The_Editor_Owns_Its_Own_Format() {
        var plan = Plan();

        Assert.Equal(FileAssociationPlan.ProgId, Value(@"Software\Classes\.shiranux", plan));
        Assert.Contains(
            plan,
            entry => entry.Key == @"Software\Classes\.shiranux\OpenWithProgids"
                     && entry.Name == FileAssociationPlan.ProgId
                     && entry.IsFlag);
    }

    [Fact]
    public void A_Text_File_Is_Offered_With_A_Line_Of_Its_Own() {
        var plan = Plan();
        const string verb = @"Software\Classes\txtfile\shell\ShiranuixOpen";

        Assert.Equal(FileAssociationPlan.TextFileVerb, Value(verb, plan));
        Assert.Equal(FileAssociationPlan.TextFileVerb, Value(verb, plan, "MUIVerb"));
        Assert.Equal(
            "\"C:\\Apps\\Shiranuix\\Shiranuix.exe\" \"%1\"",
            Value($@"{verb}\command", plan));
    }

    [Fact]
    public void A_Text_File_Is_Not_Made_Anyones_Default() {
        // The whole point of the difference in size. A .txt file gains the
        // editor as one thing among the things it could be opened with, and
        // every other program that opens it is exactly as able to as it was.
        var plan = Plan();

        // Nothing may name .txt itself: that would be saying who opens it.
        Assert.All(plan, entry => Assert.NotEqual(@"Software\Classes\.txt", entry.Key));

        // And everything that does live under .txt lives in the one list of
        // what could open it — nothing else, and nothing sitting where a
        // default would sit.
        Assert.All(
            plan.Where(entry => entry.Key.StartsWith(
                @"Software\Classes\.txt\", StringComparison.Ordinal)),
            entry => Assert.True(
                entry.Key == @"Software\Classes\.txt\OpenWithProgids"
                || entry.Key.StartsWith(
                    @"Software\Classes\.txt\OpenWithProgids\", StringComparison.Ordinal),
                $@"{entry.Key} is under .txt, but not in what could open it."));
    }

    [Fact]
    public void Everything_Is_Written_For_One_User_Only() {
        // No administrator, no machine, and nothing that follows the person
        // to another login. Everything hangs off the current user's own half
        // of the registry, which the plan reaches by name alone — it says
        // where, and never which machine.
        Assert.All(Plan(), entry => Assert.StartsWith(@"Software\", entry.Key));
    }

    [Fact]
    public void The_Build_Notes_Where_It_Stands() {
        Assert.Equal(
            $"{Executable}|{Version}",
            Value(FileAssociationPlan.MarkerKey, Plan(), FileAssociationPlan.MarkerName));
    }

    [Fact]
    public void A_Different_Build_Stands_Differently() {
        // The marker is the whole reason a second run can do nothing at all,
        // and it has to notice the two things that change: what is running,
        // and where it is running from.
        Assert.NotEqual(
            FileAssociationPlan.Marker(Executable, Version),
            FileAssociationPlan.Marker(Executable, "0.5.13"));
        Assert.NotEqual(
            FileAssociationPlan.Marker(Executable, Version),
            FileAssociationPlan.Marker(@"D:\Portable\Shiranuix.exe", Version));
    }

    [Fact]
    public void Taking_Back_Takes_Only_What_Was_Written() {
        // An unregister has to leave the machine the way it was found, which
        // means it may take back only what this very plan put in — a key
        // written and never deleted, or a name added to somebody else's key
        // and never taken back out.
        var keys = FileAssociationPlan.KeysToForget;

        foreach (var entry in Plan()) {
            var isKeptWhole = keys.Any(key =>
                entry.Key == key
                || entry.Key.StartsWith($@"{key}\", StringComparison.Ordinal));
            var isTakenOut = FileAssociationPlan.ValuesToForget.Any(
                back => back.Key == entry.Key && back.Name == entry.Name);

            Assert.True(
                isKeptWhole || isTakenOut,
                $@"{entry.Key}\{entry.Name} is written but nothing takes it back.");
        }
    }

    [Fact]
    public void Taking_Back_Touches_Someone_Elses_Key_Only_Where_It_Added() {
        // OpenWithProgids over .txt is Windows' own key and holds every
        // program that could open it, so it is added to rather than claimed —
        // and only the editor's own name may ever come back out of it.
        var only = Assert.Single(FileAssociationPlan.ValuesToForget);

        Assert.Equal(@"Software\Classes\.txt\OpenWithProgids", only.Key);
        Assert.Equal(FileAssociationPlan.ProgId, only.Name);
    }

    [Fact]
    public void No_Key_To_Forget_Is_The_Whole_User_Registry() {
        // A tree that reached too far would take somebody else's programs
        // with it, and the only honest thing to take back is the editor's own.
        Assert.All(
            FileAssociationPlan.KeysToForget,
            key => Assert.StartsWith(@"Software\", key));
        Assert.DoesNotContain(@"Software", FileAssociationPlan.KeysToForget);
    }

    /// <summary>
    /// What a key's own value says, looked up by the shape of its name.
    /// </summary>
    /// <param name="key">The key to look under.</param>
    /// <param name="plan">What was planned.</param>
    /// <param name="name">What the value is called there; the key's own when
    /// nothing is said, which is how a registry names the thing itself.</param>
    private static string Value(
        string key,
        IReadOnlyList<RegisteredValue> plan,
        string name = "") {
        var entry = Assert.Single(plan, entry => entry.Key == key && entry.Name == name);
        return Assert.IsType<string>(entry.Value);
    }
}
