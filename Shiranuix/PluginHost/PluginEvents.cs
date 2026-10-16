using Microsoft.Extensions.Logging;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Messaging;

namespace Shiranuix.PluginHost;

/// <summary>
/// The channel plugins and the editor announce news over, kept per kind of news
/// so that a listener for <c>FileOpened</c> is woken by that and nothing else.
/// </summary>
/// <remarks>
/// Handlers run in subscription order on the publishing thread, which is the
/// only rule a channel small enough to reason about can hold to. One that
/// throws is written about and the rest still run: a listener that cannot cope
/// is that listener's problem, and a publisher should not have to know which
/// of its listeners is which.
/// </remarks>
internal sealed class PluginEvents : IPluginEvents {
    private readonly Dictionary<Type, List<Subscription>> _listeners = [];
    private readonly ILogger _log;
    private readonly Lock _gate = new();

    /// <summary>
    /// Creates the channel over the editor's log.
    /// </summary>
    /// <param name="loggerFactory">Where a listener's failure is written.</param>
    public PluginEvents(ILoggerFactory loggerFactory) {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _log = loggerFactory.CreateLogger<PluginEvents>();
    }

    /// <inheritdoc />
    public IPluginSubscription Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : class {
        ArgumentNullException.ThrowIfNull(handler);

        Subscription subscription;
        lock (_gate) {
            if (!_listeners.TryGetValue(typeof(TEvent), out var forType)) {
                forType = [];
                _listeners[typeof(TEvent)] = forType;
            }

            subscription = new Subscription<TEvent>(handler);
            forType.Add(subscription);
        }

        // The removal can only be wired once the subscription exists, which is
        // why it is set here rather than in the constructor.
        subscription.StopOn(() => Remove(typeof(TEvent), subscription));

        return subscription;
    }

    /// <inheritdoc />
    public PluginOutcome Publish<TEvent>(TEvent news)
        where TEvent : class {
        ArgumentNullException.ThrowIfNull(news);

        Subscription[] listeners;
        lock (_gate) {
            listeners = _listeners.TryGetValue(typeof(TEvent), out var forType)
                ? [.. forType]
                : [];
        }

        if (listeners.Length == 0) {
            return PluginOutcome.Ok();
        }

        var failed = 0;
        foreach (var listener in listeners) {
            if (!listener.IsActive) {
                continue;
            }

            try {
                listener.Call(news);
            } catch (Exception error) {
                failed++;
                _log.LogError(
                    error, "A plugin listening for {Event} threw.", typeof(TEvent).Name);
            }
        }

        return failed == 0
            ? PluginOutcome.Ok()
            : PluginOutcome.Failure(
                $"{failed} of {listeners.Length} listeners for {typeof(TEvent).Name} threw.");
    }

    /// <inheritdoc />
    public int ListenerCount<TEvent>()
        where TEvent : class {
        lock (_gate) {
            return _listeners.TryGetValue(typeof(TEvent), out var forType)
                ? forType.Count(listener => listener.IsActive)
                : 0;
        }
    }

    private void Remove(Type newsType, Subscription subscription) {
        lock (_gate) {
            if (_listeners.TryGetValue(newsType, out var forType)) {
                forType.Remove(subscription);
            }
        }
    }

    /// <summary>
    /// One handler, in the form the channel holds it.
    /// </summary>
    private abstract class Subscription : IPluginSubscription {
        private Action? _stop;
        private bool _dropped;

        public bool IsActive => !Volatile.Read(ref _dropped);

        public void Dispose() {
            // Dropping twice is harmless: the second press has nothing left to
            // take out of the list.
            if (Interlocked.Exchange(ref _dropped, true)) {
                return;
            }

            Interlocked.Exchange(ref _stop, null)?.Invoke();
        }

        /// <summary>
        /// Sets what disposing takes out of the list, once the handler is known
        /// well enough for the channel to close the loop.
        /// </summary>
        /// <param name="stop">The removal.</param>
        public void StopOn(Action stop) => _stop = stop;

        public abstract void Call(object news);
    }

    /// <summary>
    /// One handler, with the type it listens for.
    /// </summary>
    /// <typeparam name="TEvent">What the handler listens for.</typeparam>
    private sealed class Subscription<TEvent>(Action<TEvent> handler) : Subscription
        where TEvent : class {
        public override void Call(object news) => handler((TEvent)news);
    }
}
