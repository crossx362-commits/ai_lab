import os

shared_file = r"d:\ai_lab\ai-team\_shared\공통_스킬_지식.md"

new_content = """
---

## 10. 프론트엔드 UI 템플릿 킷 (Shared UI Kits)

모든 에이전트(특히 코다리, 티모 등 개발/디자인 에이전트)는 새로운 웹/앱 프로젝트를 구성하거나 UI를 설계할 때 아래에 사전 구축된 템플릿 킷을 재사용하여 생산성을 극대화해야 한다.

### [A] Developer UI Kits (React / TypeScript)
위치: `ai-team/assets/brain-seeds/40_템플릿/developer/`
고도로 모듈화된 4가지 프론트엔드 UI 킷이 준비되어 있다. 각 킷 내부의 `README.md` 및 `manifest.json`을 참조하여 컴포넌트를 조립할 것.

1. **`dashboard-kit`**: 관리자 대시보드용 컴포넌트 (`Sidebar`, `Topbar`, `StatsCards`, `RecentTable` 등)
2. **`landing-kit`**: 서비스 랜딩 페이지용 컴포넌트 (`Hero`, `Features`, `Pricing`, `FAQ`, `CTA` 등)
3. **`mobile-kit`**: 모바일 웹/앱 스크린 뷰 (`HomeScreen`, `ProfileScreen`, `SettingsScreen` 등)
4. **`portfolio-kit`**: 개인/기업 포트폴리오용 컴포넌트 (`About`, `Skills`, `Work`, `Contact` 등)

### [B] Neon Survivor Kit (HTML)
위치: `ai-team/assets/templates/neon-survivor-kit/`
네온(Neon) 테마가 적용된 단일 랜딩/게임 UI 템플릿(`template.html`). 강렬하고 화려한 시각적 효과가 필요한 이벤트 페이지나 서바이벌 게임 컨셉의 UI를 제작할 때 즉시 활용한다.

**활용 지침:**
- UI 컴포넌트 코드가 필요할 경우, 위 경로의 `files` 디렉토리에서 `.tsx` 코드를 직접 읽어 프로젝트(src/components)로 복사하여 사용한다.
- 템플릿을 무작정 새로 짜지 말고, 기존 킷을 먼저 조합하여 뼈대를 잡은 후 세부 디자인을 커스텀한다.
"""

with open(shared_file, "r", encoding="utf-8") as f:
    content = f.read()

if "프론트엔드 UI 템플릿 킷" not in content:
    content += new_content
    with open(shared_file, "w", encoding="utf-8") as f:
        f.write(content)
    print("Successfully appended UI Kits knowledge to 공통_스킬_지식.md")
else:
    print("Knowledge already exists.")
