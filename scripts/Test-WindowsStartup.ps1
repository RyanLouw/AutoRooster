param([string]$Executable = "$PSScriptRoot/../artifacts/win-x64/SchoolExamScheduler.Desktop.exe")
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') { throw 'Requires Windows and an interactive desktop' }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$process = Start-Process -FilePath (Resolve-Path $Executable) -PassThru
try {
    $deadline = (Get-Date).AddSeconds(40)
    do {
        if ($process.HasExited) { throw "Application exited with code $($process.ExitCode)" }
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw 'No application window appeared' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    if ($window.Current.Name -ne 'School Exam Invigilation Scheduler') { throw "Unexpected window: $($window.Current.Name)" }
    $pattern = $window.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    foreach ($size in @(@(1366,768), @(1920,1080))) {
        $pattern.Resize($size[0], $size[1])
        foreach ($name in @('Teachers','Teacher Availability','Subjects','Venues','Examinations','Generate Roster','Review Roster','Dashboard / Reports','Settings','Subscription')) {
            $condition = New-Object System.Windows.Automation.AndCondition @(
                (New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), ([System.Windows.Automation.ControlType]::TabItem)),
                (New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty), $name)
            )
            $tab = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -eq $tab) { throw "Missing navigation tab: $name" }
            $selection = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $selection.Select()
        }
    }
    Write-Output 'Windows startup and navigation smoke checks passed. Visual acceptance is still required.'
} finally {
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; if (-not $process.WaitForExit(10000)) { $process.Kill() } }
}
