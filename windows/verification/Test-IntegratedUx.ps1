#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Executable,
    [string]$OutputDirectory,
    [switch]$IsolationConfirmed,
    [switch]$CompileOnly,
    [switch]$GamesOnly,
    [string]$GameStartAt,
    [switch]$HelpOnly,
    [ValidateSet('tr','en','de','fr','es','pt','nl')]
    [string]$UiLanguage = 'en',
    [ValidateRange(0.85,1.4)]
    [double]$HelpFontScale = 1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($HelpOnly -and ($GamesOnly -or $GameStartAt)) { throw 'Choose HelpOnly or game scenarios, not both.' }
if (-not $HelpOnly -and ($UiLanguage -ne 'en' -or $HelpFontScale -ne 1)) { throw 'UiLanguage and HelpFontScale overrides require HelpOnly.' }
if (-not $Executable) { $Executable = Join-Path $PSScriptRoot '..\src\YDKE.Windows\bin\Release\net10.0-windows10.0.26100.0\win-x64\YDKE.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '..\build\ui-integrated' }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase, System.Drawing, System.Security
Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

public sealed class UxSignal : IDisposable {
    public readonly AutoResetEvent Changed = new AutoResetEvent(false);
    readonly AutomationElement root;
    readonly StructureChangedEventHandler structure;
    readonly AutomationPropertyChangedEventHandler property;
    public UxSignal(AutomationElement root) {
        this.root = root;
        structure = delegate { Notify(); };
        property = delegate { Notify(); };
        Automation.AddStructureChangedEventHandler(root, TreeScope.Subtree, structure);
        Automation.AddAutomationPropertyChangedEventHandler(root, TreeScope.Subtree, property,
            AutomationElement.IsEnabledProperty, AutomationElement.IsOffscreenProperty,
            AutomationElement.NameProperty, AutomationElement.HelpTextProperty, AutomationElement.ItemStatusProperty,
            AutomationElement.BoundingRectangleProperty);
    }
    void Notify() { try { Changed.Set(); } catch (ObjectDisposedException) { } }
    public void Dispose() {
        Automation.RemoveStructureChangedEventHandler(root, structure);
        Automation.RemoveAutomationPropertyChangedEventHandler(root, property);
        Changed.Dispose();
    }
}
public static class UxNative {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MSG {
        public IntPtr Hwnd; public uint Message; public UIntPtr WParam; public IntPtr LParam;
        public uint Time; public POINT Point; public uint Private;
    }
    delegate void WinEvent(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
    delegate bool ThreadWindow(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flag);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, ThreadWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEvent callback, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG message, IntPtr hwnd, uint first, uint last);
    [DllImport("user32.dll")] static extern bool PeekMessage(out MSG message, IntPtr hwnd, uint first, uint last, uint flags);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmFlush();
    public static void Own(IntPtr hwnd, int pid) {
        uint owner;
        if (hwnd == IntPtr.Zero || GetWindowThreadProcessId(hwnd, out owner) == 0 || owner != pid)
            throw new InvalidOperationException("Window does not belong to the launched process.");
    }
    public static IntPtr WaitWindow(int pid) {
        IntPtr found = IntPtr.Zero; Exception failure = null; uint workerId = 0;
        var worker = new Thread(delegate() {
            IntPtr hook = IntPtr.Zero;
            try {
                workerId = GetCurrentThreadId(); MSG message;
                PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
                Action<IntPtr> consider = delegate(IntPtr hwnd) {
                    uint owner; RECT r;
                    if (hwnd != IntPtr.Zero && GetWindowThreadProcessId(hwnd, out owner) != 0 && owner == pid &&
                        IsWindowVisible(hwnd) && GetAncestor(hwnd, 2) == hwnd && GetWindowRect(hwnd, out r) &&
                        r.Right - r.Left >= 300 && r.Bottom - r.Top >= 200) { found = hwnd; PostQuitMessage(0); }
                };
                WinEvent handler = delegate(IntPtr h, uint k, IntPtr hwnd, int o, int c, uint t, uint time) {
                    if (o == 0 && c == 0) consider(hwnd);
                };
                hook = SetWinEventHook(0x8000, 0x8002, IntPtr.Zero, handler, (uint)pid, 0, 0);
                if (hook == IntPtr.Zero) throw new InvalidOperationException("Window event subscription failed.");
                using (var process = Process.GetProcessById(pid))
                    foreach (ProcessThread thread in process.Threads) {
                        EnumThreadWindows((uint)thread.Id, delegate(IntPtr hwnd, IntPtr p) { consider(hwnd); return found == IntPtr.Zero; }, IntPtr.Zero);
                        if (found != IntPtr.Zero) break;
                    }
                while (found == IntPtr.Zero && GetMessage(out message, IntPtr.Zero, 0, 0) > 0) { }
                GC.KeepAlive(handler);
            } catch (Exception ex) { failure = ex; }
            finally { if (hook != IntPtr.Zero) UnhookWinEvent(hook); }
        });
        worker.IsBackground = true; worker.SetApartmentState(ApartmentState.MTA); worker.Start();
        if (!worker.Join(15000)) {
            if (workerId != 0) PostThreadMessage(workerId, 0x0012, UIntPtr.Zero, IntPtr.Zero);
            throw new TimeoutException("No owned application window appeared.");
        }
        if (failure != null) throw failure;
        Own(found, pid); return found;
    }
    public static void Resize(IntPtr hwnd, int pid, int dips) {
        Own(hwnd, pid); RECT r;
        if (!GetWindowRect(hwnd, out r) || !SetWindowPos(hwnd, IntPtr.Zero, 0, 0,
            (int)Math.Round(dips * GetDpiForWindow(hwnd) / 96.0), r.Bottom-r.Top, 0x0002 | 0x0004 | 0x0010))
            throw new InvalidOperationException("Owned-window resize failed.");
    }
    public static void Capture(IntPtr hwnd, int pid, string path) {
        Own(hwnd, pid); RECT r;
        if (!GetWindowRect(hwnd, out r)) throw new InvalidOperationException("Window bounds unavailable.");
        int width = r.Right-r.Left, height = r.Bottom-r.Top;
        if (width <= 0 || height <= 0 || width > 12000 || height > 12000) throw new InvalidOperationException("Invalid capture dimensions.");
        DwmFlush();
        using (var image = new Bitmap(width, height)) using (var graphics = Graphics.FromImage(image)) {
            IntPtr dc = graphics.GetHdc(); bool ok;
            try { ok = PrintWindow(hwnd, dc, 2); } finally { graphics.ReleaseHdc(dc); }
            if (!ok) throw new InvalidOperationException("PrintWindow failed; desktop fallback prohibited.");
            image.Save(path, ImageFormat.Png);
        }
    }
}
'@ -ReferencedAssemblies @('System.dll', 'System.Core.dll', [Drawing.Bitmap].Assembly.Location, [System.Windows.Rect].Assembly.Location, [System.Windows.Automation.AutomationElement].Assembly.Location, [System.Windows.Automation.AutomationEvent].Assembly.Location)

if ($CompileOnly) { Write-Output 'UX_PROBE_COMPILED'; exit 0 }
if (-not $IsolationConfirmed) { throw 'Confirm --test-mode / --data-dir integration for this exact executable first.' }
$exe = (Get-Item -LiteralPath $Executable).FullName
$output = [IO.Path]::GetFullPath($OutputDirectory)
$testFolder = Join-Path $output ([Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testFolder -Force
$encoding = New-Object Text.UTF8Encoding($false)
$seed = @{ UiLanguage=$UiLanguage; StudyLanguage='en'; Level='A1'; DailyGoal=137; ReduceMotion=$true; UntimedPractice=$true; FontScale=$HelpFontScale }
[IO.File]::WriteAllText((Join-Path $testFolder 'settings.json'), ($seed | ConvertTo-Json), $encoding)
# Simulated identity checks only the status UI; it is not a Google authentication claim.
$testToken = [Text.Encoding]::UTF8.GetBytes('test-refresh-not-a-real-session')
$protectedToken = [Convert]::ToBase64String([Security.Cryptography.ProtectedData]::Protect($testToken, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser))
$identity = @{ ProtectedFirebaseRefreshToken=$protectedToken; FirebaseUid='test-uid'; DisplayName='Test account'; GoogleBrowser=0 }
[IO.File]::WriteAllText((Join-Path $testFolder 'cloudcredentials.json'), ($identity | ConvertTo-Json), $encoding)
$results = New-Object 'Collections.Generic.List[object]'
$owned = $null; $signal = $null; $rootElement = $null; $hwnd = [IntPtr]::Zero
$ownedPid = 0; $unexpectedExit = $null
$exitCode = 1
$dpiContext = [UxNative]::SetThreadDpiAwarenessContext([IntPtr](-4))

function Assert-Ux([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Wait-Ux([string]$description, [scriptblock]$predicate) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $last = ''
    while ($true) {
        if ($owned.HasExited) { throw "App exited while waiting for $description" }
        [UxNative]::Own($hwnd, $owned.Id)
        try { if (& $predicate) { return } } catch { $last = $_.Exception.Message }
        $remaining = 18000 - [int]$watch.ElapsedMilliseconds
        if ($remaining -le 0) { throw "Timed out: $description. $last" }
        $null = $signal.Changed.WaitOne([Math]::Min(250, $remaining))
    }
}
function Find-Ux([string]$value, [switch]$ByName, [switch]$IncludeOffscreen, [switch]$Action) {
    $property = if ($ByName) { [System.Windows.Automation.AutomationElement]::NameProperty } else { [System.Windows.Automation.AutomationElement]::AutomationIdProperty }
    $condition = [System.Windows.Automation.PropertyCondition]::new($property, $value)
    foreach ($element in $rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
        if ($element.Current.ProcessId -ne $owned.Id -or (-not $IncludeOffscreen -and $element.Current.IsOffscreen)) { continue }
        if ($Action) {
            $pattern = $null
            if (-not $element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern) -and
                -not $element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { continue }
        }
        return $element
    }
    return $null
}
function Invoke-Ux([string]$value, [switch]$ByName) {
    $target = @{ Element = $null }
    Wait-Ux "available action $value" {
        $target.Element = Find-Ux $value -ByName:$ByName -IncludeOffscreen -Action
        $null -ne $target.Element -and $target.Element.Current.IsEnabled
    }
    $candidate = $target.Element
    if ($candidate.Current.IsOffscreen) {
        $scrollItem = $null
        if ($candidate.TryGetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern, [ref]$scrollItem)) { $scrollItem.ScrollIntoView() }
        else { $candidate.SetFocus() }
    }
    Wait-Ux "action $value" {
        $target.Element = Find-Ux $value -ByName:$ByName -Action
        $null -ne $target.Element -and $target.Element.Current.IsEnabled
    }
    $element = $target.Element
    [UxNative]::Own($hwnd, $owned.Id)
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        Wait-Ux "selected $value" { $pattern.Current.IsSelected }
        return
    }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return }
    throw "No invoke/selection pattern: $value"
}
function Wait-Id([string]$id) { Wait-Ux $id { $null -ne (Find-Ux $id) } }
function Assert-NoPageOverflow([string]$name) {
    $state = @{ Text = '' }
    Wait-Ux "$name page fit measurement" {
        $viewport = Find-Ux 'shell.ContentScroll' -IncludeOffscreen
        if ($null -eq $viewport) { return $false }
        $state.Text = $viewport.Current.HelpText
        $state.Text -match 'horizontal fit=(True|False); vertical fit=(True|False)$'
    }
    Assert-Ux ($state.Text.EndsWith('horizontal fit=True; vertical fit=True')) "$name exceeds its fixed viewport: $($state.Text)"
}
function Invoke-GameCatalogItem([string]$name) {
    Assert-NoPageOverflow ($name + ' catalog')
    for ($rewind = 0; $rewind -lt 10; $rewind++) {
        $previous = Find-Ux 'games.PreviousPage' -IncludeOffscreen -Action
        if ($null -eq $previous -or -not $previous.Current.IsEnabled) { break }
        $pageState = @{ Text = '' }
        Wait-Ux 'current game catalog page' { $s=Find-Ux 'games.CatalogPage' -IncludeOffscreen; if($null -eq $s){return $false}; $pageState.Text=$s.Current.Name; $true }
        $before = $pageState.Text
        Invoke-Ux 'games.PreviousPage'
        Wait-Ux 'previous game catalog page' { $s=Find-Ux 'games.CatalogPage' -IncludeOffscreen; $null -ne $s -and $s.Current.Name -ne $before }
    }
    for ($page = 0; $page -lt 10; $page++) {
        $target = Find-Ux $name -ByName -IncludeOffscreen -Action
        if ($null -ne $target -and $target.Current.IsEnabled) { Invoke-Ux $name -ByName; return }
        $next = Find-Ux 'games.NextPage' -IncludeOffscreen -Action
        if ($null -eq $next -or -not $next.Current.IsEnabled) { break }
        $pageState = @{ Text = '' }
        Wait-Ux 'current game catalog page' { $s=Find-Ux 'games.CatalogPage' -IncludeOffscreen; if($null -eq $s){return $false}; $pageState.Text=$s.Current.Name; $true }
        $before = $pageState.Text
        Invoke-Ux 'games.NextPage'
        Wait-Ux 'next game catalog page' { $s=Find-Ux 'games.CatalogPage' -IncludeOffscreen; $null -ne $s -and $s.Current.Name -ne $before }
        Assert-NoPageOverflow ($name + ' catalog page')
    }
    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $names = @($rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition) | Where-Object {
        $_.Current.ProcessId -eq $owned.Id -and -not $_.Current.IsOffscreen
    } | ForEach-Object { $_.Current.Name } | Where-Object { $_ }) -join ' | '
    $catalog = Find-Ux 'games.CatalogPage' -IncludeOffscreen
    $catalogName = if ($null -eq $catalog) { '<missing>' } else { $catalog.Current.Name }
    throw "Game is not reachable through catalog pages: $name. Page=$catalogName. Buttons=$names"
}
function Export-UxImage([string]$name) {
    $path = Join-Path $testFolder ($name + '.png')
    $windowPattern = $null
    if ($rootElement.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$windowPattern)) {
        if ($windowPattern.Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Minimized) {
            $windowPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
            Wait-Ux 'owned window restored for capture' { $rootElement.Current.BoundingRectangle.Height -gt 200 }
        }
        $null=$windowPattern.WaitForInputIdle(5000)
    }
    [UxNative]::Capture($hwnd, $owned.Id, $path)
    Write-Output "SCREENSHOT=$path"
}
function Add-UxResult([string]$name, [string]$status='PASS') {
    $results.Add([pscustomobject]@{ scenario=$name; status=$status })
    Write-Output "$status $name"
}
function Read-TestProgress { return Get-Content -LiteralPath (Join-Path $testFolder 'progress.json') -Raw | ConvertFrom-Json }
function Set-UxText([string]$id, [string]$text) {
    $target = @{ Element = $null }
    Wait-Ux "attached text editor $id" {
        $target.Element = Find-Ux $id -IncludeOffscreen
        $null -ne $target.Element
    }
    $control = $target.Element
    $value = $null
    if ($control.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value)) { $value.SetValue($text); return }
    $edits = $control.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit))
    foreach ($edit in $edits) {
        if ($edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$value)) { $value.SetValue($text); return }
    }
    throw "No text editor: $id"
}

