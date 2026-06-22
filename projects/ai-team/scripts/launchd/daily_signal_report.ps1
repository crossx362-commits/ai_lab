#!/usr/bin/env pwsh
# -*- coding: utf-8 -*-
<#
.SYNOPSIS
    매일 새벽 3시 시그널 유망종목 분석 및 텔레그램 보고

.DESCRIPTION
    코인 + 주식 시장 분석 후 텔레그램으로 결과 전송
#>

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 작업 디렉토리 설정
$AI_LAB_ROOT = "D:\ai_lab"
Set-Location $AI_LAB_ROOT

# Python 환경 설정
$env:PYTHONUTF8 = "1"

# 시그널 스크립트 실행
$signalScript = Join-Path $AI_LAB_ROOT "projects\ai-team\skills\시그널_분석가\tools\market_signal.py"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "🔍 [Signal] 유망종목 분석 시작" -ForegroundColor Cyan
Write-Host "시간: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

try {
    # 시그널 실행 (--notify 플래그로 텔레그램 알림 활성화)
    & python $signalScript --notify

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ 시그널 분석 완료" -ForegroundColor Green
    } else {
        Write-Host "⚠️ 시그널 분석 실패 (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "❌ 오류 발생: $_" -ForegroundColor Red
    exit 1
}
