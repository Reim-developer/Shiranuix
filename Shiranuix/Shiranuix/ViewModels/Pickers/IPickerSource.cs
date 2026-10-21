namespace Shiranuix.ViewModels.Pickers;

/// <summary>
/// What a picker offers: the items, read when the picker opens rather than
/// held, and a record of a choice so it leads next time.
/// </summary>
/// <typeparam name="T">What is being picked.</typeparam>
/// <remarks>
/// The two pickers differ in what they draw on — installed typefaces versus a
/// ladder of sizes — and not in what they do for the control: offer a list, and
/// remember a choice. This contract is the second half, which is what lets the
/// control and its state stay out of the typing state that owns the list.
/// </remarks>
public interface IPickerSource<T> {
    /// <summary>The items to offer, in the order they should be shown.</summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Records that an item was chosen, so it leads next time.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    void Use(T item);
}
