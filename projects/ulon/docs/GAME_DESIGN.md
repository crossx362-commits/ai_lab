---
title: 울온 철학 기반 저폴리 3D 온라인 샌드박스 RPG 통합 기획서
version: v1.1
date: 2026-08-31
status: 현재 프로젝트 기준 문서
source: 울온_철학_저폴리3D_온라인_샌드박스_RPG_통합기획서_v1.1_UO보완_검토완료.docx
---

울온 철학 기반
# 저폴리 3D 온라인 샌드박스 RPG
통합 기획서 · 개발 계획 · 리소스 사용 계획
v1.1  |  기준일 2026-08-31  |  UO Classic 핵심 시스템 보완판

| 문서 상태  이 문서를 현재 프로젝트의 새 기준 문서로 사용합니다. 기존 v0.2의 2D/LPC 방향은 프로토타입 검토 이력으로만 보존하고, 현재 그래픽 기준은 저폴리 3D + 고정 3/4 쿼터뷰입니다. |
| --- |

| 한 줄 정의  단순한 저폴리 3D 세계에서 직업을 고르지 않고 행동으로 스킬을 성장시키며, 사냥·채집·제작·거래가 서로 연결되는 소규모 지속형 온라인 샌드박스 RPG. |
| --- |

참고 철학: 1999~2002년 전후 Classic Ultima Online의 스킬 성장·STR/DEX/INT·죽음/시체 회수·제작 경제·명성/범죄·지속형 월드를 주 기준으로 삼고, 이후 UO의 BOD/가공/편의 기능은 선택적으로 참고합니다. 이름, 세계관, 그래픽, 음악, 고유 재료명 등 IP 요소는 복제하지 않고 독자 IP로 개발합니다.

# 목차
1. 프로젝트 정의와 현재 확정안
2. 핵심 게임 철학과 플레이 루프
3. 스킬 성장 및 직업명 시스템
4. 전투·이동·캐릭터 조작
5. 장비·제작·경제 시스템
6. 월드·콘텐츠 구조
7. 온라인 서버·DB 아키텍처
8. 저폴리 3D 그래픽 규격
9. 캐릭터·장비 제작 전략
10. 몬스터 제작 및 AI 전략
11. 무료 리소스 사용 계획
12. Unity 프로젝트/리소스 관리 규칙
13. MVP 범위와 개발 일정
14. 테스트·운영·배포 계획
15. 리스크와 해결책
16. 보류/후순위 기능
17. 바로 시작할 작업 순서
18. UO 기반 핵심 시스템 보완 설계
부록 A. MVP 스킬 목록
부록 B. 리소스 출처·라이선스 레지스터
부록 C. UO 참고 포인트와 설계 원칙
최종 요약
부록 D. 주요 결정 변경 이력

# 1. 프로젝트 정의와 현재 확정안
| 항목 | 현재 기준 | 판단/이유 |
| --- | --- | --- |
| 장르 | 지속형 온라인 샌드박스 RPG | 콘텐츠 양보다 시스템 상호작용을 중심으로 개인개발 범위를 통제 |
| 그래픽 | 저폴리 3D | 2D의 방향별/프레임별 장비 제작량을 줄이고 공용 Rig·애니메이션을 활용 |
| 카메라 | 고정 3/4 쿼터뷰 + 줌 | 울온식 공간감을 유지하면서 자유 카메라 관련 제작·QA 부담을 차단 |
| 성장 | 레벨보다 스킬 숙련도 중심 | 무엇을 했는지가 캐릭터의 정체성이 됨 |
| 직업 | 고정 직업 선택 없음 | 최고 스킬/스킬 조합/현재 장비가 직업명과 외형을 자연스럽게 결정 |
| 경제 | 플레이어 제작 중심 | 채집·전투·제작·상인이 서로 필요하도록 설계 |
| 서버 | 서버 권한형 Dedicated Server | 스킬, 골드, 드랍, 거래 결과를 클라이언트가 결정하지 못하게 함 |
| 초기 동접 목표 | 20~50명 | 한 서버 프로세스로 충분히 검증 가능한 개인개발 규모 |
| 개발 목표 | 9개월 MVP + 10~14개월 UO Identity Phase | 무료 저폴리 에셋으로 MVP를 검증한 뒤 UO 핵심 사회 시스템을 단계 확장 |

| 핵심 원칙  보류된 세부 규칙은 특별한 이유가 없으면 UO식 철학을 우선합니다. 단, 2026년 사용자 경험과 개인개발 난이도 때문에 조작·UI·성능 관련 부분은 현대적으로 단순화합니다. |
| --- |

- 처음부터 대규모 MMO를 만들지 않습니다. “작은 하나의 살아 있는 월드”를 먼저 완성합니다.
- 하우징, 대규모 PvP, 공성, 선박, 복잡한 범죄 시스템은 핵심 루프 검증 후 추가합니다.
- 그래픽 품질 경쟁보다 스킬·경제·제작·거래의 연결성을 우선합니다.
- 무료 에셋은 최종 자산으로 고정하지 않고, 동일 규격의 자체/유료 자산으로 교체 가능한 데이터 구조를 유지합니다.
# 2. 핵심 게임 철학과 플레이 루프
## 2.1 플레이어가 “직업을 선택”하지 않는다
- 캐릭터 생성 시 전사·마법사·대장장이를 선택하지 않습니다.
- 검을 사용하면 검술이, 광석을 캐면 채광이, 장비를 만들면 대장장이 스킬이 성장합니다.
- 현재 높은 스킬과 장비 실루엣이 캐릭터의 역할을 보여줍니다.
- 캐릭터의 정체성은 “레벨 80”보다 “그랜드마스터 대장장이”, “검술 중심 전사”, “조련사”처럼 표현됩니다.
## 2.2 핵심 순환
| 행동 → 스킬 성장 → 더 어려운 행동/지역 접근 → 자원·아이템 획득     → 제작/가공 → 유저 거래 → 장비 소모·수리·교체 → 다시 행동 |
| --- |

## 2.3 경제 순환
| 사냥꾼 → 희귀 몬스터 재료 ┐광부   → 광석/희귀 광석     ├→ 제작자 → 장비/도구 → 전투·생활 유저벌목꾼 → 목재              ┘                    ↓                                         내구도 감소/소모                                              ↓                                      수리·재제작·재수요 |
| --- |

| 성공 조건  콘텐츠가 많아 보이는 것이 아니라, 플레이어 한 명의 행동이 다른 플레이어의 필요와 연결되어야 합니다. 이 연결이 게임의 “살아 있는 월드”를 만듭니다. |
| --- |

# 3. 스킬 성장 및 직업명 시스템
## 3.1 기본 규칙
| 규칙 | MVP 기준 |
| --- | --- |
| 개별 스킬 최대 | 100.0 |
| 총 스킬 합계 | 700.0 |
| 상태 | ↑ 상승 / ↓ 감소 허용 / 🔒 고정 |
| 성장 방식 | 관련 행동 사용 시 난이도와 현재 숙련도를 비교해 성장 시도 |
| 성장 속도 | 낮은 숙련 빠름, 높은 숙련 느림 |
| 노가다 억제 | 너무 쉬운 행동 반복은 성장 확률 0 또는 매우 낮게 |
| 총합 700 도달 | ↑ 스킬 상승 시 ↓ 지정 스킬이 같은 양만큼 감소 |
| 저장 위치 | 서버/DB가 최종 값 보유 |

## 3.2 1차 직업명 규칙
- 가장 높은 스킬 하나를 대표 스킬로 사용합니다.
- 숙련도 칭호는 Classic UO의 단계감을 기준으로 합니다. 30 미만은 별도 숙련 칭호 없음, 30 초심자, 40 수습, 50 견습, 60 숙련, 70 전문가, 80 달인, 90 대가, 100 그랜드마스터. 예: 숙련 검사 → 달인 검사 → 대가 검사 → 그랜드마스터 검사.
- 복합 직업명은 시스템이 안정된 뒤 데이터 기반 조건으로 추가합니다. 예: 검술+마법=마검사, 궁술+추적=레인저.
- 직업명은 능력치를 강제로 바꾸는 클래스가 아니라 “현재 플레이 결과를 설명하는 명칭”입니다.
| 대표 스킬 | 기본 직업명 | 확장 조합 예시 |
| --- | --- | --- |
| 검술 | 검사 | 검술+마법 → 마검사 |
| 궁술 | 궁수 | 궁술+추적 → 레인저 |
| 마법 | 마법사 | 마법+검술 → 마검사 |
| 치유 | 치료사 | 치유+마법 → 성직자 계열 |
| 채광 | 광부 | 채광+대장장이 → 광물 장인 |
| 대장장이 | 대장장이 | 대장장이+채광 → 무기 장인 |
| 동물조련 | 조련사 | 조련+동물지식 → 야수조련사 |

