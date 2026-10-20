namespace Narika.Services.Install;

/// <summary>
/// One note from the install service while the work runs: how far along it
/// is, in the words it says it in.
/// </summary>
/// <param name="Percent">
/// How much is done, nought to a hundred. A negative number says the work
/// has begun but cannot honestly name a fraction yet — the bar runs
/// indeterminate rather than lying about a position it does not have.
/// </param>
/// <param name="Message">What the service is doing right now.</param>
public sealed record InstallProgress(int Percent, string Message);
