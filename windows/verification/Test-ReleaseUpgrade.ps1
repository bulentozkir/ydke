[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$ReleaseDirectory,
    [Parameter(Mandatory)][Version]$PreviousVersion
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\packaging\PackageVersion.ps1')
$releaseVersion = ConvertTo-YdkePackageVersion $Version
$Version = $releaseVersion.ToString(4)
$msiVersion = $releaseVersion.ToString(3)
if (-not $ReleaseDirectory) { $ReleaseDirectory = Join-Path $PSScriptRoot '..\..\releases' }
$ReleaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
if ([Version]$Version -le $PreviousVersion) { throw 'Release version did not increase.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-PackageXml([string]$path, [string]$entryName) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.GetEntry($entryName)
        if (-not $entry) { throw "Missing package manifest: $entryName" }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { return [xml]$reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

function Read-MsiMetadata([string]$Path) {
    $installer = $null; $database = $null; $view = $null
    $properties = @{}; $sequences = @{}
    $upgrades = [Collections.Generic.List[object]]::new()
    $conditions = [Collections.Generic.List[string]]::new()
    $files = [Collections.Generic.List[object]]::new()
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($Path, 0)
        foreach ($table in @('Property', 'Upgrade', 'InstallExecuteSequence', 'LaunchCondition', 'File')) {
            $query = switch ($table) {
                'Property' { 'SELECT `Property`, `Value` FROM `Property`' }
                'Upgrade' { 'SELECT `UpgradeCode`, `VersionMin`, `VersionMax`, `Attributes`, `ActionProperty` FROM `Upgrade`' }
                'InstallExecuteSequence' { 'SELECT `Action`, `Sequence` FROM `InstallExecuteSequence`' }
                'LaunchCondition' { 'SELECT `Condition` FROM `LaunchCondition`' }
                'File' { 'SELECT `FileName`, `Version` FROM `File`' }
            }
            $view = $database.OpenView($query)
            try {
                $view.Execute()
                while ($null -ne ($record = $view.Fetch())) {
                    try {
                        switch ($table) {
                            'Property' { $properties[$record.StringData(1)] = $record.StringData(2) }
                            'Upgrade' {
                                $upgrades.Add([pscustomobject]@{
                                    Code=$record.StringData(1); Minimum=$record.StringData(2); Maximum=$record.StringData(3)
                                    Flags=$record.IntegerData(4); ActionProperty=$record.StringData(5)
                                })
                            }
                            'InstallExecuteSequence' { $sequences[$record.StringData(1)] = $record.IntegerData(2) }
                            'LaunchCondition' { $conditions.Add($record.StringData(1)) }
                            'File' { $files.Add([pscustomobject]@{ Name=$record.StringData(1).Split('|')[-1]; Version=$record.StringData(2) }) }
                        }
                    }
                    finally { $null=[Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) }
                }
            }
            finally { $view.Close(); $null=[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view); $view=$null }
        }
        return [pscustomobject]@{ Properties=$properties; Sequences=$sequences; Upgrades=$upgrades.ToArray(); Conditions=$conditions.ToArray(); Files=$files.ToArray() }
    }
    finally {
        if ($database) { $null=[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) }
        if ($installer) { $null=[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) }
    }
}

$expectedName = 'BulentOzkir.YDKE-YabancDilKelimeEzberleme'
$expectedPublisher = 'CN=06D08AF4-6BB1-40DF-9B96-5DF27BEE0635'
foreach ($architecture in @('x64', 'arm64')) {
    $msix = Join-Path $ReleaseDirectory "YDKE-$Version-$architecture.msix"
    $manifest = Read-PackageXml $msix 'AppxManifest.xml'
    $identity = $manifest.Package.Identity
    if ($identity.Version -ne $Version -or $identity.Name -ne $expectedName -or
        $identity.Publisher -ne $expectedPublisher -or $identity.ProcessorArchitecture -ne $architecture) {
        throw "MSIX identity mismatch: $architecture"
    }
    if ($manifest.Package.Applications.Application.Id -ne 'YDKE' -or $manifest.Package.Applications.Application.Executable -ne 'YDKE.exe') {
        throw "MSIX application identity changed: $architecture"
    }
    $zip = [IO.Compression.ZipFile]::OpenRead($msix)
    try {
        foreach ($required in @('YDKE.exe', 'YDKE.dll', 'YDKE.pri', 'App.xbf', 'MainPage.xbf', 'MainWindow.xbf', 'System.Security.Cryptography.ProtectedData.dll')) {
            if (-not $zip.GetEntry($required)) { throw "Missing runtime payload: $architecture / $required" }
        }
        $signed = $null -ne $zip.GetEntry('AppxSignature.p7x')
    }
    finally { $zip.Dispose() }
    Write-Output "PASS MSIX $architecture identity=$Version runtime-resources=present signed=$signed"

    $msi = Join-Path $ReleaseDirectory "YDKE-$Version-$architecture.msi"
    $metadata = Read-MsiMetadata $msi
    if ($metadata.Properties.ProductVersion -ne $msiVersion -or $metadata.Properties.ALLUSERS -ne '1') {
        throw "MSI version or per-machine scope mismatch: $architecture"
    }
    $expectedCode = '{8F3C1B2E-5D74-4C9A-9E31-7A6B0D2F4C18}'
    if ($metadata.Properties.UpgradeCode -ne $expectedCode -or @($metadata.Upgrades | Where-Object Code -ne $expectedCode).Count -gt 0) {
        throw "MSI upgrade family changed: $architecture"
    }
    $upgrade = @($metadata.Upgrades | Where-Object { $_.Maximum -eq $msiVersion -and $_.Flags -eq 513 -and $_.ActionProperty -eq 'WIX_UPGRADE_DETECTED' })
    $downgrade = @($metadata.Upgrades | Where-Object { $_.Minimum -eq $msiVersion -and $_.Flags -eq 2 -and $_.ActionProperty -eq 'WIX_DOWNGRADE_DETECTED' })
    if ($upgrade.Count -ne 1 -or $downgrade.Count -ne 1 -or @($metadata.Conditions | Where-Object { $_ -match 'NOT WIX_DOWNGRADE_DETECTED' }).Count -eq 0) {
        throw "MSI upgrade/downgrade policy missing: $architecture"
    }
    $sequence = $metadata.Sequences
    if (-not ($sequence.InstallInitialize -lt $sequence.RemoveExistingProducts -and $sequence.RemoveExistingProducts -lt $sequence.ProcessComponents)) {
        throw "MSI removal is outside the rollback-safe transaction position: $architecture"
    }
    $app = @($metadata.Files | Where-Object Name -eq 'YDKE.exe')
    if ($app.Count -ne 1 -or $app[0].Version -ne $Version) { throw "MSI contains a stale executable: $architecture" }
    if (@($metadata.Files | Where-Object { $_.Name -in @('cloudcredentials.json','settings.json','progress.json','crash.log') }).Count -gt 0) {
        throw "MSI must not ship user data: $architecture"
    }
    $oldMsi = Join-Path $ReleaseDirectory "YDKE-$PreviousVersion-$architecture.msi"
    if (Test-Path -LiteralPath $oldMsi) {
        $previous = Read-MsiMetadata $oldMsi
        if ($previous.Properties.UpgradeCode -ne $metadata.Properties.UpgradeCode -or
            $previous.Properties.ProductCode -eq $metadata.Properties.ProductCode -or
            (ConvertTo-YdkePackageVersion $previous.Properties.ProductVersion) -ge $releaseVersion) {
            throw "MSI cannot major-upgrade the previous artifact: $architecture"
        }
    }
    Write-Output "PASS MSI $architecture version=$msiVersion upgrade-inclusive=513 downgrade-guard=2 rollback-sequence=$($sequence.RemoveExistingProducts) executable=$Version"
}
$bundlePath = Join-Path $ReleaseDirectory "YDKE-$Version.msixbundle"
$bundle = Read-PackageXml $bundlePath 'AppxMetadata/AppxBundleManifest.xml'
if ($bundle.Bundle.Identity.Version -ne $Version -or $bundle.Bundle.Identity.Name -ne $expectedName -or
    $bundle.Bundle.Identity.Publisher -ne $expectedPublisher) { throw 'Bundle identity mismatch.' }
$packages = @($bundle.Bundle.Packages.Package)
if ($packages.Count -ne 2 -or @($packages | Where-Object Version -ne $Version).Count -ne 0 -or
    (@($packages.Architecture | Sort-Object) -join ',') -ne 'arm64,x64') { throw 'Bundle architecture/version mismatch.' }
$zip = [IO.Compression.ZipFile]::OpenRead($bundlePath)
try {
    foreach ($package in $packages) {
        $entry = $zip.GetEntry($package.FileName)
        if (-not $entry) { throw 'Bundle is missing its declared inner package.' }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        $standaloneHash = (Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $package.FileName) -Algorithm SHA256).Hash
        if ($hash -ne $standaloneHash) { throw 'Bundle contains an outdated architecture package.' }
    }
}
finally { $zip.Dispose() }
Write-Output "PASS BUNDLE version=$Version architectures=x64,arm64 previous=$PreviousVersion"
Write-Output 'Metadata and payload verification only; no installed application was modified.'