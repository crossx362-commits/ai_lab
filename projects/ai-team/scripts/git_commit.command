#!/bin/bash
cd ~/ai_lab
rm -f .git/index.lock 2>/dev/null
git add -A
git commit -m "Signal→Dave/Leo 학습 피드백 루프 구축 + 에이전트 안정화

주요 변경사항:
- trading_entry_evaluator.py: 학습 임계값 동적 로드 (market_signal.json -> learning_insights)
- signal_learner.py: 백테스트 분석 -> 최적 임계값/코인 도출 신규 모듈
- market_signal.py: learning_insights 포함, SUI/SEI/PEPE 추가
- leo_aggressive_trader.py: NEAR/SUI 추가, avoid_coins 필터, 학습 기반 종목 선정
- upbit_auto_trader.py: avoid_coins/top_coins 학습 반영
- agent_health_monitor.py: launchctl kickstart 방식으로 restart, 10분 주기
- youngsuk_launcher.sh: 다중 python 버전 탐색 + 패키지 자동 설치
- com.ailab.youngsuk.plist: ThrottleInterval 30s
- com.ailab.somi.plist: 하루 3회 (8/14/21시)
- com.ailab.kodari.plist: 10분 주기
- com.ailab.harness.plist: 신규 (9시/21시 점검)
- harness/check_all.py: WARN/FAIL 텔레그램 알림
- fix_agents.command: 전체 plist 리로드 스크립트"
git push
echo "완료!"
read -p "엔터로 닫기"
