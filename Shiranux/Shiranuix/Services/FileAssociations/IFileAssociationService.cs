namespace Shiranuix.Services.FileAssociations;

/// <summary>
/// Whether the editor is standing in the machine's own menus: a
/// <c>.shiranux</c> file named a Shiranuix Document, and a <c>.txt</c> file
/// offering to be opened with Shiranuix.
/// </summary>
/// <remarks>
/// Separate from every other service here because it is the only one that
/// writes to something outside the application and does not write it back.
/// What goes in is decided by <see cref="FileAssociationPlan"/> and can be
/// read off the screen; what comes out is a machine saying yes or no.
/// </remarks>
public interface IFileAssociationService {
    /// <summary>
    /// Writes the association, unless this very build standing in this very
    /// place already has.
    /// </summary>
    void EnsureRegistered();

    /// <summary>
    /// Takes back everything <see cref="EnsureRegistered"/> wrote, leaving the
    /// machine the way it was found.
    /// </summary>
    void Unregister();
}
