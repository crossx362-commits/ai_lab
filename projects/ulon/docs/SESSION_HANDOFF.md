# SESSION HANDOFF — Ulon

작업 루프: 한 항목이 끝나면 기획서 17장에서 다음을 고르고 바로 구현한다.

## 현재 상태
- 검술 전투, FishNet, persist, 캐릭터 생성, 채광/벌목, 제작, 거래, 은행, 주문책, 유령/시체/부활, 무게/도구, 광맥 리스폰, 시약 채집까지 슬라이스에 들어 있음.
- 시체 회수 UX: 누운 캐릭터 메시(캡슐 금지), HUD에 방향·거리, 유령은 치유사 안내, 시체 15분 소멸.
- Party 최소: 리더 초대(동료 자동 수락), HP 표시, 파티 말, 파티원 시체 룻 공유. 파티 밖은 loot_right.
- 운영툴 최소: F1 GM(워프/지급/회수/스킬/스켈 소환삭제/정지/백업), oplog(거래·제작·시체), 계정 정지 파일, data/backups.
- Closed Alpha 로컬 준비: `tools/closed_alpha_smoke.sh` (postgres+persist+백업). 클라 `-ulon-host <LAN>`. 외부 배포는 안 함.
- 명성/카르마/노토라이어티 persist. 가드존은 광장 반경 16m. 무고 공격→범죄+가드 타격. 몬스터 처치 명성+10. Open PvP 꺼짐.
- 자원 노드: Remaining 0이면 숨고, RespawnSeconds(8초) 후 Capacity로 재생. 광맥 12, 나무 12, 수지 덤불 8.
- 시약: `ResinBush`(Kenney 덤불) 클릭 → resin, 마법 스킬 상승. 도구 불필요. 주문 시전은 resin 소비.
- 씬: IronVein, Forge, OakTree, ResinBush, Banker=풍차, Healer=분수.
- 중앙 마을 1(시스템 루프가 이 안에서 돈다). 메뉴 `Ulon/Dress Village`.
  - 광장 스폰/거래(동료) → 서쪽 대장간·잡화 상점 → 북서 은행 → 남쪽 치유
  - 동쪽 광맥, 북동 벌목, 광장 옆 시약, 북쪽 문 밖 사냥
  - Gold. 상점: 곡괭이/도끼/시약 구매, 광석/나무 판매. 시작금 40.
  - 훈련사(광장 동쪽 Mage): 5G에 스킬 +1, 상한 30, 스탯은 안 오름.
  - 집 7채 + 울타리/대문. 바닥은 Unity Terrain + 노이즈 하이트맵(광장 평평). 광장만 돌길 타일.
  - 필드 3/던전은 아직 안 함.

## 월드 비주얼 금지 (오너 지시 2026-09-01, 앞으로 절대)
단색 초록 Plane, Default-Material 프리미티브, Kenney 시안/주황 무텍스처, 집으로 안 읽히는 벽 타일 더미는 화면에 두지 않는다. `Dress Village`가 `AssertVillageVisuals`로 막는다.
오너에게 보여주는 화면은 플레이 3/4만. Kenney 샘플 밀도(건물·돌길·나무·소품이 붙을 것).

## 방금 고른 다음 일
명성/가드존 최소는 들어 있음. Open PvP·필드 확장(17.10)은 보류. 다음은 결투/길드전 데이터가 아니라 17.10이 열리기 전까지 마을 루프 안정화.

## 핵심 경로
- 기획 원장: `projects/ulon/docs/GAME_DESIGN.md`
- 확정안: `projects/ulon/docs/DESIGN.md`
- Unity: `projects/ulon/unity`
- 2클라 검증: `projects/ulon/tools/two_client_check.sh`
- Alpha 스모크: `projects/ulon/tools/closed_alpha_smoke.sh`
- 빌드: 메뉴 `Ulon/Build Dedicated Server + Client` → `projects/ulon/builds/client/UlonClient.app`
