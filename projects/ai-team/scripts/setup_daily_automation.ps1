# AI 팀 일일 자동화 Windows Task Scheduler 설정 스크립트

$TaskName = "AI_Team_Daily_Automation"
$PythonPath = "C:\Python312\python.exe"  # Python 경로 (필요시 수정)
$ScriptPath = "d:\ai_lab\projects\ai-team\skills\daily_ai_team_runner.py"
$WorkingDir = "d:\ai_lab"

# 기존 작업 삭제 (있는 경우)
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "기존 작업 삭제 중..."
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

# 작업 동작 정의
$Action = New-ScheduledTaskAction `
    -Execute $PythonPath `
    -Argument $ScriptPath `
    -WorkingDirectory $WorkingDir

# 트리거 정의: 매일 오전 9시
$Trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"

# 설정
$Settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable

# 작업 등록
Write-Host "작업 등록 중: $TaskName"
Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $Action `
    -Trigger $Trigger `
    -Settings $Settings `
    -Description "AI 팀이 Notion 리포트를 분석하여 자동으로 작업을 수행합니다." `
    -RunLevel Highest

Write-Host ""
Write-Host "✅ 설정 완료!"
Write-Host ""
Write-Host "작업 정보:"
Write-Host "  이름: $TaskName"
Write-Host "  실행 시각: 매일 09:00"
Write-Host "  스크립트: $ScriptPath"
Write-Host ""
Write-Host "수동 실행:"
Write-Host "  Start-ScheduledTask -TaskName $TaskName"
Write-Host ""
Write-Host "작업 확인:"
Write-Host "  Get-ScheduledTask -TaskName $TaskName"
Write-Host ""
Write-Host "작업 삭제:"
Write-Host "  Unregister-ScheduledTask -TaskName $TaskName -Confirm:`$false"
