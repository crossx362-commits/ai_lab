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

    # 3. 루나 (YouTube)
    if "루나" in agent or "전체" in agent:
        luna = read_history("루나")
        if luna:
            last = luna[-1]
            meta = last.get("metadata", {})
            title = meta.get("youtube_title", "?")[:40]
            vid = meta.get("video_id", "")
            url = f"https://youtu.be/{vid}" if vid else ""
            date = last.get("uploaded_at", "")[:16]
            lines.append(f"🎬 <b>루나 (디렉터)</b>: {title}\n   {date} | {url}\n   누적 {len(luna)}개")
        else:
            lines.append("🎬 <b>루나 (디렉터)</b>: 업로드 기록 없음")

    # 4. 아린 (Instagram)
    if "아린" in agent or "전체" in agent:
        arin = read_history("아린")
        if arin:
            last = arin[-1]
            meta = last.get("metadata", {})
            caption = meta.get("caption", "?")[:30]
            date = last.get("uploaded_at", "")[:16]
            lines.append(f"📸 <b>아린 (관리자)</b>: {caption}\n   {date} | 누적 {len(arin)}개")
        else:
            lines.append("📸 <b>아린 (관리자)</b>: 포스팅 기록 없음")

    # 5. 가희 (검수관)
    if "가희" in agent or "검수" in agent or "전체" in agent:
        ins_path = os.path.join(project_root, "reports", "inspection", "petnna_inspection_report.md")
        if os.path.exists(ins_path):
            try:
                with open(ins_path, "r", encoding="utf-8") as f:
                    content = f.read()
                time_str = "알 수 없음"
                violation = "0건"
                for line in content.split("\n"):
                    if "검수 일시" in line:
                        time_str = line.split(":")[-1].strip()
                    elif "규칙 위반" in line:
                        violation = line.split(":")[-1].strip()
                lines.append(f"🔍 <b>가희 (검수관)</b>: 최근 검수 완료 ({time_str}) | 규칙 위반: {violation}")
            except Exception:
                lines.append("🔍 <b>가희 (검수관)</b>: 검수 보고서 읽기 오류")
        else:
            learning_info = get_last_learning("가희")
            if learning_info:
                lines.append(f"🔍 <b>가희 (검수관)</b>: {learning_info.split(':', 1)[1].strip()}")
            else:
                lines.append("🔍 <b>가희 (검수관)</b>: 검수 기록 없음")

    # 6. 코다리 (개발자)
    if "코다리" in agent or "개발" in agent or "전체" in agent:
        prog_path = os.path.join(project_root, "projects", "ai-team", "docs", "progress.md")
        if os.path.exists(prog_path):
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
                for sec in reversed(sections):
                    if "케빈 헬스 체크" in sec:
                        latest_sec = sec
                        break
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
        hb_path = os.path.join(project_root, "reports", "research", "hyunbin_research.json")
        if os.path.exists(hb_path):
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

    # 10. 경수 (수사관)
    if "경수" in agent or "수사" in agent or "전체" in agent:
        learning_info = get_last_learning("경수")
        if learning_info:
            lines.append(f"🚨 <b>경수 (수사관)</b>: {learning_info.split(':', 1)[1].strip()}")
        else:
            lines.append("🚨 <b>경수 (수사관)</b>: 악성 댓글 모니터링 중")

    # 11. 로율 (변호사)
    if "로율" in agent or "변호사" in agent or "전체" in agent:
        learning_info = get_last_learning("로율")
        if learning_info:
            lines.append(f"⚖️ <b>로율 (변호사)</b>: {learning_info.split(':', 1)[1].strip()}")
        else:
            lines.append("⚖️ <b>로율 (변호사)</b>: 법률 및 저작권 검토 대기 중")

    # 데이브 (주식/가상자산) - 별도 지정 시 또는 데이브 검색 시 추가
    if "데이브" in agent or "전체" in agent:
        dave_path = os.path.join(project_root, "reports", "research", "dave_upbit_analysis.md")
        dave_stock = os.path.join(project_root, "reports", "research", "dave_stock_analysis.md")
        dave_info = []
        if os.path.exists(dave_path):
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
        if os.path.exists(dave_stock):
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
            lines.append(f"📈 <b>데이브 (주식/코인)</b>: " + " | ".join(dave_info))
        else:
            lines.append("📈 <b>데이브 (주식/코인)</b>: 분석 기록 없음")

    return "\n\n".join(lines) if lines else "조회할 에이전트를 지정하세요 (예원/영숙/루나/아린/가희/코다리/케빈/티모/현빈/경수/로율/데이브/전체)"

