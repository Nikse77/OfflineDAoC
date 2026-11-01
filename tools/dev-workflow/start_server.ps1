$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win32 { [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l); }
"@
$BM_CLICK = 0x00F5
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
function Log($m) { Write-Output ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $m) }

$launcher = Get-Process -Name 'OfflineDAoC' -ErrorAction SilentlyContinue
if (@($launcher).Count -ne 1) { Log "ABORT: expected exactly one launcher process, found $(@($launcher).Count)"; exit 2 }
if (Get-Process -Name 'CoreServer' -ErrorAction SilentlyContinue) { Log "ABORT: a CoreServer is already running"; exit 2 }
$lpid = $launcher.Id

function Get-LauncherWindows {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $lpid)
    return $AE::RootElement.FindAll($TS::Children, $cond)
}
function Find-Button($name) {
    foreach ($w in (Get-LauncherWindows)) {
        $c = New-Object System.Windows.Automation.AndCondition(
            (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)),
            (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
        $b = $w.FindFirst($TS::Descendants, $c)
        if ($b) { return $b }
    }
    return $null
}
function Click-Button($el) {
    $pattern = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return "invoke" }
    $h = [IntPtr]$el.Current.NativeWindowHandle
    if ($h -ne [IntPtr]::Zero) { [Win32]::SendMessage($h, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; return "bm_click" }
    throw "cannot click"
}
function Handle-Dialogs {
    foreach ($w in (Get-LauncherWindows)) {
        $title = $w.Current.Name
        if ($title -eq 'Low available memory') {
            $b = $w.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, 'Yes')))
            if ($b) { Log "dialog 'Low available memory' -> Yes"; Click-Button $b | Out-Null; Start-Sleep -Milliseconds 500 }
        }
        elseif ($title -in @('Unsaved bot goals', 'Invalid bot goals', 'Cannot start', 'Unable to start server')) {
            Log "DIALOG '$title' needs a person; the server was not started"; exit 7
        }
    }
}

$start = $null
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
    $start = Find-Button 'START SERVER'
    if ($start -and $start.Current.IsEnabled) { break }
    Start-Sleep -Seconds 1
}
if (-not $start -or -not $start.Current.IsEnabled) { Log "ABORT: START SERVER is not available"; exit 5 }
Log "clicking START SERVER ($(Click-Button $start))"

$deadline = (Get-Date).AddSeconds(480)
$up = $false
while ((Get-Date) -lt $deadline) {
    Handle-Dialogs
    if (Get-NetTCPConnection -State Listen -LocalPort 10300 -ErrorAction SilentlyContinue) { $up = $true; break }
    Start-Sleep -Seconds 2
}
$s = Get-Process -Name 'CoreServer' -ErrorAction SilentlyContinue
if ($up -and $s) { Log "server listening on 10300, CoreServer pid $($s.Id)" } else { Log "WARNING: port 10300 not listening within 480 s (CoreServer running: $([bool]$s))"; exit 6 }