function Test-HelpTopics {
    # Read the real resource row for this language; do not maintain a second set of translations.
    $column = [Array]::IndexOf([string[]]@('en','tr','de','fr','es','pt','nl'), $UiLanguage) + 1
    $resources = @{}
    $resourcePath = Join-Path $PSScriptRoot '..\src\YDKE.Windows\ExperienceStrings.cs'
    foreach ($line in Get-Content -LiteralPath $resourcePath -Encoding UTF8) {
        if ($line -notmatch '^\s*[A-Za-z][A-Za-z0-9.]*\|') { continue }
        $cells = $line.Trim().Split('|')
        Assert-Ux ($cells.Count -eq 8) 'Malformed localization row in the Help runtime oracle.'
        $resources.Add($cells[0], $cells[$column])
    }
    function Element([string]$id, [switch]$ByName) {
        $target = @{ Value = $null }
        Wait-Ux "Help element $id" { $target.Value=Find-Ux $id -ByName:$ByName; $null -ne $target.Value }
        return $target.Value
    }
    function Inside([string]$id, [switch]$ByName) {
        $element = Element $id -ByName:$ByName
        $rect = $element.Current.BoundingRectangle
        $window = $rootElement.Current.BoundingRectangle
        Assert-Ux ($rect.Width -gt 0 -and $rect.Height -gt 0 -and
            $rect.Left -ge $window.Left - 2 -and $rect.Right -le $window.Right + 2 -and
            $rect.Top -ge $window.Top - 2 -and $rect.Bottom -le $window.Bottom + 2) "Help control is outside the window: $id"
    }
    $topics = @(
        @{ Id='Screen'; Title='Help.ScreenTitle'; Text=@('Help.Body','Help.Shortcuts') },
        @{ Id='Cards'; Title='Nav.Cards'; Text=@('Kids.Help.Cards') },
        @{ Id='Quiz'; Title='Nav.Quiz'; Text=@('Kids.Help.Quiz') },
        @{ Id='Words'; Title='Words.Title'; Text=@('Kids.Help.Words') },
        @{ Id='Games'; Title='Help.Games'; Text=@('Kids.Help.Games','Help.Availability') },
        @{ Id='Settings'; Title='Profile.Title'; Text=@('Help.Settings') },
        @{ Id='Statistics'; Title='Stats.Title'; Text=@('Help.Statistics') },
        @{ Id='Cloud'; Title='Help.CloudTitle'; Text=@('Help.Google','Help.CloudBackup','Kids.Help.Backups') }
    )
    Wait-Ux 'initialized localized Help' {
        $account=Find-Ux 'cloud.AccountToggle'; $null -ne $account -and $account.Current.IsEnabled -and $null -ne (Find-Ux 'help.Screen')
    }
    Assert-NoPageOverflow "Help $UiLanguage full screen"
    foreach ($topic in $topics) { Inside ('help.' + $topic.Id) }
    Add-UxResult "Help $UiLanguage full-screen topic grid"
    Wait-Ux 'available localized window toggle' { $b=Find-Ux 'shell.WindowMode'; $null -ne $b -and $b.Current.IsEnabled }
    Invoke-Ux 'shell.WindowMode'
    Wait-Ux 'localized windowed Help' { $b=Find-Ux 'shell.WindowMode'; $null -ne $b -and $b.Current.IsEnabled -and $b.Current.Name -eq $resources['Shell.FullScreen'] }
    [UxNative]::Resize($hwnd, $owned.Id, 800)
    Wait-Ux '800-pixel Help window' { [Math]::Abs($rootElement.Current.BoundingRectangle.Width - 800 * [UxNative]::GetDpiForWindow($hwnd)/96.0) -le 2 }
    Assert-NoPageOverflow "Help $UiLanguage 800-pixel topic grid"
    foreach ($topic in $topics) {
        $id = 'help.' + $topic.Id
        $button = Element $id
        Assert-Ux ($button.Current.Name -ceq $resources[$topic.Title]) "Wrong localized topic label: $id"
        Inside $id
        Invoke-Ux $id
        Wait-Ux "opened Help topic $id" { $b=Find-Ux $id -IncludeOffscreen; $null -ne $b -and $b.Current.ItemStatus -ceq $resources['Help.OpenStatus'] }
        $text = Element ($id + '.Text')
        $pieces = New-Object 'Collections.Generic.List[string]'
        $lastPage = $false
        for ($index = 0; $index -lt 32; $index++) {
            $text = Element ($id + '.Text')
            Assert-Ux (-not [string]::IsNullOrWhiteSpace($text.Current.Name)) "Empty localized Help page: $id"
            Inside ($id + '.Text'); Inside $resources['Help.Close'] -ByName
            if ($topic.Id -ne 'Screen') { Inside $resources['Help.OpenPage'] -ByName }
            $pieces.Add($text.Current.Name)
            $status = (Element ($id + '.Page')).Current.Name
            $parts = $status -split '\s*/\s*'
            Assert-Ux ($parts.Count -eq 2 -and [int]$parts[0] -eq $index + 1) "Unexpected Help page status: $status"
            if ([int]$parts[0] -eq [int]$parts[1]) { $lastPage=$true; break }
            Inside ($id + '.Next'); Inside ($id + '.Previous')
            if ($index -eq 0) { Assert-Ux (-not (Element ($id + '.Previous')).Current.IsEnabled) "Previous is enabled on the first Help page: $id" }
            Invoke-Ux ($id + '.Next')
            Wait-Ux "next Help page $id" { $s=Find-Ux ($id + '.Page'); $null -ne $s -and $s.Current.Name -ne $status }
        }
        Assert-Ux $lastPage "Help paging did not finish: $id"
        $expected = ($topic.Text | ForEach-Object { $resources[$_] }) -join ' '
        Assert-Ux ((($pieces -join ' ') -replace '\s+', ' ').Trim() -ceq ($expected -replace '\s+', ' ').Trim()) "Localized Help text was lost or changed across pages: $id"
        if ($pieces.Count -gt 1) {
            Assert-Ux (-not (Element ($id + '.Next')).Current.IsEnabled) "Next is enabled on the last Help page: $id"
            Invoke-Ux ($id + '.Previous')
            Wait-Ux "previous Help page $id" { $t=Find-Ux ($id + '.Text'); $null -ne $t -and $t.Current.Name -ceq $pieces[$pieces.Count - 2] }
        }
        Invoke-Ux $resources['Help.Close'] -ByName
        Wait-Ux "closed Help topic $id" { $b=Find-Ux $id; $null -ne $b -and $b.Current.ItemStatus -ceq $resources['Help.ClosedStatus'] -and $null -eq (Find-Ux ($id + '.Text')) }
        Assert-NoPageOverflow "Help $UiLanguage after $id"
        Add-UxResult "Help $UiLanguage $($topic.Id): full translated text, paging and close without scrolling"
    }
    Export-UxImage ('help-' + $UiLanguage)
    Invoke-Ux 'help.Cards'; Wait-Id 'help.Cards.Dialog'; Invoke-Ux $resources['Help.OpenPage'] -ByName; Wait-Id 'cards.Start'
    Invoke-Ux $resources['Nav.Help'] -ByName; Wait-Id 'help.Backups'
    Invoke-Ux 'help.Backups'; Wait-Id 'settings.Section.account'
    # This focused probe checks Help's destination, not the separate backup workflow.
    $accountTab = Element 'settings.Section.account'
    $selection = $accountTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    Assert-Ux $selection.Current.IsSelected 'Help did not select the grown-up Settings section.'
    Add-UxResult "Help $UiLanguage opens Cards and grown-up Settings without signing in or changing learning"
}

