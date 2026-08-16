# 재와 별 (Ashes to Stars)

유니티 2D 쿼터뷰 뱀서라이크 + 파티 RPG. 폴더 지도만 여기에 둔다.
기획·헌법은 `docs/DESIGN.md`, 원장은 `docs/GAME_DESIGN_ASHES_TO_STARS.md`.

## 어디에 무엇이 있나

| 경로 | 역할 | 커밋 |
|---|---|---|
| `unity/` | 게임 소스. 에디터가 여는 원본 | ✅ |
| `unity/Assets/Resources/` | 런타임이 `Resources.Load`로 읽는 **유일한** 아트·데이터 | ✅ |
| `unity/Assets/_Game/` | 화면·데이터·에디터 자가검사 | ✅ |
| `unity/Assets/Scripts/` | 전투 본체(`W3Party` 등). 대화 세션 소유 | ✅ |
| `art/` | 힉스필드 생성 원본·spec·후처리 스크립트 | ✅ |
| `art/out_*` | 재생성·비교용 원본. 게임은 안 읽는다 | ✅ (삭제 금지) |
| `blender/` | 옛 플레이스홀더 비교본만. 신규 최종 아트에 쓰지 않음 | ✅ |
| `unity_meas/` | `sync_meas.sh`로 만든 측정 사본 | ❌ gitignore |
| `build_*/` `results/` | 빌드·측정 산출물 | ❌ gitignore |

루트 `./tools/qa_shot.sh`는 루프가 부르는 라이브 경로라 여기로 옮기지 않는다.

## 두지 마라

- **`unity/Assets/_Game/Art/`** — `Resources.Load`가 못 읽는다. 스프라이트를 넣으면 화면은 플레이스홀더.
- **프로젝트 루트의 `out_*`** — 생성 원본은 `art/out_*`만.
- **`unity/Assets/Screenshots/`** — 에디터 Play 캡처. 정본은 `output/qa/ashes-to-stars/`.
- 루트 `_to_delete/` · `My project/` · `qa_vfx_live/` — 저장소 밖 잔재. 2026-08-16 삭제.
- `Resources/sprites/ranged/` · `dps_new/` — JOB_DIRS에 없는 미완성 잔재. 2026-08-16 삭제.
- `Resources` 안의 소비처 0곳(estate 통·상자, 옛 FX 장수, `normal/skill` 원본 시트, 안 까는 전투 배경 아틀라스). 2026-08-16 삭제. 오너 시트는 `blender/source_sheets/`.

검사: `python3 projects/ai-team/skills/마루_게임개발/tools/game_asset_names.py --strict`
