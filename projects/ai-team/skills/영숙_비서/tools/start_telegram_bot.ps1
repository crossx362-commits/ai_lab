# start_telegram_bot.ps1 — 영숙 텔레그램 봇 시작 스크립트
# 사용법: powershell -ExecutionPolicy Bypass .\start_telegram_bot.ps1

$ErrorActionPreference = "Stop"
$BotPath = $PSScriptRoot
$ScriptName = "telegram_receiver.py"

Write-Host "=" * 60
Write-Host "  [영숙] 텔레그램 봇 시작 스크립트"
Write-Host "=" * 60

# 1. 기존 프로세스 확인
Write-Host "`n[1/4] 기존 프로세스 확인 중..."
Write-Host "  WMI 접근이 제한된 환경에서는 명령줄 기반 중복 확인을 건너뜁니다."

# 2. 환경 확인
Write-Host "`n[2/4] 환경 확인 중..."
cd $BotPath

$PythonExe = "C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\pythonw.exe"
$PythonConsole = "C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"

# Python 확인
if (Test-Path $PythonConsole) {
    $pythonVersion = & $PythonConsole --version 2>&1
    Write-Host "  ✅ Python: $pythonVersion"
} else {
    Write-Host "  ❌ 번들 Python을 찾을 수 없습니다: $PythonConsole"
    exit 1
}

# .env 파일 확인
$envPath = Join-Path (Split-Path $BotPath -Parent | Split-Path -Parent | Split-Path -Parent | Split-Path -Parent) ".env"
if (Test-Path $envPath) {
    Write-Host "  ✅ .env 파일 존재"
} else {
    Write-Host "  ⚠️  .env 파일 없음 (환경변수로 대체 가능)"
}

# telegram_receiver.py 확인
if (Test-Path $ScriptName) {
    Write-Host "  ✅ $ScriptName 파일 존재"
} else {
    Write-Host "  ❌ $ScriptName 파일을 찾을 수 없습니다."
    exit 1
}

# 3. 봇 시작
Write-Host "`n[3/4] 텔레그램 봇 시작 중..."
$process = Start-Process -FilePath $PythonExe -ArgumentList $ScriptName -WindowStyle Hidden -PassThru
Write-Host "  ✅ 봇 시작됨 (PID: $($process.Id))"

# 4. 시작 확인
Write-Host "`n[4/4] 봇 상태 확인 중..."
Start-Sleep -Seconds 3

$runningProcess = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
if ($runningProcess) {
    Write-Host "  ✅ 봇이 정상적으로 실행 중입니다!"
    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    Write-Host "  🤖 영숙 텔레그램 봇 시작 완료"
    Write-Host "  📋 PID: $($process.Id)"
    Write-Host "  📁 경로: $BotPath"
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    Write-Host "`n💡 팁:"
    Write-Host "  - 로그 확인: Get-Content telegram_receiver.log -Tail 50 -Wait"
    Write-Host "  - 봇 종료: Stop-Process -Id $($process.Id)"
    Write-Host "  - 프로세스 확인: Get-Process -Id $($process.Id)"
} else {
    Write-Host "  ❌ 봇이 시작 직후 종료되었습니다."
    Write-Host "  로그를 확인하세요: Get-Content telegram_receiver.log -Tail 20"
    exit 1
}

Write-Host "`n텔레그램으로 메시지를 보내서 테스트해보세요!"
