function ConvertTo-YdkePackageVersion {
    param([AllowNull()][AllowEmptyString()][string]$Value)

    $parsed = $null
    if ([string]::IsNullOrWhiteSpace($Value) -or
        $Value.Trim() -notmatch '\A[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\z' -or
        -not [Version]::TryParse($Value.Trim(), [ref]$parsed)) {
        throw 'Use a numeric package version: major.minor.build or major.minor.build.0.'
    }
    if ($parsed.Major -lt 1 -or $parsed.Major -gt 255 -or $parsed.Minor -gt 255 -or
        $parsed.Build -gt 65535 -or $parsed.Revision -gt 0) {
        throw 'MSI/Store versions require major 1-255, minor 0-255, build 0-65535 and revision 0.'
    }
    return [Version]::new($parsed.Major, $parsed.Minor, $parsed.Build, 0)
}

function Get-YdkeReleaseHistory {
    param([string]$StatePath, [string[]]$ArtifactDirectories)

    if (Test-Path -LiteralPath $StatePath) {
        $saved = Get-Content -LiteralPath $StatePath -Raw -Encoding utf8
        (ConvertTo-YdkePackageVersion $saved).ToString(4)
    }
    foreach ($directory in ($ArtifactDirectories | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) { continue }
        foreach ($artifact in Get-ChildItem -LiteralPath $directory -File) {
            if ($artifact.Name -match '\AYDKE-(?<version>[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)(?:-(?:x64|arm64|x86)\.(?:msi|msix)|\.msixbundle)\z') {
                (ConvertTo-YdkePackageVersion $Matches['version']).ToString(4)
            }
        }
    }
}

function Resolve-YdkeReleaseVersion {
    param(
        [string]$BaseVersion,
        [AllowNull()][AllowEmptyString()][string]$RequestedVersion,
        [switch]$ExplicitVersion,
        [string[]]$PreviousVersions = @(),
        [DateTimeOffset]$Now = [DateTimeOffset]::UtcNow
    )

    $latest = $null
    foreach ($value in $PreviousVersions) {
        $previous = ConvertTo-YdkePackageVersion $value
        if ($null -eq $latest -or $previous -gt $latest) { $latest = $previous }
    }
    if ($ExplicitVersion) {
        $candidate = ConvertTo-YdkePackageVersion $RequestedVersion
        if ($null -ne $latest -and $candidate -le $latest) {
            throw "Version $candidate must be newer than $latest for an upgrade. Omit -Version to advance automatically."
        }
        return $candidate.ToString(4)
    }

    $base = ConvertTo-YdkePackageVersion $BaseVersion
    $epoch = [DateTimeOffset]::new(2024, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $hours = [Math]::Floor(($Now.ToUniversalTime() - $epoch).TotalHours)
    $build = [int][Math]::Max($base.Build, [Math]::Max(0, [Math]::Min($hours, 65535)))
    $candidate = [Version]::new($base.Major, $base.Minor, $build, 0)
    if ($null -ne $latest -and $candidate -le $latest) {
        if ($latest.Build -lt 65535) {
            $candidate = [Version]::new($latest.Major, $latest.Minor, $latest.Build + 1, 0)
        }
        elseif ($latest.Minor -lt 255) {
            $candidate = [Version]::new($latest.Major, $latest.Minor + 1, 0, 0)
        }
        elseif ($latest.Major -lt 255) {
            $candidate = [Version]::new($latest.Major + 1, 0, 0, 0)
        }
        else {
            throw 'The MSI/Store package version range is exhausted; refusing to reuse a version.'
        }
    }
    return $candidate.ToString(4)
}