namespace Shiranuix.CoreEditor;

/// <summary>
/// The files the user has opened lately, newest first.
/// </summary>
/// <remarks>
/// Kept behind an interface for the same reason as
/// <see cref="Services.IFileService"/>: the start screen's list is filled from it, and
/// a headless test needs an in-memory one.
/// </remarks>
public interface IRecentFilesStore {
    /// <summary>
    /// Reads the remembered paths.
    /// </summary>
    /// <returns>The paths, newest first.</returns>
    IReadOnlyList<string> Load();

    /// <summary>
    /// Moves a path to the front of the remembered list.
    /// </summary>
    /// <param name="path">The path to remember.</param>
    void Remember(string path);
}
