$ErrorActionPreference = "Stop"

$BotPath = $PSScriptRoot
$ScriptName = "telegram_receiver.py"
$WorkspaceRoot = (Resolve-Path (Join-Path $BotPath "..\..\..\..\..")).Path
$PythonExe = "C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\pythonw.exe"
$PythonConsole = "C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"

Write-Host "Starting Youngsuk Telegram receiver"

if (-not (Test-Path $PythonConsole)) {
    Write-Host "Bundled Python not found: $PythonConsole"
    exit 1
}
if (-not (Test-Path (Join-Path $BotPath $ScriptName))) {
    Write-Host "Receiver script not found: $ScriptName"
    exit 1
}
if (-not (Test-Path (Join-Path $WorkspaceRoot ".env.encrypted")) -and -not (Test-Path (Join-Path $WorkspaceRoot ".env"))) {
    Write-Host "Central env not found under $WorkspaceRoot"
    exit 1
}

Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -match '^python' -and
        $_.CommandLine -and
        ($_.CommandLine.Contains('telegram_receiver.py') -or $_.CommandLine.Contains('run_youngsuk_daemon.py'))
    } |
    ForEach-Object {
        Write-Host "Stopping existing receiver PID $($_.ProcessId)"
        taskkill /F /PID $_.ProcessId | Out-Null
    }

Set-Location $BotPath
$process = Start-Process -FilePath $PythonExe -ArgumentList $ScriptName -WorkingDirectory $BotPath -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 3

if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
    Write-Host "Youngsuk receiver started. PID: $($process.Id)"
    Write-Host "Log: $BotPath\telegram_receiver.log"
} else {
    Write-Host "Youngsuk receiver exited immediately. Check telegram_receiver.log"
    exit 1
}
