using Shiranuix.Plugins;
using Shiranuix.Plugins.Services;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// A register that behaves like the editor's, and writes down what was offered
/// on it.
/// </summary>
/// <remarks>
/// First one wins here too, because that is part of the contract rather than
/// part of the editor: a plugin that cannot displace another is a promise a
/// test is allowed to lean on.
/// </remarks>
public sealed class StubPluginServices : IPluginServices {
    private readonly Dictionary<Type, object> _registered = [];

    /// <summary>Every type something was offered under, in order.</summary>
    public List<Type> Offerings { get; } = [];

    /// <inheritdoc />
    public void Register<TService>(TService service)
        where TService : class {
        ArgumentNullException.ThrowIfNull(service);

        Register(typeof(TService), service);
    }

    /// <inheritdoc />
    public void Register(Type serviceType, object service) {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(service);

        if (_registered.TryAdd(serviceType, service)) {
            Offerings.Add(serviceType);
        }
    }

    /// <inheritdoc />
    public bool TryResolve<TService>(out TService? service)
        where TService : class {
        if (_registered.TryGetValue(typeof(TService), out var found) && found is TService typed) {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    /// <inheritdoc />
    public TService? Resolve<TService>()
        where TService : class =>
        TryResolve(out TService? service) ? service : null;

    /// <inheritdoc />
    public bool IsRegistered<TService>()
        where TService : class =>
        _registered.ContainsKey(typeof(TService));

    /// <summary>
    /// The editor's own doors, put in before a plugin runs.
    /// </summary>
    /// <param name="doors">The doors, keyed by the interface asked for.</param>
    /// <returns>The same register, so calls can be chained.</returns>
    public StubPluginServices WithEditorDoors(params (Type Door, object Door2)[] doors) {
        foreach (var (door, instance) in doors) {
            Register(door, instance);
        }

        return this;
    }
}