## 3.3 서버 판정 예
| Client: “오크에게 검 공격 요청”Server: 거리/상태/쿨타임/장비 검증Server: 명중·데미지 계산Server: Swordsmanship 사용 판정Server: 72.3 → 72.4 성장 여부 결정DB: 캐릭터 스킬 저장Client: 72.4와 직업명 UI 표시 |
| --- |

# 4. 전투·이동·캐릭터 조작
## 4.1 전투는 물리 충돌이 아니라 RPG 판정
- 검 모델이 실제 Collider로 적을 베었는지 계산하지 않습니다.
- 대상, 거리, 방향, 공격속도, 스킬, 장비, 상태 이상을 서버가 계산합니다.
- 3D 애니메이션과 VFX는 결과를 보여주는 표현 계층입니다.
- 이 방식은 네트워크 지연에 강하고 치트 방지와 밸런싱이 쉽습니다.
## 4.2 조작 기준
| 요소 | 1차 기준 |
| --- | --- |
| 이동 | UO 정신을 따라 마우스 클릭/홀드 이동을 기본 후보로 두되, PC 접근성을 위해 WASD 병행 가능 구조 |
| 대상 선택 | 클릭으로 NPC/몬스터/플레이어 선택 |
| 기본 공격 | 대상+사거리 충족 시 공격 명령, 자동 반복 여부는 테스트 후 결정 |
| 스킬/마법 | 단축키 또는 퀵바 |
| 상호작용 | 문, 상자, 채집물, NPC는 클릭/상호작용 키 |
| 카메라 | 고정 3/4 쿼터뷰, 줌 가능, 회전은 없음 또는 90도 단위만 후보 |

| 보류 처리 규칙  자동 공격, WASD 병행, 카메라 90도 회전은 플레이 테스트에서 결정합니다. 그 외 타깃 기반 전투, 서버 판정, 3/4 쿼터뷰는 확정입니다. |
| --- |

# 5. 장비·제작·경제 시스템
## 5.1 UO에서 가져올 핵심
- 제작품과 드랍품이 공존합니다. “모든 장비가 제작품”으로 고정하지 않습니다.
- 제작 숙련도에 따라 Normal/Fine/Exceptional/Masterwork 등의 품질 차이를 둘 수 있습니다.
- 고급 제작품에는 제작자 이름(Maker Mark)을 남겨 서버 내 장인 브랜드가 생기도록 합니다.
- 장비에는 내구도와 수리를 두어 생산 스킬에 지속적인 수요가 생기게 합니다.
- 보스는 완성된 최종 장비만 주기보다 희귀 재료를 주고, 제작자가 상위 장비로 완성하는 비중을 높입니다.
## 5.2 이 프로젝트의 권장 공급 비율
| 공급원 | 목표 비중 | 역할 |
| --- | --- | --- |
| 일반 플레이어 제작 | 45% | 일상적으로 쓰는 검·활·방패·갑옷·의류·도구 |
| 고급/특수 제작 | 25% | 고숙련 + 희귀 재료 + 레시피/특수 도구 |
| 몬스터 랜덤 장비 | 20% | 사냥 즉시 보상과 파밍 재미 |
| 보스/아티팩트 | 10% | 고유 외형/특수 효과. 희소성 유지 |

| 중요  위 70% 제작 연계 비율은 실제 UO의 역사적 통계가 아니라, UO의 제작 경제 재미를 이 프로젝트에서 강화하기 위한 설계 목표입니다. |
| --- |

## 5.3 장비 데이터 예시
| Dragon Slayer제작자: 백수장인품질: Exceptional기본 재료: 별철희귀 재료: 드래곤 비늘 12 / 화염의 심장 1옵션: 용족 피해 +32%, 화염 저항 +11%내구도: 87 / 100 |
| --- |

# 6. 월드·콘텐츠 구조
## 6.1 MVP 월드 규모
| 지역 | 개수 | 주요 기능 |
| --- | --- | --- |
| 중앙 마을 | 1 | 은행, 기본 상점, 대장간, 제작 스테이션, NPC, 거래 중심 |
| 초원/농경지 | 1 | 초급 몬스터, 동물, 채집 |
| 숲 | 1 | 벌목, 동물, 고블린 계열 |
| 광산/산지 | 1 | 채광, 박쥐/골렘/스켈레톤 |
| 던전 | 1~2 | 고급 몬스터, 희귀 재료, 보스 |
| 테스트 공간 | 1 | 개발자 전용 스킬/몬스터/장비 QA |

## 6.2 NPC 역할
- NPC는 플레이어 경제를 대체하지 않습니다. 기본 도구·초급 소모품·초기 장비 정도를 공급합니다.
- 상위 장비는 플레이어 제작/드랍/희귀 재료에 의존합니다.
- 은행은 캐릭터 인벤토리와 별도로 안전 저장 공간을 제공합니다.
- 퀘스트는 메인 성장축이 아니라 세계 안내, 제작법, 지역 유도 수단으로 사용합니다.
# 7. 온라인 서버·DB 아키텍처
## 7.1 1차 기술 스택
| 계층 | 선택/후보 | 역할 |
| --- | --- | --- |
| Client | Unity 6 | 입력, 렌더링, UI, 보간 |
| Game Server | Unity Dedicated Server | 게임 로직의 최종 판정 |
| Networking | FishNet 우선 후보 | 동기화, RPC, 관심 영역 관리 |
| Database | PostgreSQL | 계정, 캐릭터, 스킬, 아이템, 은행, 월드 영구 저장 |
| Hosting | 초기 로컬 → Linux VPS | 개발은 로컬, 알파 단계에서 외부 서버 |

## 7.2 서버 권한형 규칙
| 클라이언트가 보내는 것: “이동하려 한다 / 공격하려 한다 / 제작하려 한다”서버가 결정하는 것: 위치 / 명중 / 데미지 / 스킬 상승 / 드랍 / 골드 / 제작 결과 / 거래 결과 |
| --- |

## 7.3 관심 영역(Interest Management)
- 플레이어에게 월드 전체의 모든 객체를 보내지 않습니다.
- 주변 일정 거리의 플레이어, 몬스터, 아이템, NPC만 동기화합니다.
- 20~50명 동접 단계에서는 한 월드 서버 + PostgreSQL로 시작하고, 성능이 실제로 부족할 때 지역 서버 분할을 검토합니다.
## 7.4 DB 핵심 테이블
| 테이블 | 핵심 필드 |
| --- | --- |
| accounts | account_id, login/provider, created_at, status |
| characters | character_id, account_id, name, position, hp/stats, appearance |
| character_skills | character_id, skill_id, value, lock_state |
| items | item_instance_id, template_id, quality, durability, maker_id, properties |
| inventories | owner_id, slot, item_instance_id, amount |
| banks | character/account_id, slot, item_instance_id |
| world_objects | object_id, type, position, persistent_state |
| transactions | trade/craft/audit log |

# 8. 저폴리 3D 그래픽 규격
## 8.1 아트 방향
- 마인크래프트처럼 완전 복셀형보다는 Roblox 계열의 단순한 저폴리 캐릭터를 기본으로 합니다.
- 얼굴 디테일보다 큰 머리, 명확한 손/발, 큰 장비 실루엣으로 역할을 읽게 합니다.
- 복잡한 PBR, 실시간 GI, 천 시뮬레이션, 헤어 시뮬레이션은 사용하지 않습니다.
- 조명은 Directional Light + Ambient 중심으로 단순화합니다.
- 고정 3/4 쿼터뷰를 전제로 카메라에서 보이는 실루엣을 우선합니다.
| 대상 | 권장 예산/규칙 |
| --- | --- |
| 플레이어 기본 몸 | 약 1k~5k tris 목표. 에셋에 따라 더 높아도 프로토타입은 허용 |
| 일반 몬스터 | 약 1k~5k tris |
| 보스 | 약 3k~10k tris |
| 무기/방패 | 약 100~1,000 tris |
| 텍스처 | 128~256px 중심, 아틀라스 적극 활용 |
| 재질 | Flat 또는 단순 Lit. 재질 수 최소화 |
| Collider | Capsule/Box 중심. Mesh Collider 최소화 |
| LOD | MVP는 거리 기반 비활성/간단 LOD만, 필요 시 추가 |

| 그래픽 철학  그래픽의 목표는 “정교한 모델”이 아니라 “멀리서도 무엇인지 즉시 읽히는 실루엣”입니다. 장비와 직업 정체성이 눈에 먼저 들어와야 합니다. |
| --- |

## 8.2 월드 비주얼 하한 (오너 지시, 예외 없음)
플레이 화면에 아래가 보이면 그 작업은 실패다. 프로토타입도 같다.
- Unity Primitive를 Default-Material 또는 단색으로 월드 아트에 두지 않는다.
- 마을 바닥은 Unity Terrain + 노이즈 하이트맵. 광장만 평평. ground_grass 타일로 채우지 않는다. 광장 길은 돌길 타일.
- Kenney Nature 기본 grass(시안)/dirt(주황) 무텍스처를 화면에 남기지 않는다.
- Fantasy Town은 colormap을 유지한다.
- 벽 타일 더미를 집으로 치지 않는다. 문·창·지붕이 붙어 멀리서 집으로 읽혀야 한다.
검증: `Ulon/Dress Village` → `AssertVillageVisuals`.

