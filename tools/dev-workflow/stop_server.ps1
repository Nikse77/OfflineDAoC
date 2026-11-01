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
$server = Get-Process -Name 'CoreServer' -ErrorAction SilentlyContinue
if (@($server).Count -ne 1) { Log "ABORT: expected exactly one CoreServer, found $(@($server).Count)"; exit 2 }
$lpid = $launcher.Id
Log "launcher pid $lpid, server pid $($server.Id)"

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
# the launcher shows this box when the exit takes more than 10 seconds; it only says the stop goes on
function Handle-Dialogs {
    foreach ($w in (Get-LauncherWindows)) {
        if ($w.Current.Name -eq 'Shutdown pending') {
            $b = $w.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, 'OK')))
            if ($b) { Log "dialog 'Shutdown pending' -> OK"; Click-Button $b | Out-Null; Start-Sleep -Milliseconds 500 }
        }
    }
}

$stop = Find-Button 'STOP SERVER'
if (-not $stop) { Log "ABORT: STOP SERVER button not found"; exit 3 }
if (-not $stop.Current.IsEnabled) { Log "ABORT: STOP SERVER is disabled"; exit 3 }
Log "clicking STOP SERVER ($(Click-Button $stop))"

$deadline = (Get-Date).AddSeconds(240)
while ((Get-Date) -lt $deadline -and (Get-Process -Name 'CoreServer' -ErrorAction SilentlyContinue)) { Handle-Dialogs; Start-Sleep -Seconds 1 }
if (Get-Process -Name 'CoreServer' -ErrorAction SilentlyContinue) { Log "WARNING: CoreServer still running after 240 s; nothing was killed"; exit 4 }
Log "CoreServer exited"
$listen = Get-NetTCPConnection -State Listen -LocalPort 10300 -ErrorAction SilentlyContinue
Log ("port 10300 listening: " + [bool]$listen)
