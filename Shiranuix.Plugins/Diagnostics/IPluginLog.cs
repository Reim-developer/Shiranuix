namespace Shiranuix.Plugins.Diagnostics;

/// <summary>
/// The one door a plugin has to speak to the host it is running inside.
/// </summary>
/// <remarks>
/// Every line written here is prefixed with the plugin's id, so a log tells
/// which plugin said what without the plugin having to say it twice.
/// </remarks>
public interface IPluginLog {
    /// <summary>
    /// Writes one line about something that happened.
    /// </summary>
    /// <param name="level">How much it matters.</param>
    /// <param name="message">What happened, in a sentence.</param>
    /// <param name="error">
    /// The exception behind it, when there was one. The host writes the
    /// stack; a plugin never has to format one itself.
    /// </param>
    void Write(PluginLogLevel level, string message, Exception? error = null);
}