# 9. 캐릭터·장비 제작 전략
## 9.1 모델링을 직접 하지 않는 전제
- 프로토타입은 무료 리깅 캐릭터와 모듈 의상/무기를 조합합니다.
- 사람형 캐릭터는 가능하면 Humanoid Rig를 통일해 애니메이션 리타게팅이 가능하게 합니다.
- 무료 캐릭터 팩이 완전 모듈형이 아닐 경우, 플레이어 캐릭터는 Quaternius Universal Base Characters + Modular Character Outfits 무료 Standard를 우선 후보로 사용합니다.
- KayKit Adventurers는 전투/애니메이션/무기 테스트 및 NPC/초기 플레이어 대체 모델로 사용합니다.
- 최종적으로 특정 팩의 외형에 종속되지 않도록 게임 데이터는 Model ID, Equipment ID, Socket 기준으로 설계합니다.
## 9.2 공용 Rig와 Socket
| PlayerRoot├─ Humanoid Rig├─ HeadSocket     → Helmet / Hair├─ RightHandSocket→ Sword / Axe / Pickaxe / Staff├─ LeftHandSocket → Shield / Torch├─ BackSocket     → Bow / Quiver / Cape└─ Body Parts     → Chest / Pants / Boots / Gloves |
| --- |

## 9.3 공용 애니메이션 세트
| 범주 | MVP 애니메이션 |
| --- | --- |
| 공통 | Idle, Walk, Run, Hit, Die |
| 근접 | Sword/OneHand Attack, TwoHand Attack |
| 원거리 | Bow Aim/Shot |
| 마법 | Cast/Staff |
| 생활 | Mining, Chopping, Fishing, Craft/Work |
| 상호작용 | Pickup/Open/Use 정도 |

## 9.4 장비 제작 규칙
- 검 100종이라도 Sword Attack 애니메이션은 공유하고, 손 Socket의 모델만 교체합니다.
- 투구·무기는 Skinned Mesh가 아니라 가능하면 Bone/Socket 부착으로 단순화합니다.
- 갑옷은 동일 Humanoid Rig를 사용하는 모듈 파츠 또는 전체 상체 교체형을 사용합니다.
- 망토는 Cloth Physics 대신 단순 Bone 2~3개 또는 고정형으로 시작합니다.
# 10. 몬스터 제작 및 AI 전략
## 10.1 체형별 Rig 공유
| Rig 계열 | 예시 몬스터 | 전략 |
| --- | --- | --- |
| Humanoid | 고블린, 오크, 스켈레톤, 도적 | 공용 인간형 애니메이션 재사용 |
| Quadruped | 늑대, 곰, 멧돼지 | 소형/대형 1~2개 Rig로 분리 |
| Small | 슬라임, 거미, 쥐 | 간단 전용 Rig 또는 단순 애니 |
| Large | 오우거, 트롤, 골렘 | 기본 리그 + 크기/장비 변형 |
| Flying | 박쥐, 임프, 드레이크 | 비행 공용 애니 |
| Boss | 기존 리그 확장 | 전용 공격 2~4개만 추가 |

## 10.2 20종 몬스터를 8~10개 원형으로 만들기
- 같은 고블린 원형에서 Warrior/Archer/Shaman/Chief를 장비·색·크기로 파생합니다.
- 같은 Slime 모델을 Green/Blue/Red/Poison으로 재질과 능력만 바꿉니다.
- 보스는 일반 모델을 1.3~1.5배 확대하고 머리장식, 큰 무기, VFX, 전용 기술로 차별화합니다.
## 10.3 AI 템플릿
| Idle/Patrol → 감지 → Chase → Attack → 거리 이탈 → ReturnAI 타입: Melee / Ranged / Caster / Animal / Boss |
| --- |

## 10.4 몬스터가 경제에 주는 것
| 몬스터 | 주요 재료 |
| --- | --- |
| 늑대 | 가죽, 고기 |
| 곰 | 두꺼운 가죽, 고기 |
| 거미 | 거미줄, 독주머니 |
| 고블린/오크 | 저급 장비, 철조각, 부족 재료 |
| 골렘 | 광석, 마력석 |
| 드래곤 | 비늘, 뼈, 심장, 극저확률 아티팩트 |

# 11. 무료 리소스 사용 계획
| 리소스 정책  MVP는 “직접 모델링 0에 가깝게” 진행합니다. CC0 또는 명확한 상업 이용 허용 자산만 프로젝트에 넣고, 원본·라이선스·출처를 함께 보관합니다. 무료 Standard와 유료 Source가 함께 있는 팩은 Standard만 사용합니다. |
| --- |

## 11.1 1차 필수 무료 자산
| 팩 | 용도 | 라이선스 | 무료 범위/주의 | 출처 |
| --- | --- | --- | --- | --- |
| Quaternius Universal Base Characters | 플레이어 공용 베이스/헤어 | CC0 | Standard 무료, Source 유료 | 공식 다운로드 |
| Quaternius Modular Character Outfits - Fantasy | 갑옷/옷 모듈 | CC0 | Standard 무료, 62개 파츠 | 공식 페이지 |
| KayKit Adventurers 1.0 | 임시 플레이어/NPC/무기/75 애니 | CC0 | 4 캐릭터 + 25+ 액세서리 | GitHub |
| KayKit Skeletons 1.0 | 언데드 몬스터 | CC0 | 리깅/애니 포함 | GitHub |
| KayKit Dungeon Remastered | 던전/지하 공간 | CC0 | 모듈 던전/소품 | GitHub |
| Kenney Fantasy Town Kit | 중앙 마을/건물 | CC0 | 3D 160개 파일 | 공식 페이지 |
| Kenney Nature Kit | 필드/숲/바위/식생 | CC0 | 3D 330개 파일 | 공식 페이지 |
| Kenney Retro Fantasy Kit | 추가 중세 소품/대체 모듈 | CC0 | 3D 100개 파일 | 공식 페이지 |

## 11.2 선택 자산
| 팩 | 용도 | 비고 |
| --- | --- | --- |
| Quaternius Fantasy Props MegaKit Standard | 대장간, 포션, 책, 가구, 도구 | CC0. 무료 Standard 범위만 사용 |
| RGS_Dev Free Modular Low Poly Dungeon | 던전 보강 | CC0. KayKit 던전과 스타일이 맞는지 테스트 후 채택 |
| Gobkit Free Minions | 추가 인간형 적 | CC0. 스타일이 맞으면 보조 몬스터로 사용 |
| KayKit Fantasy Weapons Bits FREE | 검/도끼/방패 등 추가 무기 | CC0. 무료 25+ 모델 범위 |
| Kenney UI Pack (RPG Expansion) | 스킬창/인벤토리/상점/거래 UI 프로토타입 | CC0. 최종 UI는 UO식 정보밀도와 자체 테마로 재가공 |
| Kenney Fantasy UI Borders | 창 테두리/패널 장식 | CC0. 과한 장식 없이 선택 사용 |
| Kenney Input Prompts | 키보드/마우스/패드 입력 아이콘 | CC0. 조작 안내용 |
| Kenney RPG Audio | 발소리/무기/기본 RPG 효과음 | CC0. 50개 파일 |
| Kenney Interface Sounds | 버튼/창/선택 UI 사운드 | CC0. 100개 파일 |
| Kenney Impact Sounds | 타격/충돌 보강 | CC0. 130개 파일 |
| Kenney Particle Pack | 마법/힐/타격 VFX 임시 리소스 | CC0. Unity Particle용 텍스처 소스로 활용 |
| Noto Sans KR | 한글 UI 기본 폰트 | SIL OFL 1.1. 폰트 파일은 라이선스와 함께 보관 |

## 11.3 에셋을 섞어도 한 게임처럼 보이게 만드는 규칙
1.  Unity Import Scale을 통일합니다. 목표는 1 Unity Unit = 1 meter.
2.  캐릭터 키 범위를 정합니다. 일반 인간 약 1.7~1.9m, 고블린 약 1.1~1.4m, 오우거 2.4m 이상 등.
3.  공통 색 팔레트를 정해 Material 색을 통일합니다. 채도와 금속 밝기를 한 범위로 제한합니다.
4.  광원은 프로젝트 공통 Lighting Preset을 사용합니다. 개별 에셋의 샘플 조명은 가져오지 않습니다.
5.  원본 Material을 직접 수정하지 않고 Game용 Material/Prefab을 별도 생성합니다.
6.  카메라에서 보이는 실루엣이 우선이며, 디테일 차이는 줄이고 무기·투구 크기를 통일합니다.
## 11.4 라이선스/출처 관리
| Assets/_ThirdParty/<Creator>/<PackName>/├─ RAW/                # 원본. 수정 금지├─ LICENSE.txt         # 원본 라이선스 복사├─ SOURCE_URL.txt      # 출처 URL + 다운로드 날짜└─ README_IMPORT.txt   # 어떤 파일을 실제 사용했는지 기록Assets/Game/Art/...    # 게임용으로 가공된 Prefab/Material/ModelVariant |
| --- |

