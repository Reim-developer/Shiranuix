namespace Narika.Services.Install;

/// <summary>
/// One application's current release: the number to say out loud and the
/// file to fetch.
/// </summary>
/// <param name="AppId">Which application this release belongs to.</param>
/// <param name="Version">The release number, as its tag spells it.</param>
/// <param name="DownloadUri">Where the archive actually lives.</param>
/// <param name="FileName">What the archive is called once it lands.</param>
public sealed record ReleaseAsset(
    string AppId,
    string Version,
    Uri DownloadUri,
    string FileName);
