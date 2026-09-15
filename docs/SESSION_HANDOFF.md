# SESSION HANDOFF — tankfall (2026-09-15)

이어받은 세션은 완료한 항목을 지우고, 전부 끝나면 이 파일을 비운다.

## 현재 상태
- 프로젝트: `projects/tankfall/` (유니티 `unity/`), 명세 `docs/GAME_SPEC_TANK_ARTILLERY.md` **§2-9 부터 읽을 것**.
- 오너 지시(이 세션): 포트리스2 탱크 13종·시스템 그대로 반영 → 완료. 탱크 외형은 참고 이미지(둥근 장난감) → 완료(오너 피드백 대기).
- 존댓말. 매치업 결과는 나오는 대로 보고. 커밋은 오너가 시킬 때만(아직 안 시킴 — 워킹트리에 미커밋 변경 다수).

## 미커밋 변경 (커밋 지시 오면 한 커밋으로)
- Sim: `TankStats.cs`(13종·방어력·딜레이), `TurnOrder.cs`(신규), `ShellEffects.cs`(신규), `NiceShot.cs`(신규), `AiGunner.cs`, `ProjectileSimulator.cs`(FromFall·AfterDefense)
- View: `ProceduralTank.cs`(장난감 비례·챔퍼), `BattleDemo.cs`(딜레이 턴제·SS·효과·다탄두·갤러리 13종)
- tools: `BattleSimVerify.cs`(12×12 매치업·[6-1] 판별·네거티브 컨트롤 열), `verify.sh`(turn/shell/nice 타깃), `TurnOrderVerify.cs`·`ShellEffectsVerify.cs`·`NiceShotVerify.cs`(신규)
- docs: `GAME_SPEC_TANK_ARTILLERY.md` §2-9~§2-9-3, `HARNESS_GUARDRAILS_LEDGER.md` 2026-09-15 절
- ⚠️ `.meta` 3개(신규 Sim 파일) 같이 커밋. `.env*` 절대 금지.

## 진행 중 / 다음
1. 오너 보고 완료: v2(§2-9-4)·v3(§2-9-4-1)·v4(§2-9-4-2, 최종 29~77%). 2026-09-15 저녁 오너 지시로 중단·커밋.
2. 단일 변수 실험(§2-9-5) 결론: 이온 굴착 12→10 채택, 캐롯 2번탄 SpBase 0.95→0.45 채택, 레이저 2번탄은 원인 아님(보류), 듀크 독구름 장판화(원작대로, 승률 무변화), **듀크 꼴찌 원인 = 각도 상한 40° 가 하네스 맵 언덕에 막힘**(40°→55° 로 명중 46→71%·승률 30→74%). 각도는 원작 값이므로 고칠 곳은 **맵**(스폰 옆 언덕) — §맵 3종 작업으로 넘김.
3. 데모 연출 완료: 다탄두 부탄 비행, 위성탄 빔, 설치물(지뢰 공·장판 구슬 고리 — 원반은 사발 크레이터에 묻혀 안 보였음), HUD 태그 글자화, `-roster`·`-forcespecial`.
4. 미구현: 맵 3종(스폰 대칭·저각 기종 성립), 아이템/헬리콥터, 날씨(눈), 궁극기 §49, 네트워크. 독구름 바람 흐름.
5. 탱크 외형은 오너 참고 이미지 기준 1차 완료 — 피드백 대기.
- 단일 행 실험: `TANKFALL_ROW=Duke TANKFALL_VAR=MaxPitch TANKFALL_VALUES=40,55 ./tools/verify.sh battle`(셀당 40판, 행 평균 ±2.5%p). 필드: CraterRadius·BlastRadius·BaseDamage·DirectDamage·Defense·Delay·Hp·Min/MaxPitch·Gravity/Wind/PowerScale·MoveSpeed·Sp*. 하네스가 덮기만 하고 Sim 은 결론 뒤 한 번만 고친다.

## 검증 명령
```bash
./projects/tankfall/tools/verify.sh compile   # 유니티 Play 가능 여부
./projects/tankfall/tools/verify.sh turn      # 딜레이 턴제
./projects/tankfall/tools/verify.sh shell     # 2번탄 효과 29항목
./projects/tankfall/tools/verify.sh nice      # 나이스샷 38항목
./projects/tankfall/tools/verify.sh battle    # 매치업(오래 걸림)
```
빌드: `Unity.exe -batchmode -quit -nographics -projectPath D:\ai_lab\projects\tankfall\unity -executeMethod Tankfall.EditorTools.BuildScript.BuildWindows`
갤러리/자동사격: `unity/Build/Tankfall.exe -gallery|-autoshot -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile -`
⚠️ 실행 중인 `verify.sh` 를 편집하지 마라(bash 는 실행하며 읽는다).
