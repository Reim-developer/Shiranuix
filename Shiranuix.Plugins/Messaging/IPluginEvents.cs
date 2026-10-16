namespace Shiranuix.Plugins.Messaging;

/// <summary>
/// A way to stop hearing about a kind of news.
/// </summary>
/// <remarks>
/// Disposing the subscription unsubscribes, which is why
/// <see cref="IPluginEvents.Subscribe{TEvent}"/> hands one back rather than
/// only a delegate to forget. A plugin that keeps a subscription for the life
/// of the editor has nothing to stop; a plugin that watches the document while
/// a dialog is open drops it when the dialog closes.
/// </remarks>
public interface IPluginSubscription : IDisposable {
    /// <summary>Whether the handler is still being called.</summary>
    bool IsActive { get; }
}

/// <summary>
/// The channel plugins and the editor talk over: news with a type, and
/// handlers that asked for that type.
/// </summary>
/// <remarks>
/// <para>
/// The channel knows nothing about what travels along it. A plugin publishing
/// <c>FileOpened</c> and a plugin listening for <c>FileOpened</c> agree
/// because they name the same type, not because the editor was told to carry
/// it — which is the whole point: a plugin can invent a kind of news the editor
/// has never heard of, and another plugin can hear it, without a line changing
/// in the contract.
/// </para>
/// <para>
/// Handlers are called in the order they subscribed, on whichever thread
/// published. One that throws is written about and the rest still run: a
/// misbehaving subscriber is that subscriber's problem, not the publisher's.
/// </para>
/// <para>
/// One thing the channel cannot do: hear a kind of news that exists in two
/// copies. Each plugin is loaded into a world of its own, so a news type one
/// plugin defines is a different type to another — the two would sit on
/// opposite shelves and never meet. Types that more than one party must share
/// come from an assembly the editor shares, which today means this contract.
/// </para>
/// </remarks>
public interface IPluginEvents {
    /// <summary>
    /// Asks to hear about a kind of news.
    /// </summary>
    /// <typeparam name="TEvent">
    /// What to listen for — a class, so subscriptions can be told apart and
    /// so a listener cannot publish an empty piece of news by accident.
    /// </typeparam>
    /// <param name="handler">What to do when it arrives.</param>
    /// <returns>
    /// The subscription. Dispose it to stop hearing; disposing twice is
    /// harmless, and disposing after the editor has gone is not an error.
    /// </returns>
    /// <exception cref="ArgumentNullException">The handler is null.</exception>
    IPluginSubscription Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : class;

    /// <summary>
    /// Announces something to everyone listening for that kind of news.
    /// </summary>
    /// <typeparam name="TEvent">What happened.</typeparam>
    /// <param name="news">The news itself.</param>
    /// <returns>
    /// A refusal when one or more handlers threw, saying how many, so a
    /// publisher can decide whether that matters to it. The editor writes the
    /// detail about each one.
    /// </returns>
    /// <exception cref="ArgumentNullException">The news is null.</exception>
    PluginOutcome Publish<TEvent>(TEvent news)
        where TEvent : class;

    /// <summary>
    /// How many handlers are listening for a kind of news.
    /// </summary>
    /// <typeparam name="TEvent">What to count listeners for.</typeparam>
    /// <returns>How many are listening now.</returns>
    int ListenerCount<TEvent>()
        where TEvent : class;
}
