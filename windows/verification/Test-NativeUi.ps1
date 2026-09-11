#requires -Version 5.1
<#
.SYNOPSIS
Optional interactive Windows UI smoke test against an explicitly supplied build.
.DESCRIPTION
Run in Windows PowerShell 5.1 -NoProfile -STA on an unlocked desktop, NOT CI.
IsolationConfirmed is a mandatory safety acknowledgement: the supplied executable
must already have verified --test-mode/--data-dir integration. An old build can
ignore those arguments and write to the real profile; do not guess.
One fresh temporary profile and one owned process serve the whole suite. Never
attaches to an existing process, uses desktop input, or captures the desktop.
Exit 0 passed, 1 scenario/cleanup failure, 2 blocked/prerequisite failure.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [switch]$IsolationConfirmed,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../build/ui-smoke'),
    [ValidateRange(2, 120)][int]$TimeoutSeconds = 20,
    [switch]$CaptureWindow,
    [switch]$KeepProfile
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$results = [Collections.Generic.List[object]]::new()
$scenarioNames = @('home', 'cards-reveal-rating-undo-position', 'quiz-inline-continue-summary', 'library-results', 'statistics-profile', 'leave-game-dialog')
$script:owned = $null
$script:hwnd = [IntPtr]::Zero
$script:rootElement = $null
$script:profile = $null
$script:pacer = [Threading.ManualResetEvent]::new($false)
$exitCode = 2
$exeHash = $null
$failed = $false