- CC0라도 출처 기록은 유지합니다. 법적 의무가 아니라 프로젝트 감사/교체/업데이트를 위한 관리 목적입니다.
- 유료 Extra/Source 파일이 섞이지 않도록 무료 Standard 다운로드 파일 이름을 기록합니다.
- 출시 전 Third-Party Asset Register를 한 번 더 검수합니다.
- 원본 에셋 팩 자체를 게임 밖에서 재배포하지 않습니다.
# 12. Unity 프로젝트/리소스 관리 규칙
## 12.1 권장 폴더
| Assets/├─ _ThirdParty/│  ├─ KayKit/│  ├─ Kenney/│  └─ Quaternius/├─ Game/│  ├─ Art/Characters/│  ├─ Art/Monsters/│  ├─ Art/Env/│  ├─ Art/VFX/│  ├─ Audio/│  ├─ Data/│  ├─ Prefabs/│  ├─ Scenes/│  ├─ Scripts/Client/│  ├─ Scripts/Server/│  ├─ Scripts/Shared/│  └─ UI/└─ Tests/ |
| --- |

## 12.2 데이터 기반 원칙
- 아이템/스킬/몬스터/제작법은 코드에 하드코딩하지 않고 ID 기반 데이터(ScriptableObject 또는 서버 데이터 파일)로 관리합니다.
- 외형 데이터와 능력치 데이터를 분리합니다. 모델을 교체해도 Item ID와 밸런스가 유지되어야 합니다.
- 서버와 클라이언트가 공유하는 enum/ID 정의는 Shared 영역에 둡니다.
- Prefab은 원본 외부 에셋을 직접 참조하지 않고 Game용 Variant를 경유합니다.
## 12.3 3D 성능 규칙
| 문제 | 해결 |
| --- | --- |
| 조명 비용 | Directional Light 1개 + 단순 Ambient, 실시간 GI 제외 |
| 충돌 비용 | Capsule/Box Collider 우선 |
| 재질/Draw Call | 공유 텍스처 아틀라스, 재질 수 제한 |
| 네트워크 객체 과다 | 관심 영역 밖 객체 동기화 제외 |
| 캐릭터 애니 부담 | Humanoid 공용 Animator/클립 재사용 |
| 월드 제작량 | 벽/문/지붕/바위/나무 모듈 조립 |
| LOD 작업량 | MVP는 거리 비활성, 실제 병목 확인 후 LOD |

# 13. MVP 범위와 개발 일정
## 13.1 MVP 완료 정의
| MVP 완료  외부 서버에 2명 이상 접속 → 캐릭터 생성/재접속 → 사냥 → 스킬 성장 → 아이템 획득 → 채광/벌목 → 장비 제작 → 플레이어 거래 → 은행 저장까지 한 사이클이 안정적으로 이어지면 MVP 핵심이 완성된 것으로 봅니다. |
| --- |

## 13.2 콘텐츠 상한
| 영역 | MVP 목표 |
| --- | --- |
| 지역 | 마을 1, 필드 3, 광산 1, 던전 1~2 |
| 스킬 | 16개 내외 |
| 몬스터 | 20종 내외 + 보스 2~3 |
| 장비 | 무기군 5~6종, 방어구 기본 세트, 제작/드랍 변형 |
| 제작 | 채광/벌목/대장장이/목공/재봉 중심 |
| 온라인 | 로그인, 이동, 전투, 채팅, 거래, 은행 |
| 운영 | 기본 GM 명령, 서버 로그, 데이터 백업 |

## 13.3 9개월 개발 계획
| 기간 | 핵심 목표 | 완료 기준 |
| --- | --- | --- |
| 0~2주 | 프로젝트 부트스트랩 | Unity 프로젝트, Git, 폴더 규칙, 무료 에셋 임포트, 3/4 카메라, 테스트 씬 |
| 1개월차 | 오프라인 Vertical Slice | 캐릭터 이동, 타깃, 기본 전투, 몬스터 1종, 스킬 3종, 아이템 드랍 |
| 2개월차 | 온라인 뼈대 | Dedicated Server 접속, 2인 이동/전투 동기화, 캐릭터 생성/저장 |
| 3개월차 | 스킬/아이템 서버화 | 16개 스킬 데이터, 700 cap, 인벤토리, 장비, 사망/회복, DB 저장 |
| 4개월차 | 채집/제작 | 채광, 벌목, 대장장이, 제작 품질, 내구도, 수리, Maker Mark |
| 5개월차 | 경제/마을 | NPC 상점, 은행, 유저 거래, 중앙 마을, 기본 경제 밸런스 |
| 6개월차 | 월드/몬스터 | 필드 3, 광산, 던전, 몬스터 15~20, 보스 2, 희귀 재료 |
| 7개월차 | 콘텐츠/UX | UI 정리, 직업명, 퀵바, 채팅, 사운드, VFX, 튜토리얼 최소화 |
| 8개월차 | 안정화/운영툴 | 관심 영역, 성능 테스트, GM 명령, 로그, 백업/복구, 치트 검증 |
| 9개월차 | Closed Alpha 후보 | 외부 VPS 배포, 소수 인원 장시간 테스트, 경제/성장 속도 조정 |

## 13.4 10~14개월 UO Identity Phase / 안정화
- 10~14개월은 네트워크·DB·서버 복구·거래 악용 방지 안정화와 UO 정체성 기능을 함께 검증하는 확장 단계입니다.
- 기능 추가 순서는 Notoriety/Guard → Housing/Vendor → Taming → Travel → Criminal/Murder/Open PvP이며, 앞 단계가 안정되지 않으면 다음 단계로 넘어가지 않습니다.
- 그래픽 교체·팔레트 통일·무료 에셋 티 제거도 이 기간에 병행하되, 시스템 안정성과 경제 밸런스를 우선합니다. 상세 일정은 18.14를 기준으로 합니다.
# 14. 테스트·운영·배포 계획
## 14.1 테스트 단계
| 단계 | 대상 | 중점 |
| --- | --- | --- |
| 로컬 단독 | 개발자 1명 | 게임 루프/데이터 |
| 로컬 다중 클라 | 2~5 클라이언트 | 동기화/거래/동시성 |
| 봇/부하 테스트 | 가상 클라이언트 | 몬스터/네트워크/DB 부하 |
| 친구 Closed Test | 5~20명 | 경제, 성장 속도, UX |
| Closed Alpha | 20~50명 목표 | 서버 안정성, 악용, 장기 저장 |

## 14.2 운영 도구 최소 세트
- 캐릭터 위치 이동/복구, 아이템 지급/회수, 스킬 조회/수정, 몬스터 소환/삭제.
- 거래/제작/드랍 로그 조회.
- 계정/캐릭터 정지 및 복구.
- DB 자동 백업과 서버 재시작 후 월드 상태 복구.
- 에러/예외 로그를 날짜·캐릭터·오브젝트 ID와 함께 남김.
# 15. 리스크와 해결책
| 리스크 | 발생 시 문제 | 대응 |
| --- | --- | --- |
| 스킬 30개부터 시작 | 연관 콘텐츠/밸런스 폭발 | MVP 16개, 데이터 구조만 확장 가능하게 |
| 그래픽 에셋 혼합 | 무료 에셋 조립 느낌 | 공통 팔레트/조명/스케일/Material 재가공 |
| 모델링 경험 부족 | 리소스 제작 지연 | CC0 에셋 + 모듈형 캐릭터 + Socket 방식 |
| 3D 성능 | 저사양/동접에서 프레임 저하 | 고정 카메라, 저폴리, 단순 조명, 거리 비활성 |
| 서버 치트 | 골드/스킬/아이템 조작 | 서버 권한형, 거래/제작 로그 |
| DB 손상/롤백 | 유저 자산 유실 | 자동 백업, 트랜잭션, item instance ID |
| 경제 인플레이션 | 골드/아이템 가치 붕괴 | 수리/내구도/소모품/제작 비용/골드 싱크 |
| 콘텐츠 욕심 | 완성 시점 무한 연기 | MVP 상한 고정, 후순위 기능 별도 목록 |
| 네트워크 복잡도 | 개발 정체 | 한 서버 프로세스부터, 지역 분할은 실제 병목 후 |

