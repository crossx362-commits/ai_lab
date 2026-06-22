#!/usr/bin/env pwsh
# -*- coding: utf-8 -*-
<#
.SYNOPSIS
    Windows Task Scheduler에 매일 새벽 3시 시그널 분석 작업 등록

.DESCRIPTION
    매일 새벽 3시에 시그널 에이전트가 주식+코인 유망종목을 분석하고
    텔레그램으로 보고하도록 스케줄 등록
#>

param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$TaskName = "AI_Lab_Daily_Signal_Report"
$ScriptPath = Join-Path $PSScriptRoot "daily_signal_report.ps1"

if ($Uninstall) {
    Write-Host "🗑️  Task Scheduler에서 작업 제거 중..." -ForegroundColor Yellow
    try {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction Stop
        Write-Host "✅ [$TaskName] 제거 완료" -ForegroundColor Green
    } catch {
        if ($_.Exception.Message -like "*cannot find*") {
            Write-Host "⚠️  작업이 존재하지 않습니다." -ForegroundColor Yellow
        } else {
            throw
        }
    }
    exit 0
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "📅 매일 새벽 3시 시그널 분석 작업 등록" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 기존 작업 제거
try {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
} catch {
    # 무시
}

# 작업 액션 정의
$action = New-ScheduledTaskAction `
    -Execute "pwsh.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`"" `
    -WorkingDirectory "D:\ai_lab"

# 트리거 정의 (매일 새벽 3시)
$trigger = New-ScheduledTaskTrigger -Daily -At "03:00"

# 설정
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable

# 사용자 계정으로 실행 (현재 로그인한 사용자, 관리자 권한 없이)
$principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType S4U

# 작업 등록
Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "매일 새벽 3시 시그널 에이전트가 주식+코인 유망종목 분석 및 텔레그램 보고" `
    -Force | Out-Null

Write-Host ""
Write-Host "✅ Task Scheduler 등록 완료" -ForegroundColor Green
Write-Host ""
Write-Host "📋 작업 정보:" -ForegroundColor Cyan
Write-Host "  • 작업명: $TaskName"
Write-Host "  • 실행시간: 매일 새벽 03:00"
Write-Host "  • 스크립트: $ScriptPath"
$taskState = (Get-ScheduledTask -TaskName $TaskName).State
$stateText = if ($taskState -eq 'Ready') { 'Active' } else { 'Inactive' }
Write-Host "  • 상태: $stateText"
Write-Host ""
Write-Host "🔧 관리 명령어:" -ForegroundColor Yellow
Write-Host "  • 상태 확인: Get-ScheduledTask -TaskName '$TaskName'"
Write-Host "  • 즉시 실행: Start-ScheduledTask -TaskName '$TaskName'"
Write-Host "  • 제거: .\setup_daily_signal_task.ps1 -Uninstall"
Write-Host ""