function Add-Result([string]$Name, [string]$Status, [string]$Message, [long]$Milliseconds = 0) {
    $row = [ordered]@{ name = $Name; status = $Status; message = $Message; elapsedMs = $Milliseconds }
    $results.Add($row)
    $row | ConvertTo-Json -Compress | Write-Output
}
function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Target {
    if ($null -eq $script:owned -or $script:owned.HasExited) { throw 'Owned application process exited.' }
    [uint32]$owner = 0
    $null = [YdkeSmoke.Native]::GetWindowThreadProcessId($script:hwnd, [ref]$owner)
    Assert-True ($owner -eq $script:owned.Id) 'HWND no longer belongs to the launched process; refusing interaction.'
}
function Wait-Until([string]$Description, [scriptblock]$Predicate) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $lastError = ''
    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($null -ne $script:owned -and $script:owned.HasExited) { throw "Application exited while waiting for $Description" }
        try { if (& $Predicate) { return } }
        catch { $lastError = $_.Exception.Message }
        # A bounded kernel wait yields the CPU; no busy loop or infinite wait.
        $null = $script:pacer.WaitOne(125)
    }
    throw "Timed out: $Description. $lastError"
}
function Find-Named([string]$Name, $Pattern = $null) {
    Assert-Target
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $elements = $script:rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($element in $elements) {
        if ($element.Current.ProcessId -ne $script:owned.Id) { continue }
        $supported = $null
        if ($null -eq $Pattern -or $element.TryGetCurrentPattern($Pattern, [ref]$supported)) { Write-Output $element }
    }
}
function Get-Button([string]$Name) {
    $buttons = @(Find-Named $Name ([System.Windows.Automation.InvokePattern]::Pattern) | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button })
    if ($buttons.Count -ne 1) { throw "Expected one button '$Name'; got $($buttons.Count)." }
    return $buttons[0]
}
function Test-ButtonEnabled([string]$Name, [bool]$Enabled) {
    return (Get-Button $Name).Current.IsEnabled -eq $Enabled
}
function Invoke-Button([string]$Name) {
    Wait-Until "enabled button '$Name'" { Test-ButtonEnabled $Name $true }
    $button = Get-Button $Name
    Assert-Target
    # ScrollItem is scoped to this HWND's element; never send global keystrokes.
    $scroll = $null
    if ($button.TryGetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern, [ref]$scroll)) { $scroll.ScrollIntoView() }
    ([System.Windows.Automation.InvokePattern]$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
}
function Navigate([string]$Name) {
    Wait-Until "navigation '$Name'" { @(Find-Named $Name ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Count -eq 1 }
    $item = @(Find-Named $Name ([System.Windows.Automation.SelectionItemPattern]::Pattern))[0]
    Assert-Target
    ([System.Windows.Automation.SelectionItemPattern]$item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
}
function Test-Text([string]$Text) {
    return @(Find-Named $Text | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text }).Count -gt 0
}
function Wait-Text([string]$Text) { Wait-Until "text '$Text'" { Test-Text $Text } }
function Test-TextMatch([string]$Expression) {
    Assert-Target
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    foreach ($element in $script:rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
        if ($element.Current.ProcessId -eq $script:owned.Id -and $element.Current.Name -match $Expression) { return $true }
    }
    return $false
}
function Read-Progress {
    # Only this script's GUID temp profile is read. Never inspect the user's profile.
    return Get-Content -LiteralPath (Join-Path $script:profile 'progress.json') -Raw -Encoding UTF8 | ConvertFrom-Json
}
function Get-Session($Progress, [string]$Mode) {
    $property = $Progress.Sessions.PSObject.Properties['en:A1:' + $Mode]
    if ($null -eq $property) { throw "Missing session $Mode" }
    return $property.Value
}
function Get-AnswerCount($Session) { return @($Session.Answers.PSObject.Properties).Count }
function Set-LibraryQuery([string]$Text) {
    $name = 'Search word, meaning, or category'
    Wait-Until 'library search ValuePattern' { @(Find-Named $name ([System.Windows.Automation.ValuePattern]::Pattern)).Count -eq 1 }
    $edit = @(Find-Named $name ([System.Windows.Automation.ValuePattern]::Pattern))[0]
    Assert-Target
    ([System.Windows.Automation.ValuePattern]$edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($Text)
}
function Save-Window([string]$Name) {
    if (-not $CaptureWindow) { return }
    Assert-Target
    $rect = New-Object YdkeSmoke.Native+Rect
    Assert-True ([YdkeSmoke.Native]::GetWindowRect($script:hwnd, [ref]$rect)) 'Cannot read target window bounds.'
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    Assert-True ($width -gt 0 -and $height -gt 0 -and $width -le 12000 -and $height -le 12000) 'Invalid target dimensions.'
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $hdc = $graphics.GetHdc()
        try { $ok = [YdkeSmoke.Native]::PrintWindow($script:hwnd, $hdc, 2) }
        finally { $graphics.ReleaseHdc($hdc) }
        Assert-True $ok 'PrintWindow failed; no desktop fallback is permitted.'
        $bitmap.Save((Join-Path $OutputDirectory ($Name + '.png')), [Drawing.Imaging.ImageFormat]::Png)
        # A successful PrintWindow return is not proof of nonblank/complete pixels.
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

try {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    $null = New-Item -ItemType Directory -Force -Path $OutputDirectory
    Assert-True $IsolationConfirmed.IsPresent 'BLOCKED: verify gated native data-directory integration for this exact build, then explicitly pass -IsolationConfirmed.'
    Assert-True ($PSVersionTable.PSEdition -eq 'Desktop') 'Use Windows PowerShell 5.1 -NoProfile -STA (UI Automation desktop assemblies).'
    Assert-True ([Threading.Thread]::CurrentThread.ApartmentState -eq 'STA') 'STA is required.'
    Assert-True ([Environment]::UserInteractive) 'An interactive unlocked Windows workstation is required.'
    $Executable = (Get-Item -LiteralPath $Executable -ErrorAction Stop).FullName
    Assert-True ([IO.Path]::GetExtension($Executable) -eq '.exe') 'Supply the current native build executable, not a shortcut or package activation URI.'
    $exeHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
    if ($CaptureWindow) { Add-Type -AssemblyName System.Drawing }
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace YdkeSmoke {
  public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  }
}
'@
    $script:profile = Join-Path ([IO.Path]::GetTempPath()) ('ydke-smoke-' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $script:profile
    # Exact native property names; defaults fill other optional settings. A unique
    # daily goal is a visible sanity check before the first learning mutation.
    $settings = @{ SchemaVersion = 1; UiLanguage = 'en'; StudyLanguage = 'en'; Level = 'A1'; DailyGoal = 137; ReduceMotion = $true; UntimedPractice = $true; CloudConnected = $false }
    [IO.File]::WriteAllText((Join-Path $script:profile 'settings.json'), ($settings | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable
    $info.WorkingDirectory = [IO.Path]::GetDirectoryName($Executable)
    $launchArguments = @('--test-mode', ('--data-dir="' + $script:profile + '"'), '--page=home')
    $info.Arguments = $launchArguments -join ' '
    $info.UseShellExecute = $false
    $script:owned = [Diagnostics.Process]::Start($info)
    # Hold the process handle so ownership does not depend on re-querying a name/PID.
    $null = $script:owned.Handle
    Wait-Until 'owned main HWND' {
        $script:owned.Refresh()
        $script:hwnd = $script:owned.MainWindowHandle
        $script:hwnd -ne [IntPtr]::Zero
    }
    Assert-Target
    $script:rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($script:hwnd)
    $exitCode = 1
    $scenarios = @(
        @{ Name = 'home'; Run = {
            Wait-Text 'Which language will you strengthen today?'
            Wait-Text 'Local only'
            Wait-Until 'isolated English settings sentinel' { Test-TextMatch '^Daily goal .*: 0 / 137$' }
            Wait-Until 'home study actions' { @(Find-Named 'Continue studying' ([System.Windows.Automation.InvokePattern]::Pattern)).Count -eq 2 }
        } },
        @{ Name = 'cards-reveal-rating-undo-position'; Run = {
            Navigate 'Cards'; Wait-Text 'Vocabulary Cards'; Invoke-Button 'Start due / new session'
            Wait-Until 'persisted cards session' { (Get-Session (Read-Progress) 'cards').WordKeys.Count -ge 2 }
            $before = Read-Progress; $cards = Get-Session $before 'cards'
            $total = $cards.WordKeys.Count; $script:firstWord = $cards.WordKeys[0].Substring('en:A1:'.Length)
            Wait-Text "1 / $total"
            $good = '3 ' + [char]0xB7 + ' Good'
            Assert-True (Test-ButtonEnabled $good $false) 'Rating must be disabled before reveal.'
            Assert-True (Test-ButtonEnabled 'Next card' $false) 'Unrated card must not be skipped.'
            Invoke-Button 'Reveal meaning'; Wait-Text 'Meaning'; Wait-Text 'Example'
            Invoke-Button $good
            Wait-Until 'one rating saved and position advanced' {
                $p = Read-Progress; $s = Get-Session $p 'cards'
                $s.Index -eq 1 -and (Get-AnswerCount $s) -eq 1 -and $p.CorrectAnswers -eq ($before.CorrectAnswers + 1)
            }
            Wait-Text "2 / $total"; Invoke-Button 'Undo last rating'
            Wait-Until 'undo restored counters and queue' {
                $p = Read-Progress; $s = Get-Session $p 'cards'
                $s.Index -eq 0 -and (Get-AnswerCount $s) -eq 0 -and $p.CorrectAnswers -eq $before.CorrectAnswers
            }
            Wait-Text "1 / $total"; Invoke-Button 'Reveal meaning'; Invoke-Button $good
            Wait-Text "2 / $total"
            Navigate 'Home'; Wait-Text 'Which language will you strengthen today?'
            Navigate 'Cards'; Wait-Text "2 / $total"
            $p = Read-Progress; $s = Get-Session $p 'cards'
            Assert-True ($p.CardPositions.PSObject.Properties['en:A1'].Value -eq $s.WordKeys[1]) 'Saved card position does not match displayed session.'
        } },
        @{ Name = 'quiz-inline-continue-summary'; Run = {
            Navigate 'Quiz'; Wait-Text 'Quick Quiz'; Invoke-Button 'Start due / new session'
            Wait-Until 'persisted quiz options' { (Get-Session (Read-Progress) 'quiz-options-0').WordKeys.Count -ge 2 }
            $initial = Read-Progress; $quiz = Get-Session $initial 'quiz'; $total = $quiz.WordKeys.Count
            Assert-True ($total -ge 2 -and $total -le 8) 'Expected a 2-8 question smoke session.'
            for ($round = 0; $round -lt $total; $round++) {
                Wait-Until 'unanswered quiz' { Test-ButtonEnabled 'Continue' $false }
                $p = Read-Progress; $s = Get-Session $p 'quiz'; $options = Get-Session $p "quiz-options-$round"
                $answerKey = $s.WordKeys[$round]
                $selectedKey = $answerKey
                if ($round -eq 1) { $selectedKey = @($options.WordKeys | Where-Object { $_ -ne $answerKey })[0] }
                $index = [Array]::IndexOf(@($options.WordKeys), $selectedKey)
                $label = '{0}: {1}' -f ($index + 1), $selectedKey.Substring('en:A1:'.Length)
                Invoke-Button $label
                Wait-Text $(if ($round -eq 1) { 'Wrong' } else { 'Correct' })
                Wait-Until 'explicit continue and inline answer' {
                    (Test-ButtonEnabled 'Continue' $true) -and (Test-TextMatch '^Correct answer:') -and (Test-TextMatch '^Your answer:')
                }
                $p = Read-Progress; $s = Get-Session $p 'quiz'
                Assert-True ($s.Index -eq $round) 'Feedback auto-advanced before Continue.'
                Assert-True ((Get-AnswerCount $s) -eq ($round + 1)) 'Quiz answer count differs from rendered feedback.'
                for ($n = 0; $n -lt $options.WordKeys.Count; $n++) {
                    $optionName = '{0}: {1}' -f ($n + 1), $options.WordKeys[$n].Substring('en:A1:'.Length)
                    Assert-True (Test-ButtonEnabled $optionName $false) 'Answered quiz option still enabled.'
                }
                Invoke-Button 'Continue'
                Wait-Until 'quiz index advanced once' { (Get-Session (Read-Progress) 'quiz').Index -eq ($round + 1) }
            }
            Wait-Text 'Session complete'
            Wait-Until 'missed-word practice offered' { Test-ButtonEnabled 'Practice missed words' $true }
            $final = Read-Progress
            Assert-True ($final.CorrectAnswers -eq ($initial.CorrectAnswers + $total - 1) -and $final.WrongAnswers -eq ($initial.WrongAnswers + 1)) 'Correct/wrong totals are not exactly once per quiz answer.'
        } },
        @{ Name = 'library-results'; Run = {
            Navigate 'Words'; Wait-Text 'Word Library'
            Set-LibraryQuery ('no-match-' + [Guid]::NewGuid().ToString('N'))
            Wait-Until 'empty library results' { Test-TextMatch '^Showing / matching / total: 0 / 0 / ' }
            Assert-True (Test-ButtonEnabled 'Practice results (up to 8)' $false) 'Empty results must not start practice.'
            Set-LibraryQuery $script:firstWord
            Wait-Until 'nonempty library results' { Test-TextMatch '^Showing / matching / total: [1-9][\d,\.\s]* / [1-9]' }
            Wait-Until 'matching word row' { @(Find-Named $script:firstWord).Count -gt 0 }
            Assert-True (Test-ButtonEnabled 'Practice results (up to 8)' $true) 'Matching results should allow practice.'
        } },
        @{ Name = 'statistics-profile'; Run = {
            Navigate 'Statistics'
            Wait-Until 'statistics scope' { Test-TextMatch '^Cumulative answer totals' }
            Wait-Text 'Correct'; Wait-Text 'Wrong'; Wait-Text 'Accuracy'
            Wait-Until 'review scope' { Test-TextMatch '^Words last reviewed' }
            Navigate 'Profile'; Wait-Text 'Profile & Settings'; Wait-Text 'Local only'
            Wait-Until 'local backup action' { Test-ButtonEnabled 'Export local backup' $true }
            Wait-Until 'reduced-motion control' { @(Find-Named 'Reduce motion' ([System.Windows.Automation.TogglePattern]::Pattern)).Count -eq 1 }
            $motion = @(Find-Named 'Reduce motion' ([System.Windows.Automation.TogglePattern]::Pattern))[0]
            $toggle = [System.Windows.Automation.TogglePattern]$motion.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            Assert-True ($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) 'Seeded reduced-motion preference was not loaded.'
        } },
        @{ Name = 'leave-game-dialog'; Run = {
            Navigate 'Simple Games'; Invoke-Button 'Hangman'; Invoke-Button 'Start game'; Wait-Text 'Round'
            Navigate 'Home'; Wait-Text 'Leave this game?'; Invoke-Button 'Stay / Cancel'
            Wait-Text 'Round'
            Navigate 'Home'; Wait-Text 'Leave this game?'; Invoke-Button 'Continue'
            Wait-Text 'Which language will you strengthen today?'
            Assert-True (-not (Test-Text 'Leave this game?')) 'Leave confirmation did not close.'
        } }
    )
    foreach ($scenario in $scenarios) {
        if ($failed) { Add-Result $scenario.Name 'skipped' 'Earlier scenario failed; no further mutations.'; continue }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            & $scenario.Run
            Save-Window $scenario.Name
            Add-Result $scenario.Name 'passed' 'Assertions passed.' $watch.ElapsedMilliseconds
        }
        catch { $failed = $true; Add-Result $scenario.Name 'failed' $_.Exception.Message $watch.ElapsedMilliseconds }
    }
    $exitCode = $(if ($failed) { 1 } else { 0 })
}
catch { Add-Result 'setup' 'blocked' $_.Exception.Message; $exitCode = 2 }
finally {
    foreach ($name in $scenarioNames) {
        if (@($results | Where-Object { $_.name -eq $name }).Count -eq 0) { Add-Result $name 'skipped' 'Setup did not complete.' }
    }
    # The process object is ONLY the child created above, never Get-Process by name.
    if ($null -ne $script:owned) {
        try {
            if (-not $script:owned.HasExited) {
                $null = $script:owned.CloseMainWindow()
                if (-not $script:owned.WaitForExit(3000)) {
                    $script:owned.Kill()
                    Assert-True ($script:owned.WaitForExit(5000)) 'Owned process did not exit; retaining its temporary profile.'
                }
            }
        }
        catch { Add-Result 'cleanup' 'failed' $_.Exception.Message; $exitCode = 1; $KeepProfile = $true }
        finally { $script:owned.Dispose() }
    }
    if ($null -ne $script:profile -and -not $KeepProfile) {
        try { Remove-Item -LiteralPath $script:profile -Recurse -Force }
        catch { Add-Result 'profile-cleanup' 'failed' $_.Exception.Message; $exitCode = 1 }
    }
    $script:pacer.Dispose()
    $report = [ordered]@{ schema = 1; kind = 'native-ui-smoke'; executable = $Executable; sha256 = $exeHash; exitCode = $exitCode; profile = $script:profile; profileRetained = [bool]$KeepProfile; scenarios = @($results.ToArray()) }
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
    }
}
exit $exitCode