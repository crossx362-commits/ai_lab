"""
에이전트 현황 조회 유틸리티
telegram_bot_optimized.py에서 사용
"""
import os
import json
from datetime import datetime


"""
에이전트 현황 조회 유틸리티
telegram_bot_optimized.py 및 telegram_receiver.py 등에서 사용
"""
import os
import json
import re
import subprocess
from datetime import datetime


def get_status_report(agent: str, project_root: str) -> str:
    """에이전트 현황 보고서 생성"""
    lines = []
    hist_path = os.path.join(project_root, "reports", "history", "upload_history.json")
    learning_path = os.path.join(project_root, "reports", "history", "agent_self_learning_log.md")

    def read_history(name: str):
        if not os.path.exists(hist_path):
            return []
        try:
            with open(hist_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return [d for d in data if d.get("agent") == name] if isinstance(data, list) else []
        except Exception:
            return []

    def get_last_learning(name: str) -> str:
        if not os.path.exists(learning_path):
            return ""
        try:
            with open(learning_path, "r", encoding="utf-8") as f:
                content = f.read()
            # Parse sections: ## YYYY-MM-DD HH:MM 자가학습
            # Look for lines: - **name**: [topic](path)
            sections = re.split(r"##\s+", content)
            for section in reversed(sections):
                if not section.strip():
                    continue
                lines_in_sec = section.split("\n")
                header = lines_in_sec[0].strip()
                for line in lines_in_sec[1:]:
                    if f"**{name}**:" in line or f"**{name} (CEO)**:" in line or f"**{name} (비서)**:" in line or f"**{name} (수사관)**:" in line or f"**{name} (변호사)**:" in line or f"**{name} (디자이너)**:" in line or f"**{name} (관리자)**:" in line or f"**{name} (검수관)**:" in line or f"**{name} (개발자)**:" in line or f"**{name} (인프라)**:" in line or f"**{name} (전략가)**:" in line:
                        # Extract topic
                        match = re.search(r"\[(.*?)\]", line)
                        topic = match.group(1) if match else "자가학습"
                        return f"📚 <b>{name}</b>: [{topic}] 자학습 완료 ({header})"
            return ""
        except Exception:
            return ""

    def mtime_text(path: str) -> str:
        try:
            return datetime.fromtimestamp(os.path.getmtime(path)).strftime("%m/%d %H:%M")
        except Exception:
            return "시간 확인 불가"

    def is_recent(path: str, hours: int = 24) -> bool:
        try:
            age = datetime.now() - datetime.fromtimestamp(os.path.getmtime(path))
            return age.total_seconds() <= hours * 3600
        except Exception:
            return False

    def command_pids(script_filename: str) -> list[int]:
        try:
            needle = script_filename.replace("'", "''").lower()
            cmd = (
                "Get-CimInstance Win32_Process | "
                "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
                f"$_.CommandLine.ToLower().Contains('{needle}') }} | "
                "Select-Object -ExpandProperty ProcessId"
            )
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command", cmd],
                capture_output=True,
                text=True,
                timeout=5,
            )
            return [int(pid) for pid in result.stdout.split() if pid.isdigit()]
        except Exception:
            return []

    def upbit_env_status() -> str:
        access_ok = len(os.getenv("UPBIT_ACCESS_KEY", "")) >= 20
        secret_ok = len(os.getenv("UPBIT_SECRET_KEY", "")) >= 20
        return "Upbit env OK" if access_ok and secret_ok else "Upbit env 확인 필요"

    # 1. 예원 (CEO)
    if "예원" in agent or "CEO" in agent or "전체" in agent:
        learning_info = get_last_learning("예원")
        if learning_info:
            lines.append(f"👑 <b>예원 (CEO)</b>: {learning_info.split(':', 1)[1].strip()}")
        else:
            lines.append("👑 <b>예원 (CEO)</b>: 프로젝트 총괄 관리 중 (대기)")

    # 2. 영숙 (비서)
    if "영숙" in agent or "비서" in agent or "전체" in agent:
        learning_info = get_last_learning("영숙")
        cache_path = os.path.join(project_root, "projects", "ai-team", "_shared", "calendar_cache.md")
        cal_status = "연동 대기"
        if os.path.exists(cache_path):
            cal_status = "연동 완료 (캐시 있음)"
        
        info = f"구글 캘린더 {cal_status}"
        if learning_info:
            info += f" | {learning_info.split(':', 1)[1].strip()}"
        lines.append(f"💼 <b>영숙 (비서)</b>: {info}")

    # 3. 코다리 (개발자)
    if "코다리" in agent or "개발" in agent or "전체" in agent:
        ollama_log = os.path.join(project_root, "reports", "history", "kodari_ollama_log.md")
        prog_path = os.path.join(project_root, "projects", "ai-team", "docs", "progress.md")
        if os.path.exists(ollama_log):
            try:
                with open(ollama_log, "r", encoding="utf-8") as f:
                    content = f.read()
                events = [l.strip() for l in content.splitlines() if l.strip().startswith("- [")]
                latest = events[-1] if events else "Ollama 헬스 로그 있음"
                latest = re.sub(r"^-\s*", "", latest)
                lines.append(f"💻 <b>코다리 (개발자)</b>: Ollama/개발 헬스 {latest}\n   로그 갱신: {mtime_text(ollama_log)}")
            except Exception:
                lines.append("💻 <b>코다리 (개발자)</b>: Ollama 헬스 로그 읽기 오류")
        elif os.path.exists(prog_path):
            try:
                with open(prog_path, "r", encoding="utf-8") as f:
                    content = f.read()
                # Find the first date section
                matches = list(re.finditer(r"##\s+(.*?)\n", content))
                if matches:
                    latest_match = matches[0]
                    # Extract some details
                    header = latest_match.group(1).strip()
                    details = content[latest_match.end():].split("##")[0].strip()
                    summary_lines = [l.strip() for l in details.split("\n") if l.strip() and not l.strip().startswith("#")]
                    summary = " | ".join(summary_lines[:2])
                    lines.append(f"💻 <b>코다리 (개발자)</b>: {header}\n   작업: {summary}")
                else:
                    lines.append("💻 <b>코다리 (개발자)</b>: 개발 진행 중 (기록 없음)")
            except Exception:
                lines.append("💻 <b>코다리 (개발자)</b>: 진척 보고서 읽기 오류")
        else:
            lines.append("💻 <b>코다리 (개발자)</b>: 진척 보고서 없음")

    # 7. 케빈 (인프라)
    if "케빈" in agent or "인프라" in agent or "전체" in agent:
        kevin_path = os.path.join(project_root, "reports", "history", "kevin_monitor_log.md")
        if os.path.exists(kevin_path):
            try:
                with open(kevin_path, "r", encoding="utf-8") as f:
                    content = f.read()
                sections = re.split(r"##\s+", content)
                latest_sec = ""
                latest_dt = None
                for sec in sections:
                    if "케빈 헬스 체크" not in sec:
                        continue
                    first_line = sec.split("\n", 1)[0].strip()
                    match = re.match(r"(\d{4}-\d{2}-\d{2})(?:\s+(\d{2}:\d{2}))?", first_line)
                    if not match:
                        continue
                    time_part = match.group(2) or "00:00"
                    try:
                        sec_dt = datetime.strptime(f"{match.group(1)} {time_part}", "%Y-%m-%d %H:%M")
                    except Exception:
                        continue
                    if latest_dt is None or sec_dt > latest_dt:
                        latest_dt = sec_dt
                        latest_sec = sec
                if latest_sec:
                    sec_lines = [l.strip() for l in latest_sec.split("\n") if l.strip()]
                    header = sec_lines[0].strip()
                    status = " | ".join(sec_lines[1:3])
                    lines.append(f"🏗️ <b>케빈 (인프라)</b>: {header}\n   상태: {status}")
                else:
                    lines.append("🏗️ <b>케빈 (인프라)</b>: 모니터링 로그 분석 실패")
            except Exception:
                lines.append("🏗️ <b>케빈 (인프라)</b>: 모니터링 로그 읽기 오류")
        else:
            lines.append("🏗️ <b>케빈 (인프라)</b>: 모니터링 로그 없음")

    # 8. 티모 (디자이너)
    if "티모" in agent or "디자이너" in agent or "전체" in agent:
        learning_info = get_last_learning("티모")
        if learning_info:
            lines.append(f"🎨 <b>티모 (디자이너)</b>: {learning_info.split(':', 1)[1].strip()}")
        else:
            lines.append("🎨 <b>티모 (디자이너)</b>: UI/UX 검토 대기 중")

    # 9. 현빈 (전략가)
    if "현빈" in agent or "전략" in agent or "전체" in agent:
        intel_path = os.path.join(project_root, "reports", "research", "crypto_market_intel.json")
        hb_path = os.path.join(project_root, "reports", "research", "hyunbin_research.json")
        if os.path.exists(intel_path):
            try:
                with open(intel_path, "r", encoding="utf-8-sig") as f:
                    data = json.load(f)
                ts = data.get("timestamp", "")[:16].replace("T", " ")
                fed = data.get("fed_events", {})
                fear = data.get("fear_greed_index", {})
                kimchi = data.get("kimchi_premium", {})
                risk = fed.get("risk_level", "?")
                status = fed.get("current_status", "시장 조사 중")
                fear_text = f"공포탐욕 {fear.get('value', '?')}({fear.get('classification', '?')})"
                kimchi_text = f"김프 {kimchi.get('premium_pct', '?')}%"
                lines.append(f"📊 <b>현빈 (전략가)</b>: {status} | 위험도 {risk}\n   {fear_text} / {kimchi_text} / 갱신 {ts}")
            except Exception:
                lines.append("📊 <b>현빈 (전략가)</b>: crypto_market_intel 읽기 오류")
        elif os.path.exists(hb_path):
            try:
                with open(hb_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                if isinstance(data, list) and data:
                    last = data[-1]
                    time_str = last.get("timestamp", "")[:16].replace("T", " ")
                    topic = last.get("topic", "?")
                    content = last.get("content", "")
                    content_title = "분석 중"
                    for line in content.split("\n"):
                        if "**주제:**" in line or "**주제" in line:
                            content_title = line.replace("**", "").replace("주제:", "").strip()
                            break
                    lines.append(f"📊 <b>현빈 (전략가)</b>: {topic} 전략 - {content_title}\n   분석 시각: {time_str}")
                else:
                    lines.append("📊 <b>현빈 (전략가)</b>: 리서치 데이터 없음")
            except Exception:
                lines.append("📊 <b>현빈 (전략가)</b>: 리서치 파일 읽기 오류")
        else:
            lines.append("📊 <b>현빈 (전략가)</b>: 리서치 파일 없음")

    # 10. 경수 (수사관) - ECC 보안 스캐너 통합
    if "경수" in agent or "수사" in agent or "전체" in agent:
        security_scan_dir = os.path.join(project_root, "output", "security_scans")
        kyungsu_info = []

        # 최근 보안 스캔 결과 확인
        if os.path.exists(security_scan_dir):
            try:
                scans = sorted(
                    [f for f in os.listdir(security_scan_dir) if f.endswith('.json')],
                    key=lambda f: os.path.getmtime(os.path.join(security_scan_dir, f)),
                    reverse=True,
                )
                if scans:
                    with open(os.path.join(security_scan_dir, scans[0]), "r", encoding="utf-8") as f:
                        scan_data = json.load(f)
                    threat_level = scan_data.get("threat_level", "UNKNOWN")
                    total_scans = scan_data.get("total_scans", 0)
                    scan_time = scan_data.get("scan_timestamp", "")[:16].replace("T", " ")
                    kyungsu_info.append(f"ECC 스캔: {threat_level} ({total_scans}개 항목, {scan_time})")
                else:
                    kyungsu_info.append("보안 스캔 대기 중")
            except Exception:
                kyungsu_info.append("보안 스캔 오류")
        else:
            kyungsu_info.append("ECC 보안 스캐너 준비 완료")

        learning_info = get_last_learning("경수")
        if learning_info:
            kyungsu_info.append(learning_info.split(':', 1)[1].strip())

        lines.append(f"🚨 <b>경수 (수사관)</b>: " + " | ".join(kyungsu_info))

    # 11. 로율 (변호사) - ECC 컴플라이언스 통합
    if "로율" in agent or "변호사" in agent or "전체" in agent:
        compliance_dir = os.path.join(project_root, "output", "compliance_audits")
        royul_info = []

        # 최근 컴플라이언스 감사 결과 확인
        if os.path.exists(compliance_dir):
            try:
                audits = sorted(
                    [f for f in os.listdir(compliance_dir) if f.endswith('.json')],
                    key=lambda f: os.path.getmtime(os.path.join(compliance_dir, f)),
                    reverse=True,
                )
                if audits:
                    with open(os.path.join(compliance_dir, audits[0]), "r", encoding="utf-8") as f:
                        audit_data = json.load(f)
                    risk_level = audit_data.get("risk_level", "UNKNOWN")
                    total_issues = audit_data.get("total_issues", 0)
                    audit_time = audit_data.get("audit_timestamp", "")[:16].replace("T", " ")
                    royul_info.append(f"컴플라이언스: {risk_level} ({total_issues}개 이슈, {audit_time})")
                else:
                    royul_info.append("감사 대기 중")
            except Exception:
                royul_info.append("감사 오류")
        else:
            royul_info.append("ECC 컴플라이언스 감사 준비 완료")

        learning_info = get_last_learning("로율")
        if learning_info:
            royul_info.append(learning_info.split(':', 1)[1].strip())

        lines.append(f"⚖️ <b>로율 (변호사)</b>: " + " | ".join(royul_info))

    # 데이브 (보수적 트레이더)
    if "데이브" in agent or "전체" in agent:
        dave_path = os.path.join(project_root, "reports", "research", "dave_upbit_analysis.md")
        dave_stock = os.path.join(project_root, "reports", "research", "dave_stock_analysis.md")
        dave_log = os.path.join(project_root, "output", "trading_logs", "dave_daemon.out.log")
        intel_path = os.path.join(project_root, "reports", "research", "crypto_market_intel.json")
        dave_info = []
        pids = command_pids("upbit_auto_trader.py")
        if pids:
            dave_info.append(f"실거래 감시 실행 중 PID {', '.join(map(str, pids))}")
        dave_info.append(upbit_env_status())
        if os.path.exists(intel_path):
            dave_info.append(f"현빈 시장정보 {mtime_text(intel_path)}")
        if os.path.exists(dave_log):
            dave_info.append(f"로그 {mtime_text(dave_log)}")
        if os.path.exists(dave_path) and is_recent(dave_path):
            try:
                with open(dave_path, "r", encoding="utf-8") as f:
                    content = f.read()
                decision = "알 수 없음"
                for line in content.split("\n"):
                    if "최종 결정" in line:
                        decision = line.split(":")[-1].strip()[:50]
                        break
                mtime = datetime.fromtimestamp(os.path.getmtime(dave_path)).strftime("%m/%d %H:%M")
                dave_info.append(f"가상자산: {decision} ({mtime})")
            except Exception:
                dave_info.append("가상자산: 분석 오류")
        if os.path.exists(dave_stock) and is_recent(dave_stock):
            try:
                with open(dave_stock, "r", encoding="utf-8") as f:
                    content = f.read()
                decision = "알 수 없음"
                for line in content.split("\n"):
                    if "최종" in line or "결정" in line:
                        decision = line.strip()[:50]
                        break
                mtime = datetime.fromtimestamp(os.path.getmtime(dave_stock)).strftime("%m/%d %H:%M")
                dave_info.append(f"주식: {decision} ({mtime})")
            except Exception:
                dave_info.append("주식: 분석 오류")

        if dave_info:
            lines.append(f"📈 <b>데이브 (보수적 트레이더)</b>: " + " | ".join(dave_info))
        else:
            lines.append("📈 <b>데이브 (보수적 트레이더)</b>: 분석 기록 없음")

    # 레오 (공격적 단타 트레이더)
    if "레오" in agent or "전체" in agent:
        leo_path = os.path.join(project_root, "reports", "research", "leo_trades.json")
        leo_log = os.path.join(project_root, "output", "trading_logs", "leo_daemon.out.log")
        leo_info = []
        pids = command_pids("leo_aggressive_trader.py")
        if pids:
            leo_info.append(f"실거래 단타 감시 실행 중 PID {', '.join(map(str, pids))}")
        leo_info.append(upbit_env_status())

        # 거래 기록 확인
        if os.path.exists(leo_path):
            try:
                with open(leo_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                if isinstance(data, list) and data:
                    last_trade = data[-1]
                    ticker = last_trade.get("ticker", "?")
                    decision = last_trade.get("decision", "?")
                    profit = last_trade.get("profit_pct", 0)
                    timestamp = last_trade.get("timestamp", "")[:16].replace("T", " ")
                    leo_info.append(f"{ticker} {decision} ({profit:+.1f}% at {timestamp})")
                else:
                    leo_info.append("거래 기록 없음")
            except Exception:
                leo_info.append("거래 기록 오류")

        # 로그 파일 확인
        if os.path.exists(leo_log):
            try:
                mtime = datetime.fromtimestamp(os.path.getmtime(leo_log)).strftime("%m/%d %H:%M")
                leo_info.append(f"로그 파일: {mtime}")
            except Exception:
                pass

        if leo_info:
            lines.append(f"⚡ <b>레오 (공격적 단타)</b>: " + " | ".join(leo_info))
        else:
            lines.append("⚡ <b>레오 (공격적 단타)</b>: 대기 중")

    return "\n\n".join(lines) if lines else "조회할 에이전트를 지정하세요 (예원/영숙/코다리/케빈/티모/현빈/경수/로율/데이브/레오/전체)"

