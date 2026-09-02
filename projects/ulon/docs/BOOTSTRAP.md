# 바로 시작할 작업 (기획서 17장)

0. **이 폴더 부트스트랩** — Unity 프로젝트, 폴더 규칙, 기획 원장. (완료)
1. 파츠 교체 검증. (Humanoid 공유 본에 메시만 교체. Knight 몸 + Mage 머리 PASS. Quaternius Standard는 itch $0 다운로드라 자동 반입 안 함 — 같은 OutfitSwap에 연결하면 됨)
2. KayKit Adventurers + Kenney Fantasy Town/Nature로 3/4 쿼터뷰 테스트 씬. (완료: Bootstrap.unity)
3. Scale, Lighting, Material Palette, 캐릭터 키 규격 확정. (인간 1.8m, Directional+Ambient)
4. PlayerRoot + Humanoid Animator + Right/Left/Head/Back Socket. (handslot.r/l, head, chest)
5. Idle/Walk/Run + Sword Attack 한 세트를 캐릭터 2명이 공유. (Knight 클립 → Knight+Barbarian+Skeleton)
6. 오프라인에서 몬스터 1종 공격 → Swordsmanship 0.0 → 0.1. (self-check PASS, 적은 스켈레톤)
7. Dedicated Server로 두 클라이언트가 같은 몬스터를 보고 공격. (헤드리스 `-ulon-server` + 클라 2. two_client_check.sh PASS, HP 30→22)
8. PostgreSQL에 캐릭터/스킬/인벤토리 저장, 재접속 복원. (persist HTTP driver=postgres. persist_pg_check.py PASS, Unity CharacterStore 재로드 PASS)
9. 채광 1 + 철검 제작 1 + 플레이어 거래 1. (광맥, 대장간 2광석→철검, 클릭 후 양측 수락 거래창. PlayLoopCheck hunt/mine/craft/trade PASS)
10. 이 Vertical Slice가 안정적일 때만 스킬/몬스터/지역 확대. (목공/궁술/전술/방패술/해부학/치유/명상 1. 마법 저항 1: MagicResistResolve 적대 주문 피해 감소·장비 저항·0.0→0.1/INT, HUD/TryCast. SliceSelfCheck 2026-09-02 21:52 KST batch PASS (마법 저항 포함). 필드/던전/추가 몬스터·지능 평가/낚시 루프는 아직)

에디터: Unity Hub에서 `projects/ulon/unity` 를 Unity 6000.3.14f1로 연다.
메뉴 `Ulon/Create Bootstrap Scene` 으로 바닥+플레이스홀더+쿼터뷰 카메라를 만들 수 있다.
