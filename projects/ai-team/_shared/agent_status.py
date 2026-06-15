"""
에이전트 현황 조회 유틸리티
telegram_bot_optimized.py에서 사용
"""
import os
import json
from datetime import datetime


def get_status_report(agent: str, project_root: str) -> str:
    """에이전트 현황 보고서 생성"""
    lines = []
    hist_path = os.path.join(project_root, "reports", "history", "upload_history.json")

    def read_history(name: str):
        if not os.path.exists(hist_path):
            return []
        try:
            with open(hist_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return [d for d in data if d.get("agent") == name] if isinstance(data, list) else []
        except Exception:
            return []

    # 루나 (YouTube)
    if "루나" in agent or "전체" in agent:
        luna = read_history("루나")
        if luna:
            last = luna[-1]
            meta = last.get("metadata", {})
            title = meta.get("youtube_title", "?")[:40]
            vid = meta.get("video_id", "")
            url = f"https://youtu.be/{vid}" if vid else ""
            date = last.get("uploaded_at", "")[:16]
            lines.append(f"🎬 <b>루나</b>: {title}\n   {date} | {url}\n   누적 {len(luna)}개")
        else:
            lines.append("🎬 루나: 업로드 기록 없음")

    # 아린 (Instagram)
    if "아린" in agent or "전체" in agent:
        arin = read_history("아린")
        if arin:
            last = arin[-1]
            meta = last.get("metadata", {})
            caption = meta.get("caption", "?")[:30]
            date = last.get("uploaded_at", "")[:16]
            lines.append(f"📸 <b>아린</b>: {caption}\n   {date} | 누적 {len(arin)}개")
        else:
            lines.append("📸 아린: 포스팅 기록 없음")

    # 데이브 (주식/가상자산)
    if "데이브" in agent or "전체" in agent:
        dave_path = os.path.join(project_root, "reports", "research", "dave_upbit_analysis.md")
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
                lines.append(f"📈 <b>데이브</b>: {decision}\n   분석 시각: {mtime}")
            except Exception:
                lines.append("📈 데이브: 분석 파일 읽기 오류")
        else:
            lines.append("📈 데이브: 분석 기록 없음")

    return "\n\n".join(lines) if lines else "조회할 에이전트를 지정하세요 (루나/아린/데이브/전체)"
