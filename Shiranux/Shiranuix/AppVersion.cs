namespace Shiranuix;

/// <summary>
/// The application's version: three numbered parts, and the two shapes they
/// are used in.
/// </summary>
/// <remarks>
/// The project file numbers the assembly from the same three — a build
/// whose assembly disagrees with this file is a build made by hand, and
/// both move together on release. Everything that says what is running
/// asks here: these are the constants a title bar, an about line or a bug
/// report compares against.
/// </remarks>
public static class AppVersion {
    /// <summary>
    /// The version so far: the leading zeros say nothing has been promised
    /// to anyone yet, which is the honest state of a first release.
    /// </summary>
    public const int Major = 0;

    /// <summary>
    /// The features added without taking any away.
    /// </summary>
    public const int Minor = 6;

    /// <summary>
    /// The fixes made without changing a feature.
    /// </summary>
    public const int Patch = 4;

    /// <summary>
    /// The same three numbers, in the shape the framework compares them.
    /// </summary>
    public static readonly Version Semantic = new(Major, Minor, Patch);

    /// <summary>
    /// The version shown to a person: "0.6.4".
    /// </summary>
    public static string Text => Semantic.ToString();
}
