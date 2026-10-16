#if NETSTANDARD2_0

namespace System.Runtime.CompilerServices;

/// <summary>
/// The compiler's marker for a member that may only be assigned while an
/// object is being built. It ships with .NET 5 and later, and is what the
/// records in this package compile against; this copy exists only so the
/// same sources build for netstandard2.0. Nothing here is public and nothing
/// here has behaviour.
/// </summary>
internal static class IsExternalInit {
}

#endif