# 16. 보류/후순위 기능
| 우선순위 | 기능 | 현재 결정 |
| --- | --- | --- |
| MVP 이후 | 하우징 | UO 방식 참고. 토지/집/컨테이너/보안은 후순위 |
| MVP 이후 | 길드 | 기본 길드/채팅부터, 길드전은 후순위 |
| 후순위 | 자유 PvP/범죄 | UO식 명성/범죄 철학 참고. 경제/전투 안정 후 |
| 후순위 | 도둑질/은신 확장 | 스킬 시스템 검증 후 |
| MVP 이후 | 동물조련 대형 시스템 | MVP에서는 제외. Phase 2에서 조련+동물지식+수의학+Follower Slot을 한 묶음으로 도입 |
| 후순위 | 선박/해양 | 월드 규모와 이동 시스템이 안정된 뒤 |
| 매우 후순위 | 공성/대규모 길드전 | 동접과 서버 구조가 증명된 뒤 |
| 검토 | 카메라 90도 회전 | 3/4 고정 뷰 가독성 테스트 후 |
| 검토 | WASD 병행 | UO식 마우스 이동과 현대 조작 선호 비교 테스트 |

| 기능 추가 기준  새 기능은 “기존 핵심 루프를 더 깊게 만드는가?”를 먼저 봅니다. 단순히 기능 목록을 늘리는 기능은 MVP에 넣지 않습니다. |
| --- |

# 17. 바로 시작할 작업 순서
1.  Quaternius Universal Base Characters Standard와 Modular Character Outfits Fantasy Standard를 무료로 내려받아 플레이어 파츠 교체가 실제로 가능한지 Unity에서 검증합니다.
2.  KayKit Adventurers와 Kenney Fantasy Town/Nature Kit를 받아 3/4 쿼터뷰 테스트 씬을 만듭니다.
3.  공통 Scale, Lighting, Material Palette, 캐릭터 키 규격을 확정합니다.
4.  PlayerRoot + Humanoid Animator + Right/Left/Head/Back Socket 구조를 만듭니다.
5.  Idle/Walk/Run + Sword Attack 한 세트만 연결해 캐릭터 2명이 같은 애니메이션을 공유하도록 합니다.
6.  오프라인에서 몬스터 1종을 공격하고 Swordsmanship 0.0 → 0.1이 오르는 최소 스킬 루프를 구현합니다.
7.  Dedicated Server를 붙여 두 클라이언트가 같은 몬스터를 보고 공격하도록 합니다.
8.  PostgreSQL에 캐릭터/스킬/인벤토리를 저장하고 재접속 후 복원합니다.
9.  채광 1개 + 철검 제작 1개 + 플레이어 거래 1개를 붙여 가장 작은 경제 루프를 완성합니다.
10.  이 Vertical Slice가 재미있고 안정적일 때만 스킬/몬스터/지역을 확대합니다.
| 첫 온라인 마일스톤  두 플레이어가 서버에 접속하고, 몬스터를 공격해 검술이 상승하며, 광석을 캐서 검을 제작·거래하고, 로그아웃 후 다시 접속해도 모든 데이터가 유지되는 상태. |
| --- |

