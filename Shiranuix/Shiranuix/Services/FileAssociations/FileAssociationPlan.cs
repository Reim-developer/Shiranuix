namespace Shiranuix.Services.FileAssociations;

/// <summary>
/// Everything Shiranuix asks the machine's registry to keep, written out here
/// first and written into a machine nowhere but by
/// <see cref="WindowsFileAssociationService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two things are being asked of Windows, and they are deliberately not the
/// same size. A <c>.shiranuix</c> file becomes a Shiranuix Document: its own
/// name in Explorer, the editor's own mark for an icon, and a double-click
/// that opens the editor. That is a claim on a format this app owns outright,
/// and taking it is only fair. A <c>.txt</c> file gains one line on its
/// right-click menu — "Open with Shiranuix" — and gains nothing else. It is
/// not made anybody's default, its own name does not change, and every other
/// program that opens it is exactly as able to as it was.
/// </para>
///
/// <para>
/// Everything here lives under <c>HKEY_CURRENT_USER</c>. Nothing is written
/// machine-wide, so nothing asks for an administrator, nothing follows the
/// person to another login, and one person choosing to unregister takes back
/// everything one person put there.
/// </para>
/// </remarks>
public static class FileAssociationPlan {
    /// <summary>
    /// What Windows calls Shiranuix when it is naming the editor to somebody:
    /// a ProgID, which is the key everything else hangs off.
    /// </summary>
    public const string ProgId = "Shiranuix.Document";

    /// <summary>
    /// How a Shiranuix Document is described wherever it is described.
    /// </summary>
    public const string FriendlyName = "Shiranuix Document";

    /// <summary>
    /// The one line the right-click menu adds over a plain text file.
    /// </summary>
    public const string TextFileVerb = "Open with Shiranuix";

    /// <summary>
    /// The key remembering what was written and where from, so the next run
    /// has something to compare against and does no work it need not do.
    /// </summary>
    public const string MarkerKey = @"Software\Shiranuix\FileAssociations";

    /// <summary>What the remembering key is called inside itself.</summary>
    public const string MarkerName = "Registered";

    /// <summary>
    /// Where Windows keeps the per-user half of everything named in Explorer.
    /// Written through <c>HKEY_CURRENT_USER</c>, this is the same tree the
    /// machine-wide half is read from — the two are merged, and the person's
    /// own words are the ones that win.
    /// </summary>
    private const string Classes = @"Software\Classes";

    /// <summary>
    /// The verb the right-click menu is given over a plain text file. It hangs
    /// off <c>txtfile</c>, which is the name Windows has given that file type
    /// since long before this editor existed.
    /// </summary>
    private const string TextFileVerbKey = @"Software\Classes\txtfile\shell\ShiranuixOpen";

    /// <summary>
    /// What the running build is and where it is standing, in one line.
    /// </summary>
    /// <param name="executablePath">Where the editor was started from.</param>
    /// <param name="version">The build's version, in words.</param>
    /// <returns>A line no other build's would match.</returns>
    public static string Marker(string executablePath, string version) =>
        $"{executablePath}|{version}";

    /// <summary>
    /// Everything to write, in the order it is written.
    /// </summary>
    /// <param name="executablePath">Where the editor was started from.</param>
    /// <param name="version">The build's version, in words.</param>
    /// <returns>What to keep, and where.</returns>
    public static IReadOnlyList<RegisteredValue> For(
        string executablePath,
        string version) =>
    [
        // The name itself. From here on, "Shiranuix.Document" is a thing
        // Windows knows, and the rest of this list only fills it in.
        new RegisteredValue($@"{Classes}\{ProgId}", "", FriendlyName),

        // The mark: the editor's own, lifted out of the executable it was
        // started from rather than carried alongside it as a second file
        // that could be separated from the first.
        new RegisteredValue($@"{Classes}\{ProgId}\DefaultIcon", "", IconFor(executablePath)),

        // The double-click. `%1` is the file being opened, handed over the
        // same way Explorer has always handed it.
        new RegisteredValue($@"{Classes}\{ProgId}\shell\open\command", "", OpenWith(executablePath)),

        // This file type is that document type. `.shiranuix` is the editor's
        // own format, so it is claimed outright rather than merely offered.
        new RegisteredValue($@"{Classes}\.shiranuix", "", ProgId),
        new RegisteredValue($@"{Classes}\.shiranuix\OpenWithProgids", ProgId, null),

        // And this one is only offered: a text file gains the editor as one
        // thing among the things it could be opened with, and keeps every
        // other one exactly as it was.
        new RegisteredValue($@"{Classes}\.txt\OpenWithProgids", ProgId, null),

        // The line on the right-click menu. Two names for what it says,
        // because a shell that has stopped reading one still reads the other,
        // and the person looking at the menu should not be able to tell.
        new RegisteredValue(TextFileVerbKey, "", TextFileVerb),
        new RegisteredValue(TextFileVerbKey, "MUIVerb", TextFileVerb),
        new RegisteredValue(TextFileVerbKey, "Icon", IconFor(executablePath)),

        // And where that line leads.
        new RegisteredValue($@"{TextFileVerbKey}\command", "", OpenWith(executablePath)),

        // Last, what was done and from where — so the next run reads it,
        // finds it still true, and writes nothing at all.
        new RegisteredValue(MarkerKey, MarkerName, Marker(executablePath, version))
    ];

    /// <summary>
    /// The keys that can be deleted whole when the association is taken back.
    /// </summary>
    /// <remarks>
    /// Every one of them a key this app created — one of them under the
    /// spelling the extension used to carry, which older builds wrote and
    /// this one takes back — and only one of them a document type: the note
    /// left for the next run is here for the same reason the rest is, and
    /// goes the same way.
    /// </remarks>
    public static IReadOnlyList<string> KeysToForget { get; } =
    [
        $@"{Classes}\{ProgId}",
        $@"{Classes}\.shiranuix",
        $@"{Classes}\.shiranux",
        $@"{TextFileVerbKey}",
        MarkerKey
    ];

    /// <summary>
    /// The keys older builds left behind under the spelling the extension
    /// used to carry: this app no longer reads what that key claims to be,
    /// so nobody should be handed to it any more.
    /// </summary>
    public static IReadOnlyList<string> StaleKeysToForget { get; } =
    [
        $@"{Classes}\.shiranux"
    ];

    /// <summary>
    /// The values to take out of keys belonging to someone else.
    /// </summary>
    /// <remarks>
    /// <c>OpenWithProgids</c> over <c>.txt</c> is Windows' own key and holds
    /// every program that could open it, so it is added to rather than
    /// claimed — which means taking the editor's name back out of it is the
    /// only honest thing to do.
    /// </remarks>
    public static IReadOnlyList<RegisteredValue> ValuesToForget { get; } =
    [
        new RegisteredValue($@"{Classes}\.txt\OpenWithProgids", ProgId, null)
    ];

    /// <summary>
    /// What a shell is given to open a document with this editor.
    /// </summary>
    private static string OpenWith(string executablePath) =>
        $"\"{executablePath}\" \"%1\"";

    /// <summary>
    /// Where a shell finds this editor's mark, as an index into the
    /// executable's own.
    /// </summary>
    private static string IconFor(string executablePath) =>
        $"\"{executablePath}\",0";
}
