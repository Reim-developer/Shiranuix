namespace Shiranuix.Models;

/// <summary>
/// The dash choices, in the order the settings window offers them.
/// </summary>
/// <remarks>
/// A picker binds a list, and an enum cannot carry a list of itself - so
/// the list lives here, none first, the short dash next, the long one last:
/// the order the turns grow in.
/// </remarks>
public static class DashModes {
    /// <summary>Every dash, from none to the long one.</summary>
    public static readonly IReadOnlyList<DashMode> All =
        [DashMode.Off, DashMode.En, DashMode.Em];
}