try {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.WorkingDirectory = Split-Path $exe
    $startPage = if ($HelpOnly) { 'help' } else { 'home' }
    $start.Arguments = '--test-mode --data-dir="' + $testFolder + '" --page=' + $startPage
    $start.UseShellExecute = $false
    $owned = [Diagnostics.Process]::Start($start)
    $ownedPid = $owned.Id
    $null = $owned.Handle
    $null = $owned.WaitForInputIdle(10000)
    $hwnd = [UxNative]::WaitWindow($owned.Id)
    $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $signal = [UxSignal]::new($rootElement)
    if ($HelpOnly) {
        Test-HelpTopics
    } else {
    if ($GamesOnly) {
        Wait-Ux 'initialized shell for game checks' { $a=Find-Ux 'cloud.AccountToggle'; $null -ne $a -and $a.Current.IsEnabled }
        Wait-Ux 'full-screen game-only launch' { $b=Find-Ux 'shell.WindowMode'; $null -ne $b -and $b.Current.IsEnabled -and $b.Current.Name -eq 'Windowed' }
        Invoke-Ux 'shell.WindowMode'
        Wait-Ux 'windowed game-only checks' { (Find-Ux 'shell.WindowMode').Current.Name -eq 'Full screen' }
    } else {
    Wait-Ux 'full-screen launch action' { $b=Find-Ux 'shell.WindowMode'; $null -ne $b -and $b.Current.IsEnabled -and $b.Current.Name -eq 'Windowed' }
    $modeButton = Find-Ux 'shell.WindowMode'
    $contentScroll = Find-Ux 'shell.ContentScroll'
    Assert-Ux ($null -ne $contentScroll -and $modeButton.Current.BoundingRectangle.Bottom -le $contentScroll.Current.BoundingRectangle.Top) 'Window mode button is not in the fixed toolbar.'
    Invoke-Ux 'shell.WindowMode'
    Wait-Ux 'windowed action' { (Find-Ux 'shell.WindowMode').Current.Name -eq 'Full screen' }
    Invoke-Ux 'shell.WindowMode'
    Wait-Ux 'full-screen action restored' { (Find-Ux 'shell.WindowMode').Current.Name -eq 'Windowed' }
    Invoke-Ux 'shell.WindowMode'
    Wait-Ux 'windowed mode retained for width checks' { (Find-Ux 'shell.WindowMode').Current.Name -eq 'Full screen' }
    Add-UxResult 'Fixed toolbar switches full-screen and windowed modes'
    Wait-Ux 'isolated Home account status' { $a=Find-Ux 'cloud.AccountToggle'; $null -ne $a -and $a.Current.IsEnabled -and $a.Current.Name.Contains('Test account') }
    Assert-NoPageOverflow 'Home'
    Wait-Ux 'Home account action' { $null -ne (Find-Ux 'home.AccountAction' -IncludeOffscreen) }
    Invoke-Ux 'cloud.AccountToggle'
    Wait-Ux 'signed out' { (Find-Ux 'cloud.AccountToggle').Current.Name.Contains('Sign in') }
    $saved = Get-Content -LiteralPath (Join-Path $testFolder 'cloudcredentials.json') -Raw | ConvertFrom-Json
    $savedUid = $saved.PSObject.Properties['FirebaseUid']
    $savedToken = $saved.PSObject.Properties['ProtectedFirebaseRefreshToken']
    Assert-Ux ($null -eq $savedUid -or [string]::IsNullOrEmpty($savedUid.Value)) 'Sign-out retained the simulated identity.'
    Assert-Ux ($null -eq $savedToken -or [string]::IsNullOrEmpty($savedToken.Value)) 'Sign-out retained the simulated refresh token.'
    Assert-Ux ($null -eq (Find-Ux 'cloud.ClientId' -IncludeOffscreen) -and $null -eq (Find-Ux 'cloud.ClientSecret' -IncludeOffscreen)) 'Technical sign-in fields remain in the app.'
    Assert-Ux ((Find-Ux 'home.AccountAction' -IncludeOffscreen).Current.Name.Contains('Sign in with Google')) 'Home lost its sign-in action.'
    Add-UxResult 'Home/toolbar simulated connected state, sign-out and no technical sign-in fields (no live Google login)'
    Export-UxImage 'home'

    Invoke-Ux 'Cards' -ByName
    Wait-Id 'cards.Start'; Invoke-Ux 'cards.Start'
    Wait-Id 'cards.Reveal'
    Assert-NoPageOverflow 'Cards before reveal'
    Assert-Ux ($null -eq (Find-Ux 'cards.Rating.Good' -IncludeOffscreen)) 'Ratings distracted from the reveal step.'
    Invoke-Ux 'cards.Reveal'
    Wait-Ux 'enabled rating' { (Find-Ux 'cards.Rating.Good' -IncludeOffscreen).Current.IsEnabled }
    Assert-NoPageOverflow 'Cards after reveal'
    Export-UxImage 'cards-revealed'
    Invoke-Ux 'cards.Rating.Good'
    Wait-Ux 'saved card/Undo' { $b=Find-Ux 'cards.Undo'; $null -ne $b -and $b.Current.IsEnabled }
    $progress = Read-TestProgress
    Assert-Ux (@($progress.Sessions.'en:A1:cards'.Answers.PSObject.Properties).Count -gt 0) 'Rating did not persist in isolated profile.'
    Invoke-Ux 'cards.Undo'; Wait-Id 'cards.Reveal'
    Add-UxResult 'Cards reveal/rating gate, saved rating, Undo'

    Invoke-Ux 'Quiz' -ByName
    Wait-Id 'quiz.Start'; Invoke-Ux 'quiz.Start'
    Wait-Id 'quiz.Question'; Wait-Id 'quiz.Choice.0'
    Assert-NoPageOverflow 'Quiz question'
    Assert-Ux (-not (Find-Ux 'quiz.Continue').Current.IsEnabled) 'Quiz Continue was enabled before answering.'
    Invoke-Ux 'quiz.Choice.0'; Wait-Id 'quiz.Feedback'
    $progress = Read-TestProgress
    Assert-Ux ($progress.Sessions.'en:A1:quiz'.Index -eq 0) 'Quiz auto-advanced before Continue.'
    Wait-Ux 'quiz Continue enabled' { $control=Find-Ux 'quiz.Continue'; $null -ne $control -and $control.Current.IsEnabled }
    Assert-NoPageOverflow 'Quiz feedback'
    Export-UxImage 'quiz-feedback'
    Invoke-Ux 'quiz.Continue'
    Wait-Ux 'next quiz question' { $null -eq (Find-Ux 'quiz.Feedback') -and $null -ne (Find-Ux 'quiz.Choice.0') }
    Add-UxResult 'Quiz persistent answer, explicit feedback and Continue'

    Invoke-Ux 'Settings' -ByName
    Wait-Id 'settings.Section.learning'; Wait-Id 'settings.DailyGoal'
    Set-UxText 'settings.DailyGoal' '0'
    Invoke-Ux 'settings.SaveLearning'; Wait-Id 'settings.DailyGoal.Error'
    $savedSettings = Get-Content -LiteralPath (Join-Path $testFolder 'settings.json') -Raw | ConvertFrom-Json
    Assert-Ux ($savedSettings.DailyGoal -eq 137) 'Invalid goal was persisted.'
    Set-UxText 'settings.DailyGoal' '200'
    Invoke-Ux 'settings.SaveLearning'
    # Wait for the post-save UI before reading; an open reader can block the atomic rename.
    Wait-Ux 'saved valid goal' {
        $null -ne (Find-Ux 'Saved goal: 200 words a day' -ByName -IncludeOffscreen) -and
            (Find-Ux 'cloud.AccountToggle').Current.IsEnabled
    }
    $savedSettings = Get-Content -LiteralPath (Join-Path $testFolder 'settings.json') -Raw | ConvertFrom-Json
    Assert-Ux ($savedSettings.DailyGoal -eq 200) 'The confirmed goal was not persisted.'
    Add-UxResult 'Settings invalid goal rejected and valid goal explicitly saved'
    Invoke-Ux 'settings.Section.appearance'; Wait-Id 'settings.FontScale'
    $slider = Find-Ux 'settings.FontScale'
    $range = $slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $range.SetValue(115)
    Wait-Ux 'preview scale' { (Find-Ux 'settings.FontScale').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value -eq 115 }
    Invoke-Ux 'settings.Section.learning'; Wait-Id 'settings.DailyGoal'
    Assert-NoPageOverflow 'Settings practice'
    Invoke-Ux 'settings.Section.appearance'; Wait-Id 'settings.FontScale'
    Assert-NoPageOverflow 'Settings appearance'
    Assert-Ux ((Find-Ux 'settings.FontScale').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value -eq 115) 'Appearance draft was lost across tabs.'
    $savedSettings = Get-Content -LiteralPath (Join-Path $testFolder 'settings.json') -Raw | ConvertFrom-Json
    Assert-Ux ($savedSettings.FontScale -eq 1) 'Preview wrote settings without Apply.'
    Export-UxImage 'settings-preview'
    Invoke-Ux 'settings.Section.account'; Wait-Id 'settings.Backup.Export'
    Assert-NoPageOverflow 'Settings grown-up tools'
    Add-UxResult 'Settings grouped sections, retained draft, non-persistent preview, backups'

    Invoke-Ux 'Words' -ByName
    Wait-Id 'library.Search'; Wait-Id 'library.ResultCount'
    Set-UxText 'library.Search' 'zzzz-no-such-vocabulary'
    Wait-Id 'library.Empty'
    Assert-Ux (-not (Find-Ux 'library.Practice').Current.IsEnabled) 'Empty library results still enabled practice.'
    Invoke-Ux 'library.Empty.ClearFilters'
    Wait-Ux 'library filters reset' { $null -eq (Find-Ux 'library.Empty') -and (Find-Ux 'library.Practice').Current.IsEnabled }
    Set-UxText 'library.Search' 'cat'
    Set-UxText 'library.Search' 'dog'
    Set-UxText 'library.Search' 'zzzz-no-such-vocabulary'
    Wait-Id 'library.Empty'
    Invoke-Ux 'library.ClearFilters'
    Wait-Ux 'library reset after rapid search' {
        $next = Find-Ux 'library.Next' -IncludeOffscreen
        $null -eq (Find-Ux 'library.Empty') -and $null -ne $next -and $next.Current.IsEnabled
    }
    Invoke-Ux 'library.Next'
    Wait-Ux 'next word is reachable' { (Find-Ux 'library.ResultCount' -IncludeOffscreen).Current.Name.Contains('word 2 of') }
    Assert-NoPageOverflow 'Word browser'
    Export-UxImage 'word-library'
    Add-UxResult 'Word search latest query, empty state and reset filters'

    Invoke-Ux 'Statistics' -ByName
    Wait-Id 'stats.Section.overview'; Assert-NoPageOverflow 'Statistics overview'
    Invoke-Ux 'stats.Section.answers'; Wait-Id 'stats.GameScoresPage'; Assert-NoPageOverflow 'Statistics answers'
    Invoke-Ux 'stats.Section.history'
    Wait-Ux 'statistics popups' { $null -ne (Find-Ux 'stats.Legacy' -IncludeOffscreen) -and $null -ne (Find-Ux 'stats.Recall' -IncludeOffscreen) }
    Assert-NoPageOverflow 'Statistics history'
    Export-UxImage 'statistics'
    Add-UxResult 'Statistics separates current metrics and collapsed historical details'

    Invoke-Ux 'Help' -ByName
    Wait-Id 'help.Cards'; Wait-Id 'help.Quiz'; Wait-Id 'help.Games'; Wait-Id 'help.Words'
    Assert-NoPageOverflow 'Help'
    Invoke-Ux 'help.Backups'; Wait-Id 'settings.Backup.Export'
    Invoke-Ux 'About' -ByName; Wait-Id 'about.Help'; Assert-NoPageOverflow 'About'
    Invoke-Ux 'about.Help'; Wait-Id 'help.Cards'
    Export-UxImage 'help-topics'
    Add-UxResult 'Help topics, backup navigation and About actions'
    }

    $games = @(
        @('Simple Games','Hangman'), @('Simple Games','Word Scramble'), @('Simple Games','Listening Dictation'),
        @('Simple Games','Speed Round'), @('Simple Games','Survival Streak'), @('Simple Games','True or False'),
        @('Simple Games','Word Class'), @('Simple Games','Matching Pairs'), @('Simple Games','Listening Choice'),
        @('Simple Games','Odd One Out'), @('Simple Games','Word Race'), @('Complex Games','Cloze Test'),
        @('Complex Games','Sentence Scramble'), @('Complex Games','Reading Comprehension'), @('Complex Games','Word Morph'),
        @('Complex Games','Word Bingo'), @('Complex Games','Boss Rush'), @('Complex Games','CodyCross'),
        @('Complex Games','Crossword'), @('Complex Games','Daily Challenge'), @('Complex Games','Scrabble'),
        @('Complex Games','Word Guess'), @('Complex Games','Category Sprint'), @('Complex Games','Clue Detective'), @('Complex Games','Word Matrix')
    )
    $gameStarted = [string]::IsNullOrWhiteSpace($GameStartAt)
    foreach ($game in $games) {
        if (-not $gameStarted) {
            if ($game[1] -ne $GameStartAt) { continue }
            $gameStarted = $true
        }
        Invoke-Ux $game[0] -ByName
        Invoke-GameCatalogItem $game[1]
        Assert-NoPageOverflow ($game[1] + ' details')
        Invoke-Ux 'game.Start'
        Wait-Ux ($game[1] + ' prompt') { $null -ne (Find-Ux 'game.Question') -or $null -ne (Find-Ux 'Unavailable for this selection' -ByName) }
        $unavailable = $null -ne (Find-Ux 'Unavailable for this selection' -ByName)
        if (-not $unavailable) {
            $questionState = @{ Element = $null }
            Wait-Ux ($game[1] + ' attached question') {
                $questionState.Element = Find-Ux 'game.Question'
                $null -ne $questionState.Element
            }
            $question = $questionState.Element
            Assert-Ux (-not [string]::IsNullOrWhiteSpace($question.Current.Name)) ('Empty question: ' + $game[1])
            Assert-NoPageOverflow ($game[1] + ' game')
            if ($game[1] -in @('Word Race','Cloze Test','Word Guess','Daily Challenge','Category Sprint','Clue Detective','Scrabble')) {
                $submit = Find-Ux 'game.Submit' -IncludeOffscreen
                Assert-Ux ($null -ne $submit -and -not $submit.Current.IsEnabled) ('Blank answer enabled Submit: ' + $game[1])
                Set-UxText 'game.Answer' 'test'
                Set-UxText 'game.Answer' ''
                Wait-Ux 'empty answer re-disables Submit' { $b=Find-Ux 'game.Submit' -IncludeOffscreen; $null -ne $b -and -not $b.Current.IsEnabled }
            }
            Invoke-Ux 'game.Rules'; Wait-Id 'game.RulesDialog'; Invoke-Ux 'Close instructions' -ByName
            Wait-Ux 'rules closed' { $account=Find-Ux 'cloud.AccountToggle'; $null -eq (Find-Ux 'game.RulesDialog') -and $null -ne $account -and $account.Current.IsEnabled }
            if ($game[1] -in @('Hangman','Crossword','CodyCross','Word Matrix')) { Export-UxImage ($game[1] -replace ' ','-') }
            [UxNative]::Resize($hwnd, $owned.Id, 800)
            Wait-Ux 'compact game layout' {
                $width = 800 * [UxNative]::GetDpiForWindow($hwnd) / 96.0
                $window = $rootElement.Current.BoundingRectangle
                $prompt = Find-Ux 'game.Question'
                $null -ne $prompt -and [Math]::Abs($window.Width-$width) -le 2 -and
                    $prompt.Current.BoundingRectangle.Left -ge $window.Left -and $prompt.Current.BoundingRectangle.Right -le $window.Right
            }
                    Assert-NoPageOverflow ($game[1] + ' compact game')
            if ($game[1] -in @('Hangman','Crossword','CodyCross','Word Matrix')) { Export-UxImage (($game[1] -replace ' ','-') + '-compact') }
            [UxNative]::Resize($hwnd, $owned.Id, 1180)
            Wait-Ux 'restored game width' { [Math]::Abs($rootElement.Current.BoundingRectangle.Width - 1180 * [UxNative]::GetDpiForWindow($hwnd)/96.0) -le 2 }
        }
        Add-UxResult ('Game at default/800 width: ' + $game[1]) $(if ($unavailable) { 'SKIP_UNAVAILABLE' } else { 'PASS' })
        Invoke-Ux 'game.Back'
        if (-not $unavailable) { Invoke-Ux 'Continue' -ByName }
        Wait-Ux 'game detail' { $null -ne (Find-Ux 'game.Start' -IncludeOffscreen) }
        Invoke-Ux 'game.Back'
        Wait-Ux 'game catalog' { $null -ne (Find-Ux $game[1] -ByName -IncludeOffscreen -Action) }
    }
    Assert-Ux $gameStarted "Unknown game start name: $GameStartAt"
    }
    $exitCode = 0
}
catch {
    $results.Add([pscustomobject]@{ scenario='failed'; status='FAIL'; message=$_.Exception.Message; stack=$_.ScriptStackTrace })
    Write-Output ('FAIL ' + $_.Exception.Message)
    Write-Output $_.ScriptStackTrace
    if ($owned -and $owned.WaitForExit(1500)) {
        $unexpectedExit = $owned.ExitCode
        Write-Output "OWNED_APP_EXIT_BEFORE_CLEANUP=$unexpectedExit PID=$ownedPid"
    }
    if ($owned -and -not $owned.HasExited) {
        try {
            $details = Find-Ux 'settings.SaveFailure.Details' -IncludeOffscreen
            if ($null -ne $details) {
                $details.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
                $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
                foreach ($text in $details.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) { Write-Output ('TEST_PROFILE_ERROR: ' + $text.Current.Name) }
            }
        } catch { }
    }
    if ($owned -and -not $owned.HasExited -and $hwnd -ne [IntPtr]::Zero) { try { Export-UxImage 'failure' } catch { } }
}
finally {
    if ($signal) { $signal.Dispose() }
    if ($owned) {
        if (-not $owned.HasExited) { $null = $owned.CloseMainWindow(); if (-not $owned.WaitForExit(8000)) { $owned.Kill(); $null=$owned.WaitForExit(8000) } }
        $owned.Dispose()
    }
    if ($dpiContext -ne [IntPtr]::Zero) { $null=[UxNative]::SetThreadDpiAwarenessContext($dpiContext) }
    $receipt = @{ executable=$exe; sha256=(Get-FileHash $exe -Algorithm SHA256).Hash; assemblySha256=(Get-FileHash (Join-Path (Split-Path $exe) 'YDKE.dll') -Algorithm SHA256).Hash; processId=$ownedPid; unexpectedExit=$unexpectedExit; profile=$testFolder; uiLanguage=$UiLanguage; fontScale=$HelpFontScale; helpOnly=[bool]$HelpOnly; networkAuthenticationTested=$false; simulatedIdentity=$true; results=$results.ToArray() }
    [IO.File]::WriteAllText((Join-Path $testFolder 'results.json'), ($receipt | ConvertTo-Json -Depth 8), $encoding)
    Write-Output ("RESULT UX passed={0} skipped={1} failed={2} profile={3}" -f @($results | Where-Object status -eq 'PASS').Count, @($results | Where-Object status -eq 'SKIP_UNAVAILABLE').Count, @($results | Where-Object status -eq 'FAIL').Count, $testFolder)
}
exit $exitCode