# 18. UO 기반 핵심 시스템 보완 설계
v1.0을 UO 기준으로 다시 검토한 결과, 스킬/제작/서버 뼈대는 충분하지만 UO의 플레이 감각을 만드는 핵심인 능력치, 죽음과 시체 회수, 무게/컨테이너, 명성·범죄, 마법 자원, 자원 리스폰, 플레이어 상점·하우징, 이동 마법, 파티/길드, 조련 구조가 설계 수준에서 부족했습니다. 아래 항목을 새 기준으로 추가합니다.
## 18.1 기준 Ruleset: “Classic UO + 현대적 편의”
| 항목 | 프로젝트 기준 |
| --- | --- |
| 주 기준 시대 | 1999~2002 전후 Classic UO. 700 Skill Cap, 스킬 Up/Down/Lock, STR/DEX/INT, 시체 회수, 플레이어 제작 경제를 중심으로 함 |
| 선택적으로 가져올 후대 요소 | BOD와 유사한 제작 의뢰, 제작/드랍 연계 가공, Vendor Search와 같은 편의 기능은 필요할 때만 도입 |
| MVP에서 제외할 현대 UO 요소 | 아이템 보험, 고강도 랜덤 옵션 파밍, 120 스킬/파워 스크롤형 상한 확장, 지나친 아티팩트 중심 성장 |
| 월드 구조 | Trammel/Felucca를 두 개 복제하지 않고 하나의 월드로 시작. 마을 Guard Zone과 야외/위험 지역 규칙을 분리 |
| 핵심 원칙 | UO의 숫자를 복사하는 것이 아니라 “행동→숙련→경제→사회 관계→월드에 흔적”이라는 구조를 재현 |
## 18.2 STR / DEX / INT 능력치 시스템
현재 문서에서 가장 크게 빠진 부분입니다. UO는 레벨 대신 스킬뿐 아니라 Strength, Dexterity, Intelligence가 캐릭터 빌드를 함께 결정합니다. Classic 감각을 위해 MVP에 포함합니다.
| 능력치 | MVP 역할 | 연결 요소 |
| --- | --- | --- |
| STR | 최대 HP, 근접 피해 보정, 장비 Strength Requirement, 소지 무게 | 검술/전술/채광/대장장이 계열과 자연스럽게 성장 |
| DEX | Stamina, 공격/행동 속도 보정, 붕대 처리 속도 보조 | 궁술/방패/재봉 등 민첩 계열 |
| INT | Mana, 주문/마법 보조 | 마법/지능 평가/명상 계열 |
| 총합 Cap | 225를 Classic 기준 1차 상한으로 사용 | 각 Stat도 ↑/↓/Lock 관리. 개별 기본 상한 100부터 시작 |
| 성장 | 스킬 사용/상승 시 그 스킬의 Primary/Secondary Stat에 Stat Gain 시도 | 레벨업 버튼 없이 행동으로 함께 성장 |
정확한 HP/Mana/Stamina 공식은 UO의 현재 수치를 그대로 복사하지 않고, 밸런스 테스트가 쉬운 단순 공식으로 구현합니다. 중요한 것은 “장비와 스킬만이 아니라 능력치 배분도 템플릿의 일부”가 되는 것입니다.
## 18.3 지원 스킬 조합이 곧 빌드가 되는 구조
| 주 행동 | 핵심 스킬 | 지원 스킬 | 설계 결과 |
| --- | --- | --- | --- |
| 검/근접 | 검술 | 전술 + 해부학 + STR | 명중은 무기 스킬, 피해 효율은 전술/해부학/STR이 나눠 담당 |
| 궁술 | 궁술 | 전술 + 해부학 + DEX | 궁술 하나만 100으로 올린 캐릭터와 완성된 궁수 템플릿의 차이를 만듦 |
| 붕대 치료 | 치유 | 해부학 + DEX | 치유량/해독/후기 부활 조건을 지원 스킬과 연결 |
| 마법 | 마법 | 지능 평가 + 명상 + INT | 주문 성공, 공격 효과, 마나 지속력을 분리 |
| 마법 방어 | 마법 저항 | 장비 저항 + 상황 효과 | 단순 방어력 하나로 마법을 막지 않게 함 |
| 방패 | 방패술 | DEX + Shield Type | 큰 방패는 방어가 좋지만 장비/속도 제약을 줄 수 있음 |
이 변경으로 MVP 스킬은 여전히 16개를 유지하되, 요리와 동물조련을 Phase 2로 내리고 해부학과 지능 평가를 MVP에 올립니다. UO식 전투 템플릿의 맛을 살리면서 범위 폭발을 막습니다.
## 18.4 죽음 → Ghost → Resurrection → Corpse Recovery
UO다운 긴장감을 만드는 핵심 시스템입니다. “죽으면 마을에서 즉시 부활하고 아이템도 그대로” 방식은 사용하지 않습니다.
| 단계 | 프로젝트 규칙 |
| --- | --- |
| 사망 | 캐릭터는 Ghost 상태가 되고, 일반 아이템은 World Corpse Container로 이동 |
| Ghost | 공격/채집/제작 불가. 이동과 부활 지점 탐색만 가능. 파티원/일부 NPC와 제한 통신 가능 |
| 부활 | 마을 Healer/성소, 플레이어 고급 Healing, 상위 Magery 등 여러 경로를 단계적으로 제공 |
| 시체 회수 | 부활 후 시체로 돌아가 아이템을 회수. 지도에 최근 시체 위치 표시 |
| 보호 예외 | 퀘스트 핵심 아이템, 계정 귀속/초보 보호 아이템은 Corpse에 남기지 않는 별도 태그 사용 |
| 보험 | MVP에는 UO 현대식 Item Insurance를 넣지 않음 |
| 시체 Decay | 10~20분 configurable. PvE Alpha에서는 소유자/파티 우선권 시간을 두어 실수성 도난을 완화 |
PvP Full Loot는 범죄/명성 시스템이 완성된 뒤 별도 단계에서 활성화합니다. 구조는 처음부터 “Equipped/Backpack/Bank/Corpse/World/House” 위치를 구분할 수 있게 설계합니다.
## 18.5 Backpack, Weight, Container, Ground Item
| 요소 | UO 기반 설계 |
| --- | --- |
| Backpack | 캐릭터의 기본 소지 컨테이너. Stackable과 Item Instance를 구분 |
| Weight | 아이템별 Weight를 두고 STR에서 소지 한도를 파생. 과적 시 이동/행동 제한 |
| Item Count | 성능과 악용 방지를 위해 컨테이너별 아이템 수 상한을 둠 |
| Nested Container | 가방 안의 가방을 지원할 수 있게 parent_container_id 구조를 준비. UI는 MVP에서 1~2단 깊이만 허용 가능 |
| Strength Requirement | 중갑/대형 무기에 장비 요구 STR을 두어 Stat과 장비 선택을 연결 |
| Ground Drop | 월드에 떨어진 일반 아이템은 decay_at을 갖고 일정 시간 후 삭제. 집의 Lockdown 아이템은 예외 |
| Bank | 마을 Banker를 통해 안전 보관. 죽음/Corpse와 분리 |
## 18.6 Magic: Spellbook, Circle, Reagent, Meditation
슬라이스 주문: Ember/Mend/Bolt/Cleanse/Ward/Bind(속박·RootUntil~4s)/Weaken(약화·WeakenUntil~6s·출격 피해×0.5)/Spark(섬광·사거리6·불씨보다 낮은 피해)/Restore(회복·자가/근처 아군·봉합보다 높은 HP·마나/시약 약간 높음)/Blink(도약·자가 전방~3.5m·Ember급·유령/전투 실패)/Bless(축복·자가/근처 아군·BlessUntil~8s·출격 피해×1.25·Ward와 별개). leftover 던전3.
| 요소 | MVP/확장 방향 |
| --- | --- |
| Spellbook | 캐릭터가 배운 주문 목록을 저장. 클래스 선택 없이 Magery Skill로 사용 |
| 주문 단계 | UO 64개를 그대로 복제하지 않고 MVP는 12~16개를 4단계 난이도로 구성. 이후 확장 |
| Reagent | 공격/유틸 주문은 약초·광물·몬스터 재료 등의 Reagent를 소비. 상점/채집/제작 경제와 연결 |
| Mana | INT에서 Mana Pool을 만들고 Meditation이 회복을 담당 |
| Armor Trade-off | 중갑은 active meditation/마나 회복에 패널티. 전사 마법 혼합 빌드에 명확한 선택을 요구 |
| Interruption | 피격 시 일부 주문은 취소될 수 있음. 주문별 Casting Time 존재 |
| Travel Magic | Mark/Recall/Gate와 유사한 위치 기록·이동 시스템은 Phase 2. 범죄/전투 중 사용 제한 |
## 18.7 Fame / Karma / Notoriety / Guard Zone
UO의 PvP를 그대로 넣기 전에, “누가 무고한가 / 범죄자인가 / 살인자인가”를 서버가 판정할 데이터 구조부터 준비합니다.
| 상태 | 의미 | 1차 규칙 |
| --- | --- | --- |
| Innocent (Blue) | 일반 플레이어 | 공격 시 공격자가 Criminal 상태가 될 수 있음 |
| Criminal (Gray) | 최근 절도/무고한 공격 등 범죄 행위 | 일정 시간 공격 자유, Guard Zone에서 위험 |
| Murderer (Red) | 반복적인 플레이어 살인 | 장기 Murder Count 5 이상을 기준 후보로 사용 |
| War/Enemy | 정식 길드전/결투 관계 | 범죄 판정 없이 상호 전투 가능 |
Fame은 몬스터 처치와 고난도 제작 의뢰 등 사회적 성취, Karma는 선/악 행동으로 변화시킵니다. 캐릭터 이름에는 “직업 숙련 칭호”와 별도로 Reputation Title을 표시할 수 있습니다. Guard Zone은 범죄 행위에 강력한 NPC Guard 개입을 제공합니다.
MVP에서는 자유 PK를 끄고 이 필드들만 DB에 준비합니다. Closed Alpha 이후 Guard Zone → 결투/길드전 → 범죄/살인 → 야외 Open PvP 순으로 단계적으로 엽니다.
야외 Open PvP 1 (마을 가드존은 기존).
## 18.8 Resource Vein, Tool Uses, Respawn
| 요소 | 설계 |
| --- | --- |
| 광맥/나무 | 무한 클릭 오브젝트가 아니라 지역 Resource Node/Vein이 보유량을 가짐 |
| Respawn | 고갈 후 일정 시간에 재생. 일부 자원 등급은 respawn 때 다시 추첨 |
| Skill Gate | 높은 등급 자원은 최소 Skill과 성공 확률을 요구 |
| 희귀 재료 | UO의 고유 광물명은 사용하지 않고 독자 세계관의 6~8단계 금속/목재로 설계 |
| Tool Uses | 곡괭이/도끼/재봉 도구/대장장이 도구에 사용 횟수 또는 내구도. 고품질 도구는 더 오래 사용 |
| 지역 경제 | 광산/숲마다 주 자원이 달라 이동과 거래가 생기도록 배치 |
## 18.9 Secure Trade, Player Vendor, Gold Source/Sink
| 시스템 | 설계 |
| --- | --- |
| Secure Trade | 양쪽 Offer + Gold를 창에서 확인하고 둘 다 Accept해야 완료. 한쪽이 내용을 바꾸면 Accept가 자동 해제 |
| Player Vendor | 하우징 도입 후 집/상점에 판매 NPC를 배치. 오프라인 중에도 상품 판매 가능 |
| NPC Vendor | 초급 장비/도구/Reagent/생활품 공급 및 낮은 가격으로 일부 물품 매입. 플레이어 제작 시장을 대체하지 않음 |
| Gold Source | 몬스터 Gold, NPC 매입, 제작 의뢰, 소규모 퀘스트 |
| Gold Sink | NPC Skill Training, 도구/Reagent, 수리 비용, Vendor 계약/수수료, House 비용, 일부 여행 비용 |
| Audit | 거래/판매/제작/아이템 생성·삭제를 Transaction Log에 기록 |
## 18.10 Housing: UO의 “월드에 내 자리가 있다”를 장기 핵심으로
하우징은 MVP 이후지만 단순 장식 기능이 아니라 UO형 경제와 사회 시스템의 중심이므로 설계를 미리 확정합니다.
| 항목 | Phase 2 기준 |
| --- | --- |
| 형태 | Instance가 아니라 실제 월드에 보이는 Persistent House |
| 배치 | 개인개발 난이도를 줄이기 위해 1차는 지정 Housing Zone/Plot 방식. 이후 자유 Placement 검토 |
| 소유 | 계정당 1채 기준으로 시작. Owner / Co-owner / Friend / Guild 권한 |
| Public/Private | 상점 집은 Public, 개인 거주지는 Private 선택 |
| Lockdown | 가구/장식 아이템을 월드에 고정하여 decay 방지 |
| Secure Container | 집 내부 컨테이너에 접근 등급 설정 |
| Player Vendor | Public House의 Vendor Slot에 판매 NPC 배치 |
| 토지 회수 | 장기 미접속 계정의 토지 독점을 막기 위한 inactivity decay/회수 정책 필요 |
## 18.11 Party / Guild / Loot Rights
| 기능 | 계획 |
| --- | --- |
| Party | 리더 초대, Party Chat, HP 표시, Loot Right 공유. MVP 후반 또는 Closed Alpha |
| Loot Right | 보스/정예 Corpse는 기여자/Party에 일정 시간 우선권 후 공개 |
| Guild | Guild Name/Tag, Roster, Rank/Permission, Guild Chat을 1차 범위로 함 |
| Guild War | 범죄 판정 없이 합의된 PvP를 제공하는 첫 PvP 단계 |
| Housing 연계 | House Secure에 Guild 권한을 연결 가능하도록 데이터 구조 준비 |
## 18.12 Animal Taming은 MVP에서 빼고 “묶음 시스템”으로 추가
| 요소 | Phase 2 설계 |
| --- | --- |
| Animal Taming | 생물 포획/조련 성공과 기본 Control |
| Animal Lore | 조련 성공/명령 성공/펫 정보 확인의 지원 스킬 |
| Veterinary | 붕대를 이용한 Pet Heal, 해독, 고숙련 부활 |
| Follower Slots | 강한 펫일수록 더 많은 Control Slot을 사용하여 다수 강펫 동시 운용 방지 |
| Commands | Follow / Stay / Guard / Attack / Come / Release |
| Stable | 마을 Stable Master를 통한 비활성 펫 보관 |
| Pet Death | Bonded Pet은 Ghost가 남고 Veterinary/NPC로 부활 가능한 구조를 후보로 함 |
## 18.13 UO식 UI/상호작용을 3D에 맞게 현대화
| UI | 프로젝트 구현 |
| --- | --- |
| Paperdoll | 캐릭터 장비 슬롯과 외형을 한 화면에서 보여주는 Character Sheet |
| Skill Gump | 모든 스킬 수치 + ↑/↓/Lock + 총합 700 표시 |
| Stat Gump | STR/DEX/INT + ↑/↓/Lock + HP/Stamina/Mana |
| Backpack/Bank | Drag & Drop 중심. 컨테이너를 열어 내부 확인 |
| Target Cursor | 마법/치유/채집/상호작용에서 대상 지정 모드 제공 |
| Context Menu | NPC Train, Pet Command, House Security 같은 복잡한 행동은 우클릭/Context Menu로 현대화 |
| World Labels | 이름/Notoriety/상태를 카메라 가독성에 맞게 최소 표시 |
UO의 음성 명령 문법을 전부 복제하지는 않습니다. 중요한 기능은 UI/Context Menu로 제공하고, “bank”, “vendor”, “guards” 같은 분위기용 Keyword Speech는 선택 기능으로 둡니다.
## 18.14 UO 보완 후 개발 Phase 재정리
| 기간 | 추가/수정 목표 | 범위 보호 |
| --- | --- | --- |
| 0~3개월 | STR/DEX/INT, 700 Skill Cap, 지원 스킬, Skill/Stat Lock, 기본 Spellbook, Corpse/Resurrection 데이터 구조 | PvP/하우징/조련은 금지 |
| 4~6개월 | Weight/Strength Requirement, Resource Vein, Tool Uses, 내구도/수리, Secure Trade, Bank, Reagent 경제 | Player Vendor는 아직 제외 |
| 7~9개월 | 지역/던전/보스, Party 최소 기능, Corpse Recovery UX, 운영툴, 외부 서버 Closed Alpha | Open PvP는 끔 |
| 10개월 | Fame/Karma/Notoriety, Guard Zone, Duel/Guild War |   |
| 11개월 | Housing Zone/Plot, Lockdown/Secure, Player Vendor |   |
| 12개월 | Animal Taming + Lore + Veterinary + Stable/Follower Slot |   |
| 13개월 | Moongate + 위치 기록/Recall 계열 + Runebook 유사 시스템 |   |
| 14개월 | Criminal/Murder/Open PvP 시험, 제작 의뢰(BOD 유사), 장기 경제 밸런스 |   |
따라서 “UO답게 만들기 위해 기능을 전부 MVP에 넣는다”가 아니라, 9개월 MVP의 구조가 10~14개월 UO Identity Phase를 수용하도록 데이터를 먼저 설계합니다.
## 18.15 리소스 사용 계획의 빠진 영역 보완
| 영역 | 무료/오픈 리소스 후보 | 사용 원칙 |
| --- | --- | --- |
| UI | Kenney UI Pack (RPG Expansion), Fantasy UI Borders | 프로토타입만 사용 후 자체 테마로 재가공 |
| 입력 아이콘 | Kenney Input Prompts | 키보드/마우스/패드 안내 |
| 전투/생활 SFX | Kenney RPG Audio, Impact Sounds | CC0, 파일별 용도 태그 정리 |
| UI SFX | Kenney Interface Sounds | CC0, 버튼/거래/인벤토리 |
| VFX | Kenney Particle Pack | 마법/힐/충돌용 임시 파티클 소스 |
| 한글 폰트 | Noto Sans KR | SIL OFL 1.1, 게임 UI 기본 폰트 |
| 음악 | 초기에는 최소화 | 최종 출시 전 독자 제작/의뢰 또는 트랙별 라이선스가 명확한 곡만 사용 |
Third-Party Asset Register에는 모델뿐 아니라 UI, 소리, VFX, 폰트, 음악까지 모두 한 줄씩 등록합니다. CC0도 다운로드 날짜·원본 URL·팩 버전·실제 사용 파일을 남깁니다.
## 18.16 서버/DB에 추가할 UO형 데이터
| 영역 | 추가 필드/테이블 |
| --- | --- |
| Character | str, dex, int, stat_lock_state, fame, karma, criminal_until, murder_counts |
| Skill | primary_stat, secondary_stat, title_threshold, npc_train_cap |
| Item Instance | weight, strength_requirement, current_durability, max_durability, maker_id, blessed/bound flag |
| Item Location | location_type(Equipped/Backpack/Bank/Corpse/World/House/Vendor), parent_id, slot, world_position, decay_at |
| Corpse | corpse_id, owner_character_id, death_time, decay_at, loot_rights |
| Resource Node | resource_type, remaining_amount, tier_roll, respawn_at |
| Spellbook | character_id, learned_spell_id |
| House | owner_account_id, plot_id, public_flag, access lists, storage/lockdown/vendor limits |
| Pet | owner_id, control_slots, loyalty/bond state, stable_state, pet_skill data |
## 18.17 UO에서 의도적으로 다르게 가는 부분
| UO 요소 | 프로젝트 결정 |
| --- | --- |
| 원본 세계관/지역/재료/몬스터 이름 | 사용하지 않음. 시스템 철학만 참고 |
| 2D 아이소메트릭 그래픽 | 저폴리 3D 고정 3/4 카메라로 변경 |
| 초기부터 자유 PK | 개인개발 리스크 때문에 Closed Alpha 이후 단계 활성화 |
| 무제한 복잡한 아이템 옵션 | 제작품과 읽기 쉬운 제한된 옵션 중심 |
| 모든 50개+ 스킬 동시 구현 | MVP 16개. 데이터 구조만 확장 |
| 완전 자유 House Placement | Phase 2는 지정 Housing Zone/Plot부터 시작 |
| 음성 명령 중심 UX | Context Menu/Hotbar/Drag & Drop으로 현대화 |
## 18.18 검토에 사용한 UO 공식 근거
| 근거 | 공식 링크 |
| --- | --- |
| Classic Skill Up/Down/Stop 및 700 Cap | https://uo.com/wiki/ultima-online-wiki/technical/previous-publishes/1999-2/1999-publish-01-23rd-november/ |
| Stats / STR DEX INT / Skill Management | https://uo.com/wiki/ultima-online-wiki/player/stats/skills-stats-and-attributes/ |
| Skill Titles / NPC Training | https://uo.com/wiki/ultima-online-wiki/player/skill-titles-order/ |
| Death / Resurrection | https://uo.com/wiki/ultima-online-wiki/combat/death-and-resurrection-in-the-enhanced-client/ |
| Murder System | https://uo.com/wiki/ultima-online-wiki/player/the-murder-system/ |
| Fame and Karma | https://uo.com/wiki/ultima-online-wiki/player/fame-and-karma/ |
| Basic Item Properties / Weight / Durability | https://uo.com/wiki/ultima-online-wiki/items/basic-item-properties/ |
| Magery / Meditation | https://uo.com/wiki/ultima-online-wiki/skills/magery/ |
| Mining / resource tiers | https://uo.com/wiki/ultima-online-wiki/skills/mining/ |
| Secure Trade Window | https://uo.com/wiki/ultima-online-wiki/technical/the-trade-window/ |
| Player Vendors | https://uo.com/wiki/ultima-online-wiki/gameplay/npc-commercial-transactions/npcs-player-owned/ |
| Housing placement / management | https://uo.com/wiki/ultima-online-wiki/gameplay/houses-placing-a-house/ |
| Travel / Runes / Moongates | https://uo.com/wiki/ultima-online-wiki/beginning-the-adventure/movement-and-travel/ |
| Runebooks | https://uo.com/wiki/ultima-online-wiki/skills/inscription/runebooks/ |
| Party System | https://uo.com/wiki/ultima-online-wiki/player/the-party-system/ |
| Guild Creation | https://uo.com/wiki/ultima-online-wiki/player/guild-creation/ |
| Animal Taming / follower slots | https://uo.com/wiki/ultima-online-wiki/skills/animal-taming/ |
| Veterinary / pet resurrection | https://uo.com/wiki/ultima-online-wiki/skills/veterinary/ |
| Character Creation / 3 Starting Skills / Starting Stats | https://uo.com/getting-started/ |
## 18.19 Character Creation, NPC Training, Town Services
UO의 시작은 “직업 선택”이 아니라 초기 능력치와 몇 개의 시작 스킬을 고르는 방식입니다. 본 프로젝트도 이 철학을 유지하되, 초반 이탈을 줄이기 위해 현대적으로 단순화합니다.
- 캐릭터 생성에서 외형과 함께 STR/DEX/INT 초기 배분, 시작 스킬 3개를 선택합니다. 이것은 클래스 선택이 아니라 초기 숙련 방향 설정입니다.
- 프로젝트 MVP 권장값은 Stat 총합 80, 시작 Skill 총합 100, 개별 시작 Skill 최대 50을 후보로 두고 플레이 테스트에서 조정합니다. 수치는 UO 원본 복제가 아니라 본 프로젝트의 밸런스 값입니다.
- 시작 장비는 선택한 시작 스킬을 보고 자동 지급합니다. 검술은 초급 검, 마법은 초급 Spellbook/Reagent, 채광은 곡괭이처럼 “선택한 행동을 바로 해볼 수 있는 도구”만 제공합니다.
- MVP는 시작 도시 1개로 고정합니다. 월드가 확장되면 여러 시작 도시를 선택할 수 있게 하되, 어느 도시를 골라도 직업이나 성장 경로는 잠기지 않습니다.
- NPC Trainer/Guildmaster는 약 30 전후의 초기 스킬 구간을 Gold로 훈련할 수 있게 합니다. NPC Training으로는 Stat이 오르지 않으며, 이후 성장은 실제 행동으로 올립니다.
- 마을 핵심 NPC 역할은 Banker, Healer, Trainer/Guildmaster, 기본 Vendor, Guard로 구성합니다. Stable Master는 Taming 단계에서 추가합니다.
- NPC는 상위 제작품과 고급 장비 시장을 대체하지 않습니다. 초보 진입, Gold Sink, 기본 서비스 제공이 목적입니다.
- Skill Gain은 서버가 판정하고 동일한 저난도 행동 반복에는 성장 효율을 크게 낮춥니다. 시간제 Daily Cap보다 “현재 숙련도와 행동 난이도”를 우선하여 샌드박스 자유도를 유지합니다.

