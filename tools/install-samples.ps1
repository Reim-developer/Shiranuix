<#
.SYNOPSIS
    Copies the freshly built sample plugins into the place the editor loads
    plugins from.

.DESCRIPTION
    The editor loads plugins from a folder beside its data - %APPDATA%\Shiranuix
    \plugins by default, or $env:SHIRANUIX_HOME\plugins - and never from the
    build output. So an edited sample plugin does nothing until it is copied
    there, and a copy left behind from an earlier build keeps running: the
    editor cannot tell it is old, so a panel that was fixed in the source is
    still the old one on screen.

    This copies the current build over whatever is there, which is the only
    way the running editor ever matches the source.

.PARAMETER Config
    Debug or Release. The build that gets copied.

.PARAMETER DataHome
    Overrides the folder the editor loads plugins from. Use this to drive a
    throwaway editor: $env:SHIRANUIX_HOME is honoured the same way.

.PARAMETER Samples
    Which sample plugins to install. The file tree is the default; the
    reading-time sample is only there to be read - the smallest whole plugin
    there is - so installing it is a choice:
    .\tools\install-samples.ps1 -Samples file-tree,reading-time

.EXAMPLE
    .\tools\install-samples.ps1
    Copies the Debug build of the file tree into %APPDATA%\Shiranuix\plugins.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Config = 'Debug',

    [string]$DataHome,

    # The sample plugins to install. The file tree is the one a working editor
    # has; reading-time is here to be read - it is the smallest whole plugin
    # there is, and installing it is a choice rather than a default.
    [string[]]$Samples = @('file-tree')
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($DataHome)) {
    $DataHome = if (-not [string]::IsNullOrWhiteSpace($env:SHIRANUIX_HOME)) {
        $env:SHIRANUIX_HOME
    } else {
        Join-Path $env:APPDATA 'Shiranuix'
    }
}

$target = Join-Path $DataHome 'plugins'

$copied = 0

foreach ($sample in $Samples) {
    $built = Join-Path $repository "samples\$sample\bin\$Config\net10.0\$sample.dll"
    if (-not (Test-Path $built)) {
        throw "Not built: $built. Run 'dotnet build' first."
    }

    $folder = Join-Path $target $sample
    New-Item -ItemType Directory -Force -Path $folder | Out-Null

    # Only the assembly and the manifest are replaced. The settings a plugin
    # keeps beside itself - which folder the tree stands on - are the user's,
    # not the build's, and overwriting them would throw away where they were.
    Copy-Item $built $folder -Force
    Copy-Item (Join-Path $repository "samples\$sample\manifest.json") $folder -Force

    $stamp = (Get-Item (Join-Path $folder "$sample.dll")).LastWriteTime
    Write-Host ("{0,-14} {1}  {2}" -f $sample, $stamp, $folder)
    $copied++
}

Write-Host ""
Write-Host "$copied sample plugin(s) installed into $target."
Write-Host "Restart the editor for it to load them."