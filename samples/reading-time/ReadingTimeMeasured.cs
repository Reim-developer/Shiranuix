namespace ReadingTime;

/// <summary>
/// What the plugin learned from counting the document, announced to whichever
/// plugins are listening for it.
/// </summary>
/// <remarks>
/// A plugin's own kind of news: nothing in the contract or the editor mentions
/// this type, and a plugin that wanted to watch reading time simply declares
/// that it wants <c>ReadingTimeMeasured</c> and hears it. That is what the
/// channel is for — a way to be heard about something without asking the host
/// to carry it.
/// </remarks>
/// <param name="Words">How many words the document held.</param>
/// <param name="Minutes">How long that would take to read.</param>
public sealed record ReadingTimeMeasured(int Words, int Minutes);
