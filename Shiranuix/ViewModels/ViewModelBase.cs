using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Shiranuix.ViewModels;

/// <summary>
/// The base every view model in this application is built on.
/// </summary>
/// <remarks>
/// A compiled binding takes its property name from the path's first segment -
/// the view model it is bound to - so a state's changes are only heard by the
/// view if the view model raises them too. <see cref="Expose{TState}"/> is how
/// that forwarding is said, once, rather than as a handler per state.
/// </remarks>
public abstract class ViewModelBase : ObservableObject {
    /// <summary>
    /// Raises this view model's own versions of the given state's changes, so a
    /// view bound here hears them.
    /// </summary>
    /// <typeparam name="TState">The state's type.</typeparam>
    /// <param name="state">The state to forward.</param>
    /// <param name="derived">
    /// Called after each change, for the flags and labels computed from it.
    /// </param>
    /// <remarks>
    /// The state's names are forwarded as they are, so the view model's doors
    /// carry the same names the state does — a door named differently is a
    /// change nobody hears.
    /// </remarks>
    protected void Expose<TState>(TState state, Action<string?>? derived = null)
        where TState : INotifyPropertyChanged {
        ArgumentNullException.ThrowIfNull(state);

        state.PropertyChanged += (_, e) => {
            if (e.PropertyName is not null) {
                OnPropertyChanged(e.PropertyName);
            }

            derived?.Invoke(e.PropertyName);
        };
    }
}