# 부록 A. MVP 스킬 목록
| 계열 | MVP 스킬 | 핵심 행동 |
| --- | --- | --- |
| 전투 | 검술 | 검/한손 근접 무기 명중·숙련 |
| 전투 | 궁술 | 활/원거리 무기 명중·숙련 |
| 전투 보조 | 전술 | 근접·원거리 피해 효율 보정 |
| 전투 보조 | 방패술 | 방패 막기/방어 판정 |
| 전투 보조 | 해부학 | 근접 피해 및 붕대 치유 보조 |
| 전투 보조 | 치유 | 붕대 치료·해독·후기에는 부활 |
| 마법 | 마법 | 주문 성공/원형 주문 체계 |
| 마법 보조 | 지능 평가 | 공격 마법 위력/효과 보조 |
| 마법 보조 | 명상 | 마나 회복, 중갑 착용 시 효율 저하 |
| 마법 보조 | 마법 저항 | 적대 주문/상태이상 저항 |
| 생활 | 채광 | 광맥 채집·희귀 광물 판정 |
| 생활 | 벌목 | 나무 채집·희귀 목재 판정 |
| 생활 | 대장장이 | 금속 무기/갑옷·수리 |
| 생활 | 목공 | 목재 도구/가구/활 |
| 생활 | 재봉 | 천/가죽 장비 |
| 생활 | 낚시 | 물고기/희귀 어획·향후 해양 콘텐츠 연결 |

