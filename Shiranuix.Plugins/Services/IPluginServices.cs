namespace Shiranuix.Plugins.Services;

/// <summary>
/// The place a plugin keeps what it offers: a register of its own types, open
/// to every other plugin as well as to the editor.
/// </summary>
/// <remarks>
/// <para>
/// The register is what keeps a plugin from needing the editor to change. A
/// spell-checker that wants to hand other plugins a word list registers it
/// here; a plugin that wants a word list asks for
/// <c>IWordList</c>. Neither of them, nor the editor, has to have heard of the
/// other's name beforehand, and no new door is needed in the contract for the
/// next thing a plugin invents.
/// </para>
/// <para>
/// Two rules keep that from becoming a mess. Registration is first-one-wins:
/// the first thing registered for a type stays, so a later plugin cannot
/// replace a service the editor itself put there or one an earlier plugin
/// already vouched for. And the editor's own doors are registered before any
/// plugin runs and cannot be replaced at all — a plugin asks for
/// <c>IEditorHost</c>, it does not get to bring its own.
/// </para>
/// <para>
/// The same limit as the channel applies here, and for the same reason: each
/// plugin is loaded into a world of its own, so an interface one plugin
/// declares is a different interface to another and the register cannot match
/// them. An interface more than one party must share comes from an assembly the
/// editor shares, which today means this contract.
/// </para>
/// </remarks>
public interface IPluginServices {
    /// <summary>
    /// Offers a service to every plugin in the editor, and to the editor.
    /// </summary>
    /// <typeparam name="TService">
    /// The interface or type it is asked for later — the interface, wherever
    /// there is a choice, because that is what lets two plugins satisfy each
    /// other without sharing a type.
    /// </typeparam>
    /// <param name="service">The service itself.</param>
    /// <exception cref="ArgumentNullException">The service is null.</exception>
    void Register<TService>(TService service)
        where TService : class;

    /// <summary>
    /// Offers a service whose type is only known at run time.
    /// </summary>
    /// <param name="serviceType">The type it is asked for later.</param>
    /// <param name="service">The service itself.</param>
    /// <exception cref="ArgumentNullException">
    /// Either the type or the service is null, or the service is not of the
    /// type it is being registered as.
    /// </exception>
    void Register(Type serviceType, object service);

    /// <summary>
    /// Asks for a service, and says whether one was there.
    /// </summary>
    /// <typeparam name="TService">What to ask for.</typeparam>
    /// <param name="service">
    /// The service, or null when nothing was registered for that type.
    /// </param>
    /// <returns>Whether one was there.</returns>
    bool TryResolve<TService>(out TService? service)
        where TService : class;

    /// <summary>
    /// Asks for a service, or gets nothing.
    /// </summary>
    /// <typeparam name="TService">What to ask for.</typeparam>
    /// <returns>
    /// The service, or null. The same answer as
    /// <see cref="TryResolve{TService}"/>, for the many places where what
    /// matters is the service or nothing.
    /// </returns>
    TService? Resolve<TService>()
        where TService : class;

    /// <summary>
    /// Whether anything has been registered for a type.
    /// </summary>
    /// <typeparam name="TService">What to ask about.</typeparam>
    /// <returns>True when a service is registered for it.</returns>
    bool IsRegistered<TService>()
        where TService : class;
}
