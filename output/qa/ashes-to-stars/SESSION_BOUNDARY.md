# 재와 별 — 세션별 작업 경계

> 오너 지시(2026-08-15). **경계는 주제가 아니라 자원으로 긋는다.**
> 2026-08-14에 "개발/그래픽"이라는 주제 경계로 나눴다가 그래픽 세션이 C#(`FieldDecor`·
> `ArenaLayout`·`W3Party`)을 만졌고, 그 커밋(`86097c55`·`9f45af23`)이 개발 세션의
> 미커밋 변경까지 쓸어담았다. 두 세션이 **같은 파일 집합**을 만진 것이 원인이지
> 나눈 것 자체가 원인이 아니었다.

---

## 개발 세션 (`재와 벌 개발 1`)

**독점 자원: 유니티 프로젝트 전체 + 측정**

| 만진다 | 경로 |
|---|---|
| C# 전부 | `projects/ashes-to-stars/unity/Assets/**/*.cs` |
| 씬·프리팹·ProjectSettings | `unity/Assets/**`, `unity/ProjectSettings/**` |
| 배치 빌드·플레이어 실행·측정 | `results/`, `projects/ai-team/skills/마루_게임개발/tools/game_*.py` |
| 유니티 락 | **이 세션만 잡는다** (에디터 열기/닫기, 배치 빌드) |

**안 만진다**: `art/`, `blender/`, 이미지 생성 파이프라인.

## 그래픽 세션 (`재와벌 그래픽 리소스 제작`)

**독점 자원: 아트 원본 + Blender**

| 만진다 | 경로 |
|---|---|
| 아트 생성·정규화·대조 이미지 | `projects/ashes-to-stars/art/**` |
| Blender 스크립트·MCP | `projects/ashes-to-stars/blender/**`, `mcp__blender__*` |
| 완성 스프라이트 반입 | `unity/Assets/_Game/Resources/sprites/**` (**png/meta만**) |

**안 만진다**: **C# 한 줄도 안 고친다.** 씬·프리팹·ProjectSettings도 안 건드린다.
배선이 필요하면 `ORDERS.md`에 「반입 요청」으로 남기고 개발 세션이 한다.

---

## 공용 규칙

1. **커밋은 자기 파일만 지정해서 add하고 곧바로 commit.** `git add -A`·`git commit -a` 금지 —
   이 저장소는 여러 세션이 master에 직접 커밋한다. 스테이징을 방치하면 남의 변경이 딸려 들어간다.
2. **유니티 락은 개발 세션 것.** 그래픽이 유니티로 확인할 게 있으면 `ORDERS.md`에 남기고
   개발 세션의 다음 빌드에 얹는다. 동시에 배치 빌드가 돌면 `exit 21`로 죽는다.
3. **Blender는 그래픽 세션 것.** 개발 세션은 안 켠다.
4. 경계가 애매한 작업이 나오면 **자원을 기준으로** 판단한다 — "C#을 고쳐야 하나?"가 곧
   "개발 세션 몫인가?"다.
