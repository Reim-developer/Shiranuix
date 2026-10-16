namespace Shiranuix.Plugins;

/// <summary>
/// How much a line in the log matters.
/// </summary>
/// <remarks>
/// Three levels, and that is all. A plugin saying "this went wrong" and a
/// plugin saying "this went wrong while counting words in chapter twelve" are
/// the same event with different sentences in front of it, and the sentence is
/// the plugin's business, not the level's.
/// </remarks>
public enum PluginLogLevel {
    /// <summary>Something happened that is worth writing down.</summary>
    Information,

    /// <summary>
    /// Something unexpected happened and the plugin carried on.
    /// </summary>
    Warning,

    /// <summary>
    /// Something failed and the plugin could not carry on.
    /// </summary>
    Error
}
