# Claude Code watchdog v2
# Reads the Claude terminal window text via UI Automation, then either:
#   - sends ENTER on rate-limit / "press enter" dialogs (auto-pick default "wait")
#   - sends "продовжуй" + ENTER as a wake-up prompt when Claude has been idle past
#     the configured threshold AND no rate-limit dialog is active
#
# Usage:
#   pwsh -File seeder\watchdog.ps1
#   pwsh -File seeder\watchdog.ps1 -WindowTitle "*claude*" -PollSeconds 60 -IdleThresholdSec 600
#   pwsh -File seeder\watchdog.ps1 -DryRun
#
# Stop with Ctrl+C.

[CmdletBinding()]
param(
    [string]$WindowTitle = "*Claude*",
    [int]$PollSeconds = 60,
    [int]$IdleThresholdSec = 600,
    [int]$UserIdleThresholdSec = 600,
    [string]$WakePrompt = "продовжуй",
    [switch]$DryRun
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder buf, int count);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
}
"@

function Find-ClaudeWindow {
    Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -like $WindowTitle } | Select-Object -First 1
}

function Get-SystemIdleSeconds {
    $info = New-Object Win32+LASTINPUTINFO
    $info.cbSize = [System.Runtime.InteropServices.Marshal]::SizeOf($info)
    if ([Win32]::GetLastInputInfo([ref]$info)) {
        return [int](([Environment]::TickCount - $info.dwTime) / 1000)
    }
    return -1
}

function Read-WindowText {
    param([IntPtr]$hwnd)
    try {
        $el = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
        if (-not $el) { return $null }
        # Try TextPattern (Windows Terminal exposes terminal buffer here)
        $tp = $el.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
        if ($tp) {
            $range = $tp.DocumentRange.Clone()
            return $range.GetText(20000)
        }
        # Fallback: walk children, concat Name properties
        $sb = New-Object System.Text.StringBuilder
        $walker = [System.Windows.Automation.TreeWalker]::ContentViewWalker
        $child = $walker.GetFirstChild($el)
        while ($child -and $sb.Length -lt 20000) {
            $name = $child.Current.Name
            if ($name) { [void]$sb.AppendLine($name) }
            $child = $walker.GetNextSibling($child)
        }
        return $sb.ToString()
    } catch {
        return $null
    }
}

function Test-RateLimitDialog([string]$txt) {
    if (-not $txt) { return $false }
    return ($txt -match "(?i)wait.*token|rate limit|approaching.*limit|context.*limit|press enter to continue|session.*resume|tokens? will refresh")
}

function Send-Keys-To-Window([IntPtr]$hwnd, [string]$keys) {
    [Win32]::ShowWindow($hwnd, 9) | Out-Null  # SW_RESTORE
    Start-Sleep -Milliseconds 250
    [Win32]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 350
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 200
}

Write-Host "[watchdog v2] target pattern '$WindowTitle' | poll ${PollSeconds}s | idle threshold ${IdleThresholdSec}s | dry-run: $DryRun" -ForegroundColor Cyan
Write-Host "[watchdog v2] reads terminal text via UI Automation; sends ENTER on rate-limit dialog, '$WakePrompt' on extended idle" -ForegroundColor Cyan

$lastTextHash = ""
$lastTextChangeAt = Get-Date
$pokeCount = 0
$wakeCount = 0
$origWindow = [Win32]::GetForegroundWindow()

while ($true) {
    Start-Sleep -Seconds $PollSeconds

    $userIdle = Get-SystemIdleSeconds
    $proc = Find-ClaudeWindow
    if (-not $proc) {
        Write-Host "[$(Get-Date -Format HH:mm:ss)] Claude window not found (pattern '$WindowTitle')" -ForegroundColor Yellow
        continue
    }

    $hwnd = $proc.MainWindowHandle
    $text = Read-WindowText -hwnd $hwnd
    if (-not $text) {
        Write-Host "[$(Get-Date -Format HH:mm:ss)] could not read window text (UIA returned null)" -ForegroundColor Yellow
        continue
    }

    # Track activity by hashing the visible terminal buffer.
    $hash = [System.BitConverter]::ToString(
        (New-Object System.Security.Cryptography.SHA1Managed).ComputeHash(
            [System.Text.Encoding]::UTF8.GetBytes($text)
        )
    )
    $isRateLimit = Test-RateLimitDialog -txt $text
    $textIdleSec = [int]((Get-Date) - $lastTextChangeAt).TotalSeconds
    if ($hash -ne $lastTextHash) {
        $lastTextHash = $hash
        $lastTextChangeAt = Get-Date
        $textIdleSec = 0
    }

    $status = "user idle ${userIdle}s | text idle ${textIdleSec}s | rate-limit: $isRateLimit"
    Write-Host "[$(Get-Date -Format HH:mm:ss)] $status"

    if ($userIdle -lt $UserIdleThresholdSec) {
        Write-Host "  -> user is active (<${UserIdleThresholdSec}s) — skip" -ForegroundColor DarkGray
        continue
    }

    if ($isRateLimit) {
        # Rate-limit / press-enter dialog — accept default with ENTER.
        if ($DryRun) {
            Write-Host "  -> [DRY-RUN] would send ENTER to accept rate-limit dialog" -ForegroundColor Magenta
        } else {
            Send-Keys-To-Window -hwnd $hwnd -keys "{ENTER}"
            $pokeCount++
            Write-Host "  -> Enter sent (poke #$pokeCount)" -ForegroundColor Green
        }
    }
    elseif ($textIdleSec -ge $IdleThresholdSec) {
        # No output change for a while AND no rate-limit dialog → Claude is idle, wake it up.
        if ($DryRun) {
            Write-Host "  -> [DRY-RUN] would type '$WakePrompt' + ENTER (idle ${textIdleSec}s)" -ForegroundColor Magenta
        } else {
            Send-Keys-To-Window -hwnd $hwnd -keys "$WakePrompt{ENTER}"
            $wakeCount++
            $lastTextChangeAt = Get-Date  # reset so we don't spam
            Write-Host "  -> wake-up '$WakePrompt' sent (wake #$wakeCount)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  -> nothing to do" -ForegroundColor DarkGray
    }

    if ($origWindow -ne 0) {
        [Win32]::SetForegroundWindow($origWindow) | Out-Null
    }
}
