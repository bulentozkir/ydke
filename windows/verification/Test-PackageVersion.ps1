#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$packaging = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\packaging'))
. (Join-Path $packaging 'PackageVersion.ps1')
$script:passed = 0
$script:failed = 0

function Assert-Version([string]$Expected, [string]$Actual) {
    if ($Expected -cne $Actual) { throw "Expected $Expected, got $Actual" }
}
function Assert-Rejected([scriptblock]$Action, [string]$MessagePattern = '.') {
    try { $null = & $Action }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) { throw }
        return
    }
    throw 'The invalid release was accepted.'
}
function Test-Case([string]$Name, [scriptblock]$Action) {
    try { & $Action; $script:passed++; Write-Output "PASS $Name" }
    catch { $script:failed++; Write-Output "FAIL ${Name}: $($_.Exception.Message)" }
}

$clock = [DateTimeOffset]::new(2024, 1, 1, 0, 0, 0, [TimeSpan]::Zero).AddHours(23586)
Test-Case 'Three-part versions normalize to a Store-compatible revision' {
    Assert-Version '1.2.3.0' (ConvertTo-YdkePackageVersion ' 1.2.3 ').ToString(4)
}
Test-Case 'Automatic version starts from the clock without lowering the project build' {
    Assert-Version '1.0.23586.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -Now $clock)
    Assert-Version '1.0.30000.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.30000' -Now $clock)
}
Test-Case 'Repeated builds in one hour always increase the MSI-compared component' {
    $previous = '1.0.23586.0'
    for ($index = 0; $index -lt 25; $index++) {
        $next = Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @($previous) -Now $clock
        if ([Version]$next -le [Version]$previous -or ([Version]$next).Revision -ne 0) { throw 'Version collided.' }
        $previous = $next
    }
    Assert-Version '1.0.23611.0' $previous
}
Test-Case 'Clock rollback cannot downgrade a release' {
    Assert-Version '1.0.23587.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('1.0.23586.0') -Now $clock.AddDays(-1))
}
Test-Case 'History comparisons use the whole version, not just the build' {
    Assert-Version '2.3.5.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('1.9.60000.0','2.3.4.0') -Now $clock)
    Assert-Version '3.0.23586.0' (Resolve-YdkeReleaseVersion -BaseVersion '3.0.0' -PreviousVersions @('2.3.60000.0') -Now $clock)
}
Test-Case 'A higher explicit release is normalized and accepted' {
    Assert-Version '2.0.1.0' (Resolve-YdkeReleaseVersion -ExplicitVersion -RequestedVersion '2.0.1' -PreviousVersions @('1.0.23586.0'))
}
Test-Case 'Same or older explicit releases fail instead of producing a non-upgrade' {
    foreach ($version in @('1.0.23586', '1.0.23586.0', '1.0.23585.0', '1.0.0', '1.0.1.9')) {
        Assert-Rejected { Resolve-YdkeReleaseVersion -ExplicitVersion -RequestedVersion $version -PreviousVersions @('1.0.23586.0') }
    }
}
Test-Case 'Invalid, overflowed and revision-only versions are rejected' {
    foreach ($version in @($null, '', ' ', '1', '1.2', '1.2.3.4.5', '1.0.0-beta', '-1.0.0', '1..0', '0.0.0',
        '256.0.0', '1.256.0', '1.0.65536', '99999999999999999999.0.0', '1.2.3.1')) {
        Assert-Rejected { ConvertTo-YdkePackageVersion $version }
    }
}
Test-Case 'Empty explicit versions do not silently fall back to auto-versioning' {
    Assert-Rejected { Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -ExplicitVersion -RequestedVersion '' }
}
Test-Case 'Build and minor overflow advance the next supported component' {
    Assert-Version '1.3.0.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('1.2.65535.0') -Now $clock)
    Assert-Version '2.0.0.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('1.255.65535.0') -Now $clock)
    Assert-Rejected { Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('255.255.65535.0') -Now $clock }
}
Test-Case 'Clocks beyond the build range never wrap or reuse a version' {
    $future = $clock.AddYears(20)
    Assert-Version '1.0.65535.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -Now $future)
    Assert-Version '1.1.0.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('1.0.65535.0') -Now $future)
}
Test-Case 'Corrupt release history fails closed' {
    Assert-Rejected { Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions @('corrupt') -Now $clock }
}

