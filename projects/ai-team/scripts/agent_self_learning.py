#!/usr/bin/env python3
"""
agent_self_learning.py
에이전트들이 유휴 시간에 자가학습 → 스킬 문서 생성 → 지식 축적

Usage: python3 projects/ai-team/scripts/agent_self_learning.py
"""
import json, os, re, sys, random
from datetime import datetime
from pathlib import Path

ROOT    = Path(__file__).resolve().parents[3]
AI_TEAM = ROOT / "projects/ai-team"
sys.path.insert(0, str(AI_TEAM))

from _shared.env_loader import load_env
from _shared import ollama_client as lm
load_env()

SKILLS_DIR = AI_TEAM / "skills"
LOG_FILE   = ROOT / "reports/history/agent_self_learning_log.md"

AGENTS = [
    {"name": "현빈", "dir": "현빈_전략가",   "specialty": "비즈니스 전략, 시장 분석, 수익화 모델, 경쟁사 분석", "task": ""},
    {"name": "코다리", "dir": "코다리_개발자", "specialty": "풀스택 개발, Python/TypeScript, 아키텍처 설계, 코드 품질", "task": "coding"},
    {"name": "루나",  "dir": "루나_디렉터",   "specialty": "뮤직비디오 기획, 크리에이터 전략, 유튜브 콘텐츠", "task": ""},
    {"name": "아린",  "dir": "아린_관리자",   "specialty": "소셜미디어 운영, 인스타그램, 팬 커뮤니티 관리", "task": ""},
    {"name": "티모",  "dir": "티모_디자이너", "specialty": "UI/UX 디자인, 시각 브랜딩, 컬러 이론, 타이포그래피", "task": ""},
    {"name": "가희",  "dir": "가희_검수관",   "specialty": "콘텐츠 QA, 품질 기준, 반려동물 건강 정보 검수", "task": ""},
    {"name": "경수",  "dir": "경수_수사관",   "specialty": "보안 분석, 리스크 탐지, 이상행동 모니터링", "task": ""},
    {"name": "영숙",  "dir": "영숙_비서",     "specialty": "일정 관리, 보고서 작성, 팀 커뮤니케이션 조율", "task": ""},
    {"name": "로율",  "dir": "로율_변호사",   "specialty": "법률 리뷰, 계약 분석, 저작권, 컴플라이언스", "task": ""},
    {"name": "예원",  "dir": "예원_CEO",      "specialty": "전략적 의사결정, 팀 리더십, 비전 수립, OKR", "task": ""},
    {"name": "케빈",  "dir": "케빈_인프라",   "specialty": "인프라 관리, DevOps, CI/CD, 서버 모니터링", "task": ""},
]


def slugify(text: str) -> str:
    text = re.sub(r"[^\w가-힣\-]", "_", text.strip())
    return re.sub(r"_+", "_", text)[:40]


def learn(agent: dict) -> dict:
    name      = agent["name"]
    specialty = agent["specialty"]
    task      = agent["task"]
    result    = {"agent": name, "status": "skip", "topic": "", "file": ""}

    if not lm.is_available():
        result["status"] = "no_ollama"
        return result

    # 1. 학습 주제 결정
    topic = lm.chat(
        f"{name}는 [{specialty}] 전문가입니다. "
        f"지금 자가학습할 새로운 스킬/지식 주제 하나를 10자 이내로 제안하세요. "
        f"주제 이름만 출력하세요.",
        task=task, temperature=0.9,
    )
    if not topic:
        result["status"] = "no_topic"
        return result
    topic = topic.strip().replace("/", "_")[:30]
    result["topic"] = topic

    # 2. 스킬 지식 문서 생성
    doc = lm.chat(
        f"에이전트: {name} ({specialty})\n"
        f"학습 주제: {topic}\n\n"
        f"이 주제에 대해 실무에서 바로 쓸 수 있는 스킬 지식 문서를 마크다운으로 작성하세요.\n"
        f"형식:\n"
        f"# {topic}\n\n"
        f"## 핵심 개념\n(3-5개 핵심 포인트)\n\n"
        f"## 실전 적용\n(구체적 사용 방법, 예시)\n\n"
        f"## {name}에게 유용한 이유\n(이 에이전트 역할에서의 활용)\n\n"
        f"문서만 출력하세요. 500자 이내.",
        task=task, temperature=0.75,
    )
    if not doc or len(doc.strip()) < 50:
        result["status"] = "no_doc"
        return result

    # 3. 파일 저장
    date_str  = datetime.now().strftime("%Y%m%d")
    filename  = f"learned_{date_str}_{slugify(topic)}.md"
    out_dir   = SKILLS_DIR / agent["dir"] / "knowledge"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path  = out_dir / filename

    out_path.write_text(
        f"---\nlearned_at: {datetime.now().isoformat()}\nagent: {name}\ntopic: {topic}\n---\n\n"
        + doc.strip() + "\n",
        encoding="utf-8",
    )
    result["status"] = "ok"
    result["file"]   = str(out_path.relative_to(ROOT))
    return result


def main():
    print(f"\n🧠 에이전트 자가학습 — {datetime.now().strftime('%Y-%m-%d %H:%M')}")

    if not lm.is_available():
        print("⚠️  Ollama 미실행 — 종료")
        return

    batch   = random.sample(AGENTS, min(3, len(AGENTS)))
    results = []

    for agent in batch:
        print(f"\n[{agent['name']}] 학습 중...")
        r = learn(agent)
        if r["status"] == "ok":
            print(f"  ✅ {r['topic']} → {r['file']}")
        else:
            print(f"  ⚠️  {r['status']}")
        results.append(r)

    # 로그 저장
    LOG_FILE.parent.mkdir(parents=True, exist_ok=True)
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(f"\n\n## {datetime.now().strftime('%Y-%m-%d %H:%M')} 자가학습\n")
        for r in results:
            if r["status"] == "ok":
                f.write(f"- **{r['agent']}**: [{r['topic']}]({r['file']})\n")
            else:
                f.write(f"- **{r['agent']}**: {r['status']}\n")

    ok = sum(1 for r in results if r["status"] == "ok")
    print(f"\n✅ 완료 — {ok}/{len(batch)}명 학습")


if __name__ == "__main__":
    main()
