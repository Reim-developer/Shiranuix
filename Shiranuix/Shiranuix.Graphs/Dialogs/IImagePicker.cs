namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// Asks the writer for a picture: a character's face.
/// </summary>
/// <remarks>
/// The package asks for the path of a picture and leaves the choosing of
/// one to whoever holds the window — the app, which knows how a file is
/// chosen on the machine it is running on.
/// </remarks>
public interface IImagePicker {
    /// <summary>
    /// Asks for a picture, and answers with where it is.
    /// </summary>
    /// <param name="title">The words the picker is opened with.</param>
    /// <returns>The path to the picture, or nothing when the writer walked away.</returns>
    Task<string?> PickAsync(string title);
}