$scratch = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\build'))) ('version-test-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $scratch
try {
    $state = Join-Path $scratch 'version.txt'
    Set-Content -LiteralPath $state -Value '1.0.23580.0'
    $artifact = Join-Path $scratch 'YDKE-1.0.23586.0-x64.msi'
    Set-Content -LiteralPath $artifact -Value 'sentinel: do not remove'
    Set-Content -LiteralPath (Join-Path $scratch 'OtherApp-255.0.0.0-x64.msi') -Value 'unrelated'
    Test-Case 'Existing YDKE artifacts protect against stale or missing version markers' {
        $history = @(Get-YdkeReleaseHistory -StatePath $state -ArtifactDirectories @($scratch))
        Assert-Version '1.0.23587.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions $history -Now $clock)
        $history = @(Get-YdkeReleaseHistory -StatePath (Join-Path $scratch 'missing.txt') -ArtifactDirectories @($scratch))
        Assert-Version '1.0.23587.0' (Resolve-YdkeReleaseVersion -BaseVersion '1.0.0' -PreviousVersions $history -Now $clock)
    }
    Test-Case 'Rejected release runs preserve existing artifacts and the real version marker' {
        $markerPath = Join-Path $packaging '.last-package-version.txt'
        $marker = Get-Content -LiteralPath $markerPath -Raw
        Assert-Rejected { & (Join-Path $packaging 'release.ps1') -Version '1.0.0' -OutputDir $scratch } 'must be newer than'
        Assert-Version 'sentinel: do not remove' (Get-Content -LiteralPath $artifact -Raw).Trim()
        Assert-Version $marker (Get-Content -LiteralPath $markerPath -Raw)
    }
    Test-Case 'Invalid format selections stop before touching existing installers' {
        Assert-Rejected { & (Join-Path $packaging 'release.ps1') -SkipMsi -SkipMsix -OutputDir $scratch } 'At least one installer format'
        Assert-Rejected { & (Join-Path $packaging 'release.ps1') -Runtime 'win-x86' -SkipMsix -OutputDir $scratch } 'MSI releases require'
        Assert-Version 'sentinel: do not remove' (Get-Content -LiteralPath $artifact -Raw).Trim()
    }
}
finally { Remove-Item -LiteralPath $scratch -Recurse -Force }

Test-Case 'MSI-only builds republish and commit version history after packaging succeeds' {
    $source = Get-Content -LiteralPath (Join-Path $packaging 'release.ps1') -Raw
    if ($source -notmatch 'if \(\$SkipMsix -or -not \(Test-Path') { throw 'MSI-only build can reuse stale payload.' }
    if ($source.IndexOf('Move-Item -LiteralPath $versionTemporary') -lt $source.IndexOf('& $wix build')) { throw 'Version marker is committed too early.' }
    if ($source -notmatch '\$msiVersion = \(\[Version\]\$packageVersion\)\.ToString\(3\)') { throw 'MSI must receive a three-part version.' }
    if ($source -notmatch "\[string\[\]\]\`$Runtime = @\('win-x64', 'win-arm64'\)") { throw 'Default release does not include the bundle architectures.' }
    $wxs = [xml](Get-Content -LiteralPath (Join-Path $packaging 'msi\Product.wxs') -Raw)
    Assert-Version 'afterInstallInitialize' $wxs.Wix.Package.MajorUpgrade.Schedule
    Assert-Version 'yes' $wxs.Wix.Package.MajorUpgrade.AllowSameVersionUpgrades
}

Write-Output "RESULT VERSION passed=$script:passed failed=$script:failed"
if ($script:failed -gt 0) { exit 1 }
exit 0