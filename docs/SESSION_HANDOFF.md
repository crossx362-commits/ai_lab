# 세션 인수인계 — 5직업 스프라이트 모션 8프레임 통일 (2026-08-21, 진행 중)

> 오너 지시: "모션마다 이미지 수가 왜 달라, 다르면 안 되지" + "제대로 세봐라".
> 실측 완료: 원본 시트(`unity/Assets/TestSpriteSheets/`) 30장은 **전부 4×2=8프레임 균일**.
> 게임 폴더는 idle/walk/attack/special 6 · dash 4 · hurt/death/invuln 1 — 6칸 옛 계약에
> 맞추느라 2장씩 버린 것이 원인. 코드 계약을 8칸으로 넓히는 작업 도중 세션 종료.

## 완료 (이 세션)
- 시트 30장 반입·일괄 애니 빌드(`acb69886`), 전투 프레임 교체(`6ce16c74`),
  초상화 교체(`de3bc1eb`), 모션 순환 셀프체크 추가(`c8956851`) — 전부 커밋됨.
- **SpriteBank.cs 8칸 확장 — 수정 완료, 컴파일 PASS, ⚠️ 미커밋**
  (`projects/ashes-to-stars/unity/Assets/Scripts/SpriteBank.cs`):
  - Frame enum: Idle/Walk/Atk/Sp 각 8칸 + Death0~7(총 46칸), 별칭 유지
  - JOB_FRAMES: `*_06`/`*_07` + `death_00~07` 추가
  - Have/HaveDir 호출부 max 6→8, 캐릭터 death가 몹처럼 8장 재생 후 시체 유지
  - 총길이 유지: AtkFrame 0.0625(8장=0.50s 오너 지정), SpFrame 0.125(=1.00s),
    DeathFrame 0.1f 신설

## 다음 단계 (순서대로)
1. **프레임 재추출** — 게임 폴더에 아직 `*_06/_07`, `death_01~07`이 없다(코드는 Have로
   6장에서 멈춰 동작엔 문제 없음). PIL 스크립트로 시트에서 다시 잘라 넣는다:
   - 소스 `Assets/TestSpriteSheets/{tanker,assassin,magican,supporter,healer}_*.png`
     → 대상 `Assets/Resources/sprites/{tank,dps,mage,buffer,healer}/`
   - 매핑: idle→idle_00~07, run→walk_00~07, attack→attack_00~07, skill→special_00~07,
     death→death_00~07(마지막 장이 시체), dash=run[0,2,4,6], hurt=death[0],
     invuln=idle_01[0]. 셀 분할 round(c*w/4)/round(r*h/2), 높이 124px 정규화(LANCZOS).
   - dash·hurt·invuln은 **원본 시트가 없어** 파생 유지 — 오너에게 이 3종은 시트가
     없다는 사실을 보고에 명시할 것.
2. `MotionCycleSelfCheck.cs` 기대치 6→8로, Death 8장 순환 검사 추가
   (`unity/Assets/_Game/Scripts/Editor/MotionCycleSelfCheck.cs`, rect 기준 — name은 빈 문자열 함정).
3. 배치 임포트(신규 png의 .meta 생성) 후 `AshesToStars.JobAnimSelfCheck.Run`·
   `AshesToStars.MotionCycleSelfCheck.Run` 배치 실행 PASS 확인.
4. 커밋: SpriteBank.cs + MotionCycleSelfCheck.cs + sprites png/meta (다른 세션 WIP인
   TextureImportRules.cs·estate 파일들은 건드리지 말 것 — 파일 지정 add).
5. `PlayableScenesBuilder.BuildGame` 배치 재빌드 → `build_game/AshesToStars.app`
   `--auto dungeon --shots` 스샷 + 직업별 GIF 재생성(8프레임)으로 오너에게 증빙.
6. `docs/GAME_WORKLOG.md`의 2026-08-20 항목 아래에 결과 추가.

## 절차 메모
- 유니티 배치 전 `ls unity/Temp/UnityLockfile` — GUI가 열려 있으면
  `osascript -e 'tell application "Unity" to quit'` 후 대기, 끝나면 `open -na ...`로 재오픈.
- 배치 실행 예: `Unity -batchmode -projectPath .../unity -executeMethod <메서드> -logFile <로그> -quit`
- 에디터 `/Applications/Unity/Hub/Editor/6000.5.6f1/`, 검사기 `game_asset_names.py`,
  스샷 폴더 `output/qa/ashes-to-stars/new_sprites_shots/`.