## 확장 후보
- 요리
- 동물조련
- 창술
- 둔기술
- 연금술
- 추적
- 동물지식
- 수의학
- 은신
- 잠행
- 도둑질
- 자물쇠따기
- 음악
- 도발/평화 계열
# 부록 B. 리소스 출처·라이선스 레지스터
> 2026-08-31 기준 확인. 출시 시점에 다시 확인하며, 각 다운로드 파일 안의 LICENSE를 최종 기준으로 보관합니다.
| 리소스 | 확인 내용 | 링크 |
| --- | --- | --- |
| Quaternius Universal Base Characters | CC0, Standard 무료. itch.io 다운로드 화면에서 $0 / “No thanks, just take me to the downloads” 선택 가능. | 열기 |
| Quaternius Modular Character Outfits - Fantasy | CC0, Standard 무료. Source는 유료. | 열기 |
| KayKit Adventurers 1.0 | CC0. 4 캐릭터, 75 애니메이션, 25+ 무기/액세서리. | 열기 |
| KayKit Skeletons 1.0 | CC0. 스켈레톤 캐릭터/애니메이션. | 열기 |
| KayKit Dungeon Remastered | CC0. 던전 모듈/소품. | 열기 |
| Kenney Fantasy Town Kit | CC0. 3D 160개 파일. | 열기 |
| Kenney Nature Kit | CC0. 3D 330개 파일. | 열기 |
| Kenney Retro Fantasy Kit | CC0. 3D 100개 파일. | 열기 |
| Quaternius Fantasy Props MegaKit | CC0. Standard 무료 범위 사용. | 열기 |
| KayKit Fantasy Weapons Bits | CC0. 무료 버전 25+ 무기. | 열기 |
| RGS_Dev Free Modular Low Poly Dungeon | CC0. 보조 던전 모듈. | 열기 |

# 부록 C. UO 참고 포인트와 설계 원칙
- 스킬 숙련도와 Skill Title: 직업을 고르는 대신 행동 결과로 정체성이 형성되는 철학을 참고합니다.
- Blacksmithing / Exceptional / Maker’s Mark: 제작자의 숙련도와 이름이 장비 가치에 연결되는 구조를 참고합니다.
- Bulk Order / Runic 계열: 생산 스킬에 반복 목표와 희귀 제작 도구를 제공하는 아이디어를 참고할 수 있습니다.
- Age of Shadows 이후의 아이템 속성 파밍: 제작과 드랍이 함께 존재할 수 있음을 참고하되, 본 프로젝트는 제작자 역할을 더 강하게 유지합니다.
- Imbuing 철학: 드랍 아이템과 제작/가공을 경쟁시키지 않고 연결하는 방향을 참고합니다.
- Persistent World: 캐릭터, 은행, 아이템, 집/월드 상태가 서버 재시작과 로그아웃을 넘어 유지되는 것을 장기 목표로 합니다.
## 공식 UO 참고 링크
• Skill Titles: https://uo.com/wiki/ultima-online-wiki/player/skill-titles-order/
• Blacksmithing: https://uo.com/wiki/ultima-online-wiki/skills/blacksmithing/
• Blacksmith BOD: https://uo.com/wiki/ultima-online-wiki/skills/blacksmithing/blacksmith-bulk-orders/
• Age of Shadows Publish: https://uo.com/wiki/ultima-online-wiki/technical/previous-publishes/2003-2/publish-17-1-age-of-shadows/
• Stygian Abyss / Imbuing: https://uo.com/wiki/ultima-online-wiki/technical/previous-publishes/2009-2/publish-60-8th-september-stygian-abyss/
• Loot Generation: https://uo.com/wiki/ultima-online-wiki/items/loot-generation/
# 최종 요약
- 프로젝트 기준은 “Classic UO의 시스템 철학 + 현대적 3D UX”로 확정합니다. 특히 STR/DEX/INT, 지원 스킬 조합, Corpse Recovery, Weight/Container, Reagent, Notoriety 데이터까지 기본 설계에 포함합니다.
| 현재 최종 방향  “저폴리 3D + 고정 3/4 쿼터뷰 + 공용 Humanoid Rig + 무료 모듈형 에셋 + 서버 권한형 스킬 성장 + 플레이어 제작 경제”를 프로젝트의 중심 축으로 확정합니다. |
| --- |

- 그래픽은 시스템을 가리지 않을 정도로 단순하게 만들고, 장비 실루엣으로 캐릭터의 역할을 표현합니다.
- 개발의 첫 목표는 거대한 월드가 아니라 2명이 접속해 스킬·사냥·채집·제작·거래·저장이 이어지는 하나의 완전한 루프입니다.
- 재미가 검증된 뒤에만 스킬, 몬스터, 지역, 하우징, PvP를 확장합니다.
# 부록 D. 주요 결정 변경 이력
| 단계 | 검토/결정 | 현재 처리 |
| --- | --- | --- |
| 초기 | UO 느낌의 2D 온라인 샌드박스 RPG | 핵심 시스템 철학은 유지 |
| 2D 그래픽 검토 | RimWorld 수준 단순화 → Dave the Diver 계열 픽셀 감성 | 아트 참고 이력으로 보존 |
| LPC 검토 | 공용 베이스 + 헤어/갑옷/무기 파츠 구조 | 파츠 교체 철학은 3D에도 그대로 적용 |
| 시점 검토 | 완전 탑뷰보다 3/4 쿼터뷰 선호 | 현재 확정 |
| 3D 전환 | Minecraft/Roblox 정도의 단순 저폴리 3D | 현재 확정 |
| 3D 단점 대응 | 공용 Rig, Socket 장비, 고정 카메라, 단순 조명/충돌 | 현재 기술 원칙 |
| 모델링 문제 | 직접 모델링 대신 무료 모듈형/CC0 에셋 활용 | 현재 리소스 전략 |
| 리소스 기준 | KayKit/Kenney/Quaternius Standard 중심 | MVP 우선 자산 풀 |
| v1.1 UO 재검토 | 능력치/지원스킬/죽음·시체/무게·컨테이너/마법 자원/명성·범죄/하우징·상점/여행/파티·길드/조련 설계 보완 | Classic UO 기반 핵심 시스템을 설계 수준에서 추가. MVP와 Phase 2를 분리 |
