#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Executable,
    [string]$OutputDirectory,
    [switch]$IsolationConfirmed
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsolationConfirmed) { throw 'Confirm --test-mode / --data-dir integration for this exact executable first.' }
if (-not $Executable) { $Executable = Join-Path $PSScriptRoot '..\src\YDKE.Windows\bin\Release\net10.0-windows10.0.26100.0\win-x64\YDKE.exe' }
$Executable = (Get-Item -LiteralPath $Executable).FullName
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '..\build\native-accessibility' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$results = New-Object 'Collections.Generic.List[object]'
$encoding = New-Object Text.UTF8Encoding($false)

function Assert-A11y([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Wait-A11y([scriptblock]$predicate, [string]$description) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt 18) {
        $matched = & $predicate
        if ($matched) { return }
        Start-Sleep -Milliseconds 150
    }
    throw "Timed out: $description"
}
function New-Profile {
    $folder = Join-Path ([IO.Path]::GetTempPath()) ('ydke-a11y-' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Force -Path $folder
    $settings = @{ SchemaVersion = 1; UiLanguage = 'en'; StudyLanguage = 'en'; Level = 'A1'; DailyGoal = 20; ReduceMotion = $true; UntimedPractice = $true; CloudConnected = $false }
    [IO.File]::WriteAllText((Join-Path $folder 'settings.json'), ($settings | ConvertTo-Json), $encoding)
    return $folder
}
function Start-Ydke([string[]]$arguments) {
    $folder = New-Profile
    $process = Start-Process -FilePath $Executable -ArgumentList (@('--test-mode', "--data-dir=$folder") + $arguments) -PassThru
    try {
        Wait-A11y { $process.Refresh(); -not $process.HasExited -and $process.MainWindowHandle -ne [IntPtr]::Zero } 'owned YDKE window'
        Start-Sleep -Milliseconds 700
        return @{ Process = $process; Folder = $folder; Root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle) }
    }
    catch {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        $process.Dispose(); Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }
}
function Stop-Ydke([hashtable]$app) {
    if (-not $app.Process.HasExited) { $app.Process.CloseMainWindow() | Out-Null; if (-not $app.Process.WaitForExit(2500)) { $app.Process.Kill(); $app.Process.WaitForExit(5000) | Out-Null } }
    $app.Process.Dispose(); Remove-Item -LiteralPath $app.Folder -Recurse -Force -ErrorAction SilentlyContinue
}
function Find-Id([System.Windows.Automation.AutomationElement]$root, [string]$id, [switch]$IncludeOffscreen) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    foreach ($element in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
        if ($element.Current.ProcessId -eq $PID) { continue }
        if ($IncludeOffscreen -or -not $element.Current.IsOffscreen) { return $element }
    }
    return $null
}
function Wait-Id([System.Windows.Automation.AutomationElement]$root, [string]$id) {
    $state = @{ Found = $null }
    Wait-A11y { $state.Found = Find-Id $root $id -IncludeOffscreen; $null -ne $state.Found } $id
    return $state.Found
}
function Invoke-Id([System.Windows.Automation.AutomationElement]$root, [string]$id) {
    $element = Wait-Id $root $id
    $pattern = $null
    Assert-A11y $element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern) "No InvokePattern: $id"
    $pattern.Invoke()
}
function Assert-Element([System.Windows.Automation.AutomationElement]$element, [string]$id, [bool]$HelpRequired = $true, [bool]$AcceleratorRequired = $false) {
    Assert-A11y (-not [string]::IsNullOrWhiteSpace($element.Current.Name)) "$id has no accessible name."
    Assert-A11y (-not [string]::IsNullOrWhiteSpace($element.Current.AutomationId)) "$id has no AutomationId."
    $bounds = $element.Current.BoundingRectangle
    Assert-A11y ($bounds.Width -gt 0 -and $bounds.Height -gt 0) "$id has invalid bounds: offscreen=$($element.Current.IsOffscreen), rect=$bounds"
    if ($HelpRequired) { Assert-A11y (-not [string]::IsNullOrWhiteSpace($element.Current.HelpText)) "$id has no contextual HelpText." }
    if ($AcceleratorRequired) { Assert-A11y (-not [string]::IsNullOrWhiteSpace($element.Current.AcceleratorKey)) "$id has no accelerator metadata." }
    [pscustomobject]@{ id = $id; name = $element.Current.Name; help = $element.Current.HelpText; accelerator = $element.Current.AcceleratorKey }
}
function Run-Scenario([string]$name, [scriptblock]$body) {
    try { $details = @(& $body); $results.Add([pscustomobject]@{ scenario = $name; status = 'PASS'; details = $details }); Write-Output "PASS $name" }
    catch { $results.Add([pscustomobject]@{ scenario = $name; status = 'FAIL'; message = $_.Exception.Message }); Write-Output "FAIL $name :: $($_.Exception.Message)" }
}

