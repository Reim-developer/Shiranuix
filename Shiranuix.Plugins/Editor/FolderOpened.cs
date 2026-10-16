namespace Shiranuix.Plugins.Editor;

/// <summary>
/// The writer has opened a folder: a whole folder, not a document in one.
/// </summary>
/// <remarks>
/// <para>
/// A folder is a thing the editor now opens, beside the documents it opens,
/// and it is news the way a document is: anything in the editor that has
/// something to do with a folder - a file tree, a search across files, a
/// project view - hears about it the same way it hears about a document
/// changing.
/// </para>
/// <para>
/// It travels on the shared channel rather than being asked for by name,
/// because the editor does not know which plugins care and neither does any
/// one plugin: a plugin that has never heard of this news simply never hears
/// it. That is the whole arrangement the channel is for.
/// </para>
/// </remarks>
/// <param name="Path">The folder that was opened, in full.</param>
public sealed record FolderOpened(string Path);
