using CommunityToolkit.Mvvm.ComponentModel;

namespace Narika.ViewModels;

/// <summary>
/// The base every view model in this application is built on.
/// </summary>
/// <remarks>
/// The editor carries a forwarding base for its state objects; the
/// installer keeps its state in its view models directly, so all this
/// needs to be is the observable base under them — one name to say that
/// anything here is bound to.
/// </remarks>
public abstract class ViewModelBase : ObservableObject;
