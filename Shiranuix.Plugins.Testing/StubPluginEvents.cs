using Shiranuix.Plugins;
using Shiranuix.Plugins.Messaging;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// A channel that keeps everything announced over it, so a test can ask what
/// a plugin said and to whom.
/// </summary>
/// <remarks>
/// The same shape as the editor's: handlers in subscription order, a handler
/// that throws is recorded and the rest still run. A plugin that announces to
/// nobody is not a failure here - a test that wants an announcement asserts on
/// <see cref="Announcements"/>.
/// </remarks>
public sealed class StubPluginEvents : IPluginEvents {
    private readonly Dictionary<Type, List<Subscription>> _listeners = [];

    /// <summary>
    /// Everything announced, newest last, whatever the type.
    /// </summary>
    public List<object> Announcements { get; } = [];

    /// <summary>
    /// How many handlers threw when they were called, across every publish.
    /// </summary>
    public int ListenerFailures { get; private set; }

    /// <inheritdoc />
    public IPluginSubscription Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : class {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription<TEvent>(handler);
        subscription.StopOn(() => Remove(typeof(TEvent), subscription));

        if (!_listeners.TryGetValue(typeof(TEvent), out var forType)) {
            forType = [];
            _listeners[typeof(TEvent)] = forType;
        }

        forType.Add(subscription);

        return subscription;
    }

    /// <inheritdoc />
    public PluginOutcome Publish<TEvent>(TEvent news)
        where TEvent : class {
        ArgumentNullException.ThrowIfNull(news);

        Announcements.Add(news);

        Subscription[] listeners;
        lock (_listeners) {
            listeners = _listeners.TryGetValue(typeof(TEvent), out var forType)
                ? [.. forType]
                : [];
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
                ListenerFailures++;
                Failures.Add(error);
            }
        }

        return failed == 0
            ? PluginOutcome.Ok()
            : PluginOutcome.Failure(
                $"{failed} of {listeners.Length} listeners for {typeof(TEvent).Name} threw.");
    }

    /// <summary>Every exception a listener threw, in order.</summary>
    public List<Exception> Failures { get; } = [];

    /// <inheritdoc />
    public int ListenerCount<TEvent>()
        where TEvent : class =>
        _listeners.TryGetValue(typeof(TEvent), out var forType)
            ? forType.Count(listener => listener.IsActive)
            : 0;

    private void Remove(Type newsType, Subscription subscription) {
        if (_listeners.TryGetValue(newsType, out var forType)) {
            forType.Remove(subscription);
        }
    }

    private abstract class Subscription : IPluginSubscription {
        private Action? _stop;
        private bool _dropped;

        public bool IsActive => !_dropped;

        public void Dispose() {
            if (_dropped) {
                return;
            }

            _dropped = true;
            _stop?.Invoke();
        }

        public void StopOn(Action stop) => _stop = stop;

        public abstract void Call(object news);
    }

    private sealed class Subscription<TEvent>(Action<TEvent> handler) : Subscription
        where TEvent : class {
        public override void Call(object news) => handler((TEvent)news);
    }
}