Run-Scenario 'quiz-question-choices-feedback' {
    $app = Start-Ydke @('--page=quiz')
    try {
        $start = Wait-Id $app.Root 'quiz.Start'; Assert-Element $start 'quiz.Start' | Out-Null; Invoke-Id $app.Root 'quiz.Start'
        Start-Sleep -Milliseconds 900
        $question = Wait-Id $app.Root 'quiz.Question'; $details = @(Assert-Element $question 'quiz.Question')
        for ($index = 0; $index -lt 4; $index++) {
            $choice = Wait-Id $app.Root ("quiz.Choice.{0}" -f $index); $details += Assert-Element $choice ("quiz.Choice.{0}" -f $index) $true $true
        }
        Invoke-Id $app.Root 'quiz.Choice.0'
        Start-Sleep -Milliseconds 900
        $continue = Wait-Id $app.Root 'quiz.Continue'; $details += Assert-Element $continue 'quiz.Continue'
        $answerDetails = Find-Id $app.Root 'quiz.AnswerDetails'
        if ($null -ne $answerDetails) { $details += Assert-Element $answerDetails 'quiz.AnswerDetails' $true $false }
        $details
    }
    finally { Stop-Ydke $app }
}

Run-Scenario 'cards-reveal-ratings' {
    $app = Start-Ydke @('--page=cards')
    try {
        $start = Wait-Id $app.Root 'cards.Start'; Assert-Element $start 'cards.Start' | Out-Null; Invoke-Id $app.Root 'cards.Start'
        Start-Sleep -Milliseconds 900
        $reveal = Wait-Id $app.Root 'cards.Reveal'; $details = @(Assert-Element $reveal 'cards.Reveal')
        Invoke-Id $app.Root 'cards.Reveal'
        foreach ($rating in @('Again','Hard','Good','Easy')) {
            $id = "cards.Rating.$rating"; $control = Wait-Id $app.Root $id; $details += Assert-Element $control $id $true $true
        }
        $details
    }
    finally { Stop-Ydke $app }
}

foreach ($game in @(
    @{ Id = 'speedround'; ChoicePrefix = 'game.Choice.'; Count = 4 },
    @{ Id = 'memory'; ChoicePrefix = 'game.Card.'; Count = 12 },
    @{ Id = 'sentencescramble'; ChoicePrefix = 'game.Tile.'; Count = 3 },
    @{ Id = 'matrix'; ChoicePrefix = 'game.Cell.'; Count = 36 }
)) {
    Run-Scenario ("game-" + $game.Id + '-choices') {
        $app = Start-Ydke @('--game=' + $game.Id)
        try {
            $question = Wait-Id $app.Root 'game.Question'; $details = @(Assert-Element $question 'game.Question')
            for ($index = 1; $index -le $game.Count; $index++) {
                $id = $game.ChoicePrefix + $index
                $control = Wait-Id $app.Root $id
                $details += Assert-Element $control $id
            }
            $details
        }
        finally { Stop-Ydke $app }
    }
}

$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
$failed = @($results | Where-Object status -eq 'FAIL').Count
"RESULT accessibility passed=$($results.Count - $failed) failed=$failed"
if ($failed -gt 0) { exit 1 }
exit 0
