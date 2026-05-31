# 🐾 Connect AI Lab — Monorepo Workspace

이 워크스페이스는 AI 1인 기업 자동화 에이전트 팀(`ai-team`)과 주력 서비스 프로젝트인 펫 힐링 플랫폼(`petnna`)을 함께 관리하는 통합 모노레포 저장소입니다.

---

## 🏗️ 폴더 구조

```text
ai_lab/
├── ai-team/              # 🤖 AI 에이전트 프레임워크 (VS Code Extension 및 실행 도구)
│   ├── _shared/          #   - 에이전트 공용 API 클라이언트 및 라이브러리
│   ├── assets/           #   - 에이전트별 프롬프트 및 도구 실행 스크립트 (tool-seeds)
│   └── src/              #   - VS Code 확장 프로그램 소스 코드
├── petnna/               # 🐶 펫과나 (Pet&Na) 웹/하이브리드 애플리케이션
│   ├── css/              #   - 스타일시트 (Tailwind CSS, Leaflet 등)
│   ├── docs/             #   - 프로젝트 기획, 미팅 로그 및 딥서치 분석 보고서
│   ├── js/               #   - 핵심 기능 컨트롤러 및 뷰 템플릿
│   └── index.html        #   - 웹 앱 메인 엔트리
├── .agent/               # ⚙️ 에이전트 구동에 필요한 로컬 스킬, 메모리 및 도구
├── .claude/              # 🔍 에이전트 컨텍스트 캐시 및 설정
└── .gitignore            # 깃 추적 제외 규칙
```

---

## 🚀 프로젝트 실행 및 관리

### 1. 펫과나 웹 앱 실행 (로컬 웹 서버)
`ai-team`에 내장된 웹 프리뷰 도구를 실행하여 `petnna` 프로젝트를 로컬 브라우저에서 실시간으로 확인할 수 있습니다.

```bash
# 로컬 미리보기 서버 구동 (포트: 8000)
python ai-team/assets/tool-seeds/코다리_개발자/web_preview.py
```
* **미리보기 주소**: [http://localhost:8000](http://localhost:8000)

### 2. 에이전트 상태 및 헬스 체크
에이전트들이 사용하는 LLM(Ollama) 및 텔레그램 연동 상태가 정상인지 진단합니다.

```bash
# Windows 환경에서 한글 깨짐 및 인코딩 오류 방지 설정 후 실행
$env:PYTHONUTF8=1
python ai-team/assets/tool-seeds/코다리_개발자/ollama_health_check.py
```

### 3. 웹 서비스 UI/UX 자동 검토
디자이너 에이전트인 **티모**를 통해 `petnna` 웹 서비스의 UI/UX 완성도 및 사용성을 검수하고 보고서를 생성합니다.

```bash
$env:PYTHONUTF8=1
python ai-team/assets/tool-seeds/티모_디자이너/petnna_reviewer.py
```

---

## 🔒 보안 및 가이드라인
* API 키 및 토큰 정보는 절대 소스코드에 노출하지 않으며, 루트 경로 및 프로젝트 내 `.env` 파일 또는 시스템 환경변수를 사용해 관리합니다.
* 에이전트 자동화 관련 정보(`.agent/`, `.claude/`, `*.pid`)는 깃에 커밋되지 않도록 이그노어 처리되어 있습니다.
