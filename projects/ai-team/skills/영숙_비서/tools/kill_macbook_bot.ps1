# 맥북 봇 원격 종료 스크립트
# 사용법: .\kill_macbook_bot.ps1

$env:PYTHONUTF8 = "1"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  맥북 텔레그램 봇 원격 종료" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# .env 파일에서 SSH 정보 확인
$envPath = "D:\ai_lab\.env"
$sshHost = ""

if (Test-Path $envPath) {
    $envContent = Get-Content $envPath -Encoding UTF8
    foreach ($line in $envContent) {
        if ($line -match '^MACBOOK_SSH_HOST="?([^"]+)"?') {
            $sshHost = $matches[1]
            break
        }
    }
}

if (-not $sshHost -or $sshHost -eq "") {
    Write-Host "⚠️  SSH 설정이 없습니다." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "다음 방법 중 하나를 선택하세요:" -ForegroundColor White
    Write-Host ""
    Write-Host "1. SSH 설정하기 (REMOTE_CONTROL_GUIDE.md 참고)" -ForegroundColor Green
    Write-Host "2. 맥북에서 직접 종료: pkill -f telegram_receiver" -ForegroundColor Green
    Write-Host "3. Telegram API logOut 실행 (모든 봇 로그아웃)" -ForegroundColor Yellow
    Write-Host ""

    $choice = Read-Host "선택 (1/2/3)"

    if ($choice -eq "3") {
        Write-Host ""
        Write-Host "⏳ 모든 봇 인스턴스 로그아웃 중..." -ForegroundColor Yellow

        python -c @"
import urllib.request, json, os, sys
sys.path.insert(0, 'D:/ai_lab/projects/ai-team')
from _shared.env_loader import load_env
load_env()
token = os.getenv('TELEGRAM_BOT_TOKEN')
url = f'https://api.telegram.org/bot{token}/logOut'
result = json.loads(urllib.request.urlopen(url).read().decode())
print('✅ LogOut 성공!' if result.get('ok') else '❌ LogOut 실패')
"@

        Write-Host ""
        Write-Host "⏳ 5초 대기 후 Windows 봇 시작..." -ForegroundColor Yellow
        Start-Sleep -Seconds 5

        $env:PYTHONUTF8 = "1"
        Start-Process pythonw -ArgumentList "D:\ai_lab\projects\ai-team\skills\영숙_비서\tools\telegram_receiver.py" -WindowStyle Hidden

        Write-Host "✅ Windows 봇 시작 완료!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📝 주의: 맥북 봇이 자동으로 재연결할 수 있습니다." -ForegroundColor Yellow
        Write-Host "   완전히 종료하려면 맥북에서 직접 pkill -f telegram_receiver 실행" -ForegroundColor Yellow
    }

    exit
}

Write-Host "🔌 SSH 접속 중: $sshHost" -ForegroundColor Cyan
Write-Host ""

# SSH로 맥북 봇 종료
$sshKeyPath = ""
$envContent = Get-Content $envPath -Encoding UTF8
foreach ($line in $envContent) {
    if ($line -match '^MACBOOK_SSH_KEY_PATH="?([^"]+)"?') {
        $sshKeyPath = $matches[1]
        break
    }
}

try {
    if ($sshKeyPath -and (Test-Path $sshKeyPath)) {
        Write-Host "🔑 SSH 키 사용: $sshKeyPath" -ForegroundColor Gray
        $result = ssh -i $sshKeyPath $sshHost "pkill -f telegram_receiver.py" 2>&1
    } else {
        $result = ssh $sshHost "pkill -f telegram_receiver.py" 2>&1
    }

    # pkill은 프로세스를 찾으면 0, 못 찾으면 1 반환
    if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 1) {
        Write-Host "✅ 맥북 봇 종료 명령 전송 완료" -ForegroundColor Green
        Write-Host ""
        Write-Host "⏳ 3초 대기 후 Windows 봇 시작..." -ForegroundColor Yellow
        Start-Sleep -Seconds 3

        # Windows 봇 시작
        $env:PYTHONUTF8 = "1"
        $process = Start-Process pythonw -ArgumentList "D:\ai_lab\projects\ai-team\skills\영숙_비서\tools\telegram_receiver.py" -WindowStyle Hidden -PassThru

        Write-Host "✅ Windows 봇 시작 완료 (PID: $($process.Id))" -ForegroundColor Green
        Write-Host ""
        Write-Host "📱 텔레그램에서 '현황' 메시지를 보내 봇이 응답하는지 확인하세요." -ForegroundColor Cyan
    } else {
        Write-Host "❌ SSH 명령 실패: $result" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ SSH 연결 실패: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "문제 해결:" -ForegroundColor Yellow
    Write-Host "  1. 맥북이 켜져 있고 네트워크에 연결되어 있는지 확인" -ForegroundColor White
    Write-Host "  2. .env 파일의 MACBOOK_SSH_HOST가 올바른지 확인" -ForegroundColor White
    Write-Host "  3. SSH 키 권한 확인: chmod 600 $sshKeyPath" -ForegroundColor White
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
