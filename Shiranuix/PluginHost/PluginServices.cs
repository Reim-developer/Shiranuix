using Shiranuix.Plugins;
using Shiranuix.Plugins.Services;

namespace Shiranuix.PluginHost;

/// <summary>
/// The register every plugin in the editor shares: what one has offered, what
/// the editor has opened, and what the others can ask for.
/// </summary>
/// <remarks>
/// <para>
/// First one wins, and that is the whole safety story. A plugin asking for a
/// type gets whatever is already registered for it: a plugin cannot displace
/// the editor's own doors, cannot displace a service another plugin vouched
/// for, and cannot displace itself by registering twice without noticing.
/// </para>
/// <para>
/// The editor's doors go in here before any plugin runs, so a plugin looking
/// for <see cref="IEditorHost"/> through the register gets the very instance
/// the capability door hands out. One concept, two ways of asking.
/// </para>
/// </remarks>
internal sealed class PluginServices : IPluginServices {
    private readonly Dictionary<Type, object> _registered = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Puts the editor's own doors in the register.
    /// </summary>
    /// <param name="capabilities">What the editor is offering every plugin.</param>
    public PluginServices(IReadOnlyDictionary<Type, object> capabilities) {
        ArgumentNullException.ThrowIfNull(capabilities);

        foreach (var capability in capabilities) {
            _registered[capability.Key] = capability.Value;
        }
    }

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

        if (!serviceType.IsInstanceOfType(service)) {
            throw new ArgumentException(
                $"A {service.GetType().Name} is not a {serviceType.Name}, so it cannot be "
                + "registered as one.", nameof(service));
        }

        lock (_gate) {
            // First one wins: the register is a shelf, not a slot the last hand
            // reaches past the first one to take.
            _registered.TryAdd(serviceType, service);
        }
    }

    /// <inheritdoc />
    public bool TryResolve<TService>(out TService? service)
        where TService : class {
        lock (_gate) {
            if (_registered.TryGetValue(typeof(TService), out var found)
                && found is TService typed) {
                service = typed;
                return true;
            }
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
        where TService : class {
        lock (_gate) {
            return _registered.ContainsKey(typeof(TService));
        }
    }
}
