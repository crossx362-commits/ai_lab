using System;
using System.IO;
using FishNet.Object;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        [MenuItem("Ulon/Run Slice Self-Check")]
        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != scenePath)
                scene = EditorSceneManager.OpenScene(scenePath);
            RunBuild();
            // 이미 만들어진 씬은 빌더 수정만으로 안 고쳐진다 — 멱등 보수 패스로 돌린다(검수 랩 D).
            Debug.Log("[Ulon] 건물 시야 페이드 — 레이어 올린 렌더러 " + VisualSliceBuilder.EnsureBuildingsFadeable() + "개");
            VisualSliceBuilder.EnsureBankBuilding();       // 은행을 들어갈 수 있는 건물로(검수 P1)
            VisualSliceBuilder.EnsureFishingSpotAtWater(); // 지표 스냅 뒤에 물가로(앞에 두면 스냅이 둑 위로 끌어올린다)
            VisualSliceBuilder.EnsureVillageFacilities();  // 시설이 그 기능으로 읽히게(검수 랩 ①)
            VisualSliceBuilder.EnsureServiceNpcs();        // 표시명만 사람이던 자리에 사람을(§18.19, 검수 랩 ②)
            VisualSliceBuilder.EnsureGearDressed();        // 한 사람이 무기 1·방패 1만 든다(랩 ③ 발견)
            VisualSliceBuilder.EnsureMobLooks();           // 도적·자객·기사·약탈자를 든 것·색으로 가른다(검수 승인)
            VisualSliceBuilder.EnsureCompanion();          // 지워진 뒤 아무도 안 세우던 동료를 다시(랩 ③)
            VisualSliceBuilder.EnsureActorAnimators();     // 컨트롤러 없는 액터는 게임에서 T포즈다(랩 ④)
            VisualSliceBuilder.EnsureVillagerLooks();      // 5역할을 든 것·몸 색으로 가른다(검수 랩 ③사람)
            VisualSliceBuilder.EnsureCampfireFire();       // 화덕에 불(검수 반려 — 불 메시가 없어도 파티클로 된다)
            VisualSliceBuilder.EnsureStableYardFence();
            VisualSliceBuilder.EnsureStableBeast();       // 마당에 짐승 한 마리(빈 마당은 마구간이 아니다)    // 마구간 울타리를 닫는다(검수 반려)
            VisualSliceBuilder.EnsureEntranceFramesQualified();  // 옛 씬의 검은 큐브 문틀을 등록 조각으로 다시 세운다(검수 2026-09-07)
            VisualSliceBuilder.EnsureRoomSize();           // 방 반경이 원장과 다르면 헐고 다시 짓는다(검은 허공)
            VisualSliceBuilder.EnsureCapeIsBossOnly();     // 망토는 §10.2 보스 표식이다 — 예외 없음(검수 판정 2026-09-07)
            ActionVfxBuilder.EnsureActionVfx();
            ActionSfxBuilder.EnsureActionSfx();          // 등록 CC0 효과음(§11.2)            // 행동 결과 파티클(§11.2·§18.15)
            VisualSliceBuilder.EnsureRoomFurnishing();     // 넓힌 방을 채운다(§6.1 던전 콘텐츠)
            VisualSliceBuilder.EnsureCapBuried();        // 뚜껑을 지표 아래로 묻고 Terrain 홀을 메운다(반려 B)
            VisualSliceBuilder.EnsureVillagePlaza();       // 광장 바닥을 원장으로(칸·무늬·가로등, 검수 랩 ②)
            VisualSliceBuilder.EnsureWorldPropMaterials(); // 소품이 전부 놓인 뒤에 칠한다(반려 A)
            VisualSliceBuilder.EnsureOutdoorPropMaterials(); // 야외에 남은 던전 텍스처를 마을 톤으로
            VisualSliceBuilder.EnsureWorldAtmosphere();    // 대기는 원장 하나에서 — 씬에 옛 값이 남아 있으면 여기서 수렴한다
            VisualSliceBuilder.EnsureDecorClearOfPeople();  // 사람 몸에 박힌 장식은 장식이 비킨다(검수 판정 2026-09-08)
            // **소품끼리 먼저 푼다** — 뒤에 오는 건물·문 앞 정리가 그 결과를 다시 훑어,
            // 비켜난 소품이 문 앞을 막으면 그 자리에서 다시 밀려난다(순서를 바꾸면 NC가 운다).
            VisualSliceBuilder.ClearPropsFromProps();
            VisualSliceBuilder.ClearPropsFromBuildings();   // 건물에 박힌 소품도 소품이 비킨다 — 위 `Ensure*`가
                                                           // 집을 다시 지으면(부지 집) 커밋된 덤불이 그 안에 남는다(2026-09-09)
            // **드레싱·역할 외형이 다 끝난 뒤** 그림 크기를 충돌체에 맞춘다(랩 A). 앞쪽에 두었더니
            // **여기 있어야 한다** — 위 배치 패스 뒤, 아래 네트워크 배선 앞이다.
            // 분할 첫 판에 이 넉 줄을 `RunBuild`로 올렸다가 **스물일곱 문장을 건너뛰어 먼저 돌았고**,
            // 치유사 샷이 10만 픽셀 달라졌다(발·포즈). 순서가 곧 뜻인 자리는 옮기지 마라.
            // 뒤따르는 역할 외형 패스가 훈련사를 다시 키워 게이트가 1.14배로 빨간불이었다 —
            // 맞추는 자가 여럿이면 **마지막에 서는 자**가 이긴다.
            VisualSliceBuilder.EnsureActorBodyMatchesCapsule();
            VisualSliceBuilder.EnsureActorsOnSurface();
            VisualSliceBuilder.EnsureWeaponsAboveFloor();  // 몸을 세운 **뒤** 무기를 바닥 위로(같은 원인의 다른 얼굴)    // **스케일·드레싱이 다 끝난 뒤** 발을 바닥에 다시 붙인다

            VisualSliceBuilder.EnsureCameraSightFade();

            RunNetWiring(scene);

            var bandit = GameObject.Find("Bandit");
            var banditBody = bandit != null ? bandit.GetComponent<WorldBody>() : null;
            if (banditBody == null || banditBody.MobId != "bandit" || !banditBody.IsEnemy)
                throw new InvalidOperationException("두 번째 몬스터 도적이 사냥 구역에 있어야 합니다.");
            if (banditBody.DisplayName != "도적" || Math.Abs(banditBody.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("도적 카탈로그는 이름=도적, HP=45여야 합니다.");
            if (bandit.GetComponent<NetworkObject>() == null || bandit.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("도적 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (MobCatalog.HostileKindCount != 8 || !MobCatalog.TryGet(MobCatalog.Bandit, out MobDefinition banditDefinition)
                || banditDefinition.DisplayName != "도적" || Math.Abs(banditDefinition.MaxHp - 45f) > 0.0001f
                || Math.Abs(banditDefinition.Height - 1.75f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그는 스켈레톤+도적+야만인+자객+기사+주술사+졸병+해골도적 8종이어야 합니다.");

            var serverBanditGo = new GameObject("selfcheck-server-bandit");
            try
            {
                var serverBanditBody = serverBanditGo.AddComponent<WorldBody>();
                serverBanditBody.MobId = "bandit";
                var serverBandit = serverBanditGo.AddComponent<NetMob>();
                serverBandit.OnStartServer();
                if (serverBanditBody.DisplayName != "도적" || Math.Abs(serverBanditBody.MaxHp - 45f) > 0.0001f || Math.Abs(serverBanditBody.Hp - 45f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 도적 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverBanditGo);
            }

            var raider = GameObject.Find("Raider");
            var raiderBody = raider != null ? raider.GetComponent<WorldBody>() : null;
            if (raiderBody == null || raiderBody.MobId != "raider" || !raiderBody.IsEnemy)
                throw new InvalidOperationException("세 번째 몬스터 야만인이 사냥 구역에 있어야 합니다.");
            if (raiderBody.DisplayName != "야만인" || Math.Abs(raiderBody.MaxHp - 60f) > 0.0001f)
                throw new InvalidOperationException("야만인 카탈로그는 이름=야만인, HP=60이어야 합니다.");
            if (raider.GetComponent<NetworkObject>() == null || raider.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("야만인 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Raider, out MobDefinition raiderDefinition)
                || raiderDefinition.DisplayName != "야만인" || Math.Abs(raiderDefinition.MaxHp - 60f) > 0.0001f
                || Math.Abs(raiderDefinition.Height - 1.85f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 야만인이 있어야 합니다.");

            var serverRaiderGo = new GameObject("selfcheck-server-raider");
            try
            {
                var serverRaiderBody = serverRaiderGo.AddComponent<WorldBody>();
                serverRaiderBody.MobId = "raider";
                var serverRaider = serverRaiderGo.AddComponent<NetMob>();
                serverRaider.OnStartServer();
                if (serverRaiderBody.DisplayName != "야만인" || Math.Abs(serverRaiderBody.MaxHp - 60f) > 0.0001f || Math.Abs(serverRaiderBody.Hp - 60f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 야만인 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverRaiderGo);
            }

            var rogue = Array.Find(scene.GetRootGameObjects(), go => go.name == "Rogue");
            var rogueBody = rogue != null ? rogue.GetComponent<WorldBody>() : null;
            if (rogueBody == null || rogueBody.MobId != "rogue" || !rogueBody.IsEnemy)
                throw new InvalidOperationException("네 번째 몬스터 자객이 사냥 구역에 있어야 합니다.");
            if (rogueBody.DisplayName != "자객" || Math.Abs(rogueBody.MaxHp - 40f) > 0.0001f)
                throw new InvalidOperationException("자객 카탈로그는 이름=자객, HP=40이어야 합니다.");
            if (rogue.GetComponent<NetworkObject>() == null || rogue.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("자객 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Rogue, out MobDefinition rogueDefinition)
                || rogueDefinition.DisplayName != "자객" || Math.Abs(rogueDefinition.MaxHp - 40f) > 0.0001f
                || Math.Abs(rogueDefinition.Height - 1.70f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 자객이 있어야 합니다.");

            var serverRogueGo = new GameObject("selfcheck-server-rogue");
            try
            {
                var serverRogueBody = serverRogueGo.AddComponent<WorldBody>();
                serverRogueBody.MobId = "rogue";
                var serverRogue = serverRogueGo.AddComponent<NetMob>();
                serverRogue.OnStartServer();
                if (serverRogueBody.DisplayName != "자객" || Math.Abs(serverRogueBody.MaxHp - 40f) > 0.0001f || Math.Abs(serverRogueBody.Hp - 40f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 자객 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverRogueGo);
            }

            var knight = GameObject.Find("Knight");
            var knightBody = knight != null ? knight.GetComponent<WorldBody>() : null;
            if (knightBody == null || knightBody.MobId != "knight" || !knightBody.IsEnemy)
                throw new InvalidOperationException("다섯 번째 몬스터 기사가 사냥 구역에 있어야 합니다.");
            if (knightBody.DisplayName != "기사" || Math.Abs(knightBody.MaxHp - 70f) > 0.0001f)
                throw new InvalidOperationException("기사 카탈로그는 이름=기사, HP=70여야 합니다.");
            if (knight.GetComponent<NetworkObject>() == null || knight.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("기사 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Knight, out MobDefinition knightDefinition)
                || knightDefinition.DisplayName != "기사" || Math.Abs(knightDefinition.MaxHp - 70f) > 0.0001f
                || Math.Abs(knightDefinition.Height - 1.80f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 기사가 있어야 합니다.");

            var serverKnightGo = new GameObject("selfcheck-server-knight");
            try
            {
                var serverKnightBody = serverKnightGo.AddComponent<WorldBody>();
                serverKnightBody.MobId = "knight";
                var serverKnight = serverKnightGo.AddComponent<NetMob>();
                serverKnight.OnStartServer();
                if (serverKnightBody.DisplayName != "기사" || Math.Abs(serverKnightBody.MaxHp - 70f) > 0.0001f || Math.Abs(serverKnightBody.Hp - 70f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 기사 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverKnightGo);
            }

            var acolyte = GameObject.Find("Acolyte");
            var acolyteBody = acolyte != null ? acolyte.GetComponent<WorldBody>() : null;
            if (acolyteBody == null || acolyteBody.MobId != "acolyte" || !acolyteBody.IsEnemy)
                throw new InvalidOperationException("여섯 번째 몬스터 주술사가 사냥 구역에 있어야 합니다.");
            if (acolyteBody.DisplayName != "주술사" || Math.Abs(acolyteBody.MaxHp - 50f) > 0.0001f)
                throw new InvalidOperationException("주술사 카탈로그는 이름=주술사, HP=50이어야 합니다.");
            if (acolyte.GetComponent<NetworkObject>() == null || acolyte.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("주술사 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Acolyte, out MobDefinition acolyteDefinition)
                || acolyteDefinition.DisplayName != "주술사" || Math.Abs(acolyteDefinition.MaxHp - 50f) > 0.0001f
                || Math.Abs(acolyteDefinition.Height - 1.65f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 주술사가 있어야 합니다.");

            var serverAcolyteGo = new GameObject("selfcheck-server-acolyte");
            try
            {
                var serverAcolyteBody = serverAcolyteGo.AddComponent<WorldBody>();
                serverAcolyteBody.MobId = "acolyte";
                var serverAcolyte = serverAcolyteGo.AddComponent<NetMob>();
                serverAcolyte.OnStartServer();
                if (serverAcolyteBody.DisplayName != "주술사" || Math.Abs(serverAcolyteBody.MaxHp - 50f) > 0.0001f || Math.Abs(serverAcolyteBody.Hp - 50f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 주술사 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverAcolyteGo);
            }

            var minion = GameObject.Find("Minion");
            var minionBody = minion != null ? minion.GetComponent<WorldBody>() : null;
            if (minionBody == null || minionBody.MobId != "minion" || !minionBody.IsEnemy)
                throw new InvalidOperationException("일곱 번째 몬스터 졸병이 사냥 구역에 있어야 합니다.");
            if (minionBody.DisplayName != "졸병" || Math.Abs(minionBody.MaxHp - 22f) > 0.0001f)
                throw new InvalidOperationException("졸병 카탈로그는 이름=졸병, HP=22여야 합니다.");
            if (minion.GetComponent<NetworkObject>() == null || minion.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("졸병 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Minion, out MobDefinition minionDefinition)
                || minionDefinition.DisplayName != "졸병" || Math.Abs(minionDefinition.MaxHp - 22f) > 0.0001f
                || Math.Abs(minionDefinition.Height - 1.35f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 졸병이 있어야 합니다.");
            AssertHuntSpot("졸병", minion, "Minion");

            var serverMinionGo = new GameObject("selfcheck-server-minion");
            try
            {
                var serverMinionBody = serverMinionGo.AddComponent<WorldBody>();
                serverMinionBody.MobId = "minion";
                var serverMinion = serverMinionGo.AddComponent<NetMob>();
                serverMinion.OnStartServer();
                if (serverMinionBody.DisplayName != "졸병" || Math.Abs(serverMinionBody.MaxHp - 22f) > 0.0001f || Math.Abs(serverMinionBody.Hp - 22f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 졸병 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverMinionGo);
            }

            var skelRogue = GameObject.Find("SkelRogue");
            var skelRogueBody = skelRogue != null ? skelRogue.GetComponent<WorldBody>() : null;
            if (skelRogueBody == null || skelRogueBody.MobId != "skelrogue" || !skelRogueBody.IsEnemy)
                throw new InvalidOperationException("여덟 번째 몬스터 해골도적이 사냥 구역에 있어야 합니다.");
            if (skelRogueBody.DisplayName != "해골도적" || Math.Abs(skelRogueBody.MaxHp - 28f) > 0.0001f)
                throw new InvalidOperationException("해골도적 카탈로그는 이름=해골도적, HP=28이어야 합니다.");
            if (skelRogue.GetComponent<NetworkObject>() == null || skelRogue.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("해골도적 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.SkelRogue, out MobDefinition skelRogueDefinition)
                || skelRogueDefinition.DisplayName != "해골도적" || Math.Abs(skelRogueDefinition.MaxHp - 28f) > 0.0001f
                || Math.Abs(skelRogueDefinition.Height - 1.50f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 해골도적이 있어야 합니다.");
            AssertHuntSpot("해골도적", skelRogue, "SkelRogue");

            var serverSkelRogueGo = new GameObject("selfcheck-server-skelrogue");
            try
            {
                var serverSkelRogueBody = serverSkelRogueGo.AddComponent<WorldBody>();
                serverSkelRogueBody.MobId = "skelrogue";
                var serverSkelRogue = serverSkelRogueGo.AddComponent<NetMob>();
                serverSkelRogue.OnStartServer();
                if (serverSkelRogueBody.DisplayName != "해골도적" || Math.Abs(serverSkelRogueBody.MaxHp - 28f) > 0.0001f || Math.Abs(serverSkelRogueBody.Hp - 28f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 해골도적 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverSkelRogueGo);
            }

            if (!MobCatalog.TryGet(MobCatalog.BoneWarden, out MobDefinition bossDefinition)
                || bossDefinition.DisplayName != "본워든" || Math.Abs(bossDefinition.MaxHp - 120f) > 0.0001f
                || Math.Abs(bossDefinition.Height - 2.15f) > 0.0001f || !MobCatalog.IsBoss(MobCatalog.BoneWarden)
                || MobCatalog.IsBoss(MobCatalog.Skeleton) || MobCatalog.HostileKindCount != 8)
                throw new InvalidOperationException("던전 1 네임드 엘리트 본워든은 사냥 8종과 별도 보스여야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.ShadowCaptain, out MobDefinition boss2Definition)
                || boss2Definition.DisplayName != "섀도우캡틴" || Math.Abs(boss2Definition.MaxHp - 150f) > 0.0001f
                || Math.Abs(boss2Definition.Height - 2.45f) > 0.0001f || !MobCatalog.IsBoss(MobCatalog.ShadowCaptain)
                || MobCatalog.KillDropOf(MobCatalog.ShadowCaptain) != ItemCatalog.CaptainSigil
                || MobCatalog.KillDropOf(MobCatalog.BoneWarden) != ItemCatalog.WardenCrest
                || MobCatalog.KillDropOf(MobCatalog.ShadowCaptain) == MobCatalog.KillDropOf(MobCatalog.BoneWarden)
                || Math.Abs(boss2Definition.MaxHp - bossDefinition.MaxHp) < 0.0001f
                || Math.Abs(boss2Definition.Height - bossDefinition.Height) < 0.0001f
                || MobCatalog.HostileKindCount != 8)
                throw new InvalidOperationException("던전 2 네임드 엘리트 섀도우캡틴은 본워든과 다른 HP/키/드랍의 별도 보스여야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Hexarch, out MobDefinition boss3Definition)
                || boss3Definition.DisplayName != "헥사크" || Math.Abs(boss3Definition.MaxHp - 180f) > 0.0001f
                || Math.Abs(boss3Definition.Height - 2.60f) > 0.0001f || !MobCatalog.IsBoss(MobCatalog.Hexarch)
                || MobCatalog.KillDropOf(MobCatalog.Hexarch) != ItemCatalog.HexSeal
                || MobCatalog.KillDropOf(MobCatalog.Hexarch) == MobCatalog.KillDropOf(MobCatalog.BoneWarden)
                || MobCatalog.KillDropOf(MobCatalog.Hexarch) == MobCatalog.KillDropOf(MobCatalog.ShadowCaptain)
                || Math.Abs(boss3Definition.MaxHp - bossDefinition.MaxHp) < 0.0001f
                || Math.Abs(boss3Definition.MaxHp - boss2Definition.MaxHp) < 0.0001f
                || Math.Abs(boss3Definition.Height - bossDefinition.Height) < 0.0001f
                || Math.Abs(boss3Definition.Height - boss2Definition.Height) < 0.0001f
                || MobCatalog.HostileKindCount != 8)
                throw new InvalidOperationException("필드 네임드 엘리트 헥사크는 본워든/섀도우캡틴과 다른 HP/키/드랍의 별도 보스여야 합니다.");
            // **비켜 세우기는 저장 앞에서.** 아래 두 패스를 저장 뒤에 배선했다가, 자는 초록인데
            // 화면은 그대로인 판을 세 번 냈다 — 씬에 안 남으니 QA 샷은 옛 자리를 찍는다(2026-09-09).
            VisualSliceBuilder.KeepPeopleOffWalls();      // 가게 사람은 벽에서 두 걸음(검수 2026-09-09)
            VisualSliceBuilder.SpreadTreeTones();         // 나무는 한 색이 아니다(검수 랩 ⑤)
            AssertTreeTones();
            // **절벽 바위는 두 번 걷었다 — 다시 넣지 마라.**
            // ①랩 ⑦(간격 6.5m·220개, 「세로줄을 물건으로 끊는다」): 늘어난 줄 위에 창백한 상자만 얹혔다.
            // ②랩 ⑩(간격 15m·35개, 셰이더로 줄이 없어진 뒤 「크기 단서」로 목적을 바꿔 재유도):
            //   **화면에서 바위가 벽보다 1.66배 밝다**(실측 — 바위 (168,159,144) / 바로 옆 벽 (101,96,83)).
            //   맞추려면 알베도를 0.62배로 낮춰야 하는데 **야외 색조 하한 0.30**에 걸린다(0.36 → 0.22).
            //   게다가 차이의 절반은 알베도가 아니라 **평평한 면이 해를 받는 각**이라 톤으로 못 지운다.
            //   결론: 이 킷 바위는 이 암벽 위에서 **어떤 개수·크기로도 「같은 암반」이 안 된다.**
            // 다음에 시도할 것은 바위가 아니라 **암벽 자체의 톤·무늬 주기**다(한 톤·잔모래).
            // **자도 저장 앞에서 잰다.** 저장 뒤에 쟀더니 사람이 원자리로 돌아간 값을 봤다 —
            // QA 샷이 보는 것은 **저장된 씬**이므로, 자는 저장되는 그 상태를 재야 한다.
            AssertPeopleOffWallsNegativeControl();
            AssertPeopleOffWalls();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var skills = new SkillSet();
            if (Math.Abs(skills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("검술은 0.0에서 시작해야 합니다.");

            var miss = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 9f,
                Skills = skills,
                TargetAlive = true
            });
            if (miss.Applied)
                throw new InvalidOperationException("사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(skills.Get(SkillId.Tactics)) > 0.0001f || Math.Abs(skills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("실패한 공격은 스킬을 올리면 안 됩니다.");

            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                NextAttackAt = 0f,
                Skills = skills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(hit.SkillBefore) > 0.0001f || Math.Abs(hit.SkillAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"검술 0.0→0.1이어야 합니다. 실제 {hit.SkillBefore}→{hit.SkillAfter}");
            if (Math.Abs(skills.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet에 0.1이 저장되어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("근접 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("근접 공격 후 해부학 0.0→0.1이어야 합니다.");

            var a = new SkillSet();
            var b = new SkillSet();
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 1f, Skills = a, TargetAlive = true });
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 1f, Skills = b, TargetAlive = true });
            if (Math.Abs(a.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f || Math.Abs(b.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("공격자별 스킬이 독립이어야 합니다.");

            var archSkills = new SkillSet();
            var archMiss = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 9f,
                Range = ItemCatalog.ArcheryRange,
                WeaponSkill = SkillId.Archery,
                Skills = archSkills,
                TargetAlive = true
            });
            if (archMiss.Applied)
                throw new InvalidOperationException("활 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(archSkills.Get(SkillId.Archery)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Tactics)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("실패한 원거리 공격은 스킬을 올리면 안 됩니다.");

            var archHit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Range = ItemCatalog.ArcheryRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Archery,
                Skills = archSkills,
                TargetAlive = true
            });
            if (!archHit.Applied || !archHit.Hit)
                throw new InvalidOperationException("활 사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Archery) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("궁술 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("활 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("활 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("활 공격은 검술을 올리면 안 됩니다.");

            var meleeFar = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Skills = new SkillSet(),
                TargetAlive = true
            });
            if (meleeFar.Applied)
                throw new InvalidOperationException("근접은 활 사거리에서 들어가면 안 됩니다.");

            var dexLow = new StatSet();
            dexLow.ForceSet(30, 10, 25);
            var dexHigh = new StatSet();
            dexHigh.ForceSet(30, 50, 25);
            var archLow = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.ArcheryRange,
                Now = 2f,
                WeaponSkill = SkillId.Archery,
                Skills = new SkillSet(),
                Stats = dexLow,
                TargetAlive = true
            });
            var archHigh = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.ArcheryRange,
                Now = 2f,
                WeaponSkill = SkillId.Archery,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (archHigh.Damage <= archLow.Damage)
                throw new InvalidOperationException("궁술 피해는 DEX 보정이 있어야 합니다.");

            if (!CharacterStore.EnsureRunning())
                throw new InvalidOperationException("persist 서버를 시작하지 못했습니다.");
            var snap = new CharacterSnapshot
            {
                AccountId = "selfcheck",
                CharacterId = "selfcheck",
                Name = "검사",
                X = 1.5f,
                Y = 0f,
                Z = -2f,
                Hp = 41f,
                Skills = new[] { new SkillRecord { Id = (int)SkillId.Swordsmanship, Value = 0.4f, Lock = 0 } },
                Inventory = new[] { new ItemRecord { Slot = 0, TemplateId = "iron_ore", Amount = 2 } }
            };
            CharacterStore.Save(snap);
            var loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || Math.Abs(loaded.Skills[0].Value - 0.4f) > 0.0001f || loaded.Inventory.Length != 1)
                throw new InvalidOperationException("persist 저장/로드 실패");
            snap.Inventory = new[]
            {
                new ItemRecord { Slot = 0, TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 37, MakerId = "crafter-a" }
            };
            CharacterStore.Save(snap);
            loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || loaded.Inventory.Length != 1
                || loaded.Inventory[0].Uses != 37 || loaded.Inventory[0].MakerId != "crafter-a")
                throw new InvalidOperationException("persist 내구/Maker Mark 왕복 실패");

            var gatherSkills = new SkillSet();
            SkillGain.TryRaise(gatherSkills, SkillId.Mining, 10f, out _, out float mineAfter);
            if (Math.Abs(mineAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("채광 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Blacksmithing, 15f, out _, out float smithAfter);
            if (Math.Abs(smithAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("대장장이 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Lumberjacking, 10f, out _, out float woodAfter);
            if (Math.Abs(woodAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("벌목 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Carpentry, 12f, out _, out float carpAfter);
            if (Math.Abs(carpAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("목공 0.0→0.1이어야 합니다.");

            var weak = new StatSet();
            weak.ForceSet(20, 25, 25);
            var strong = new StatSet();
            strong.ForceSet(50, 25, 25);
            var dmgWeak = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = weak, TargetAlive = true });
            var dmgStrong = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = strong, TargetAlive = true });
            if (dmgStrong.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("STR가 근접 피해에 반영되어야 합니다.");
            var tac = new SkillSet();
            tac.ForceSet(SkillId.Tactics, 40f, SkillLock.Up);
            var dmgTac = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 3f, Skills = tac, Stats = weak, TargetAlive = true });
            if (dmgTac.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("전술이 근접 피해에 반영되어야 합니다.");
            var ana = new SkillSet();
            ana.ForceSet(SkillId.Anatomy, 40f, SkillLock.Up);
            var dmgAna = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 3f, Skills = ana, Stats = weak, TargetAlive = true });
            if (dmgAna.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("해부학이 근접 피해에 반영되어야 합니다.");
            if (StatSet.MaxHpOf(30) != 50 || StatSet.MaxHpOf(50) != 70)
                throw new InvalidOperationException("MaxHp=20+STR 이어야 합니다.");
            var gainStats = new StatSet();
            int strBefore = gainStats.Str;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, gainStats);
            if (gainStats.Str != strBefore + 1)
                throw new InvalidOperationException("검술 상승 시 STR가 올라야 합니다.");
            snap.Str = 44;
            snap.Dex = 22;
            snap.Int = 18;
            CharacterStore.Save(snap);
            loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || loaded.Str != 44 || loaded.Dex != 22 || loaded.Int != 18)
                throw new InvalidOperationException("persist STR/DEX/INT 왕복 실패");

            var cap = new SkillSet();
            cap.ForceSet(SkillId.Archery, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Tactics, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Parrying, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Anatomy, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Healing, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Magery, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Mining, 100f, SkillLock.Locked);
            if (Math.Abs(cap.Total - 700f) > 0.01f)
                throw new InvalidOperationException("700 캡 픽스처 실패 total=" + cap.Total);
            if (SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out _))
                throw new InvalidOperationException("↓ 스킬 없이 700 캡을 넘기면 안 됩니다.");
            cap.SetLock(SkillId.Mining, SkillLock.Down);
            if (!SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out float afterCap))
                throw new InvalidOperationException("↓ 채광이 있으면 검술이 올라야 합니다.");
            if (Math.Abs(afterCap - 0.1f) > 0.0001f || Math.Abs(cap.Get(SkillId.Mining) - 99.9f) > 0.0001f)
                throw new InvalidOperationException("700캡에서 ↓ 채광이 0.1 줄어야 합니다.");
            cap.SetLock(SkillId.Swordsmanship, SkillLock.Locked);
            if (SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out _))
                throw new InvalidOperationException("잠긴 스킬은 오르면 안 됩니다.");

            var lockedStr = new StatSet();
            lockedStr.SetLock(StatId.Str, SkillLock.Locked);
            int strWas = lockedStr.Str;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, lockedStr);
            if (lockedStr.Str != strWas)
                throw new InvalidOperationException("잠긴 STR은 스킬로 오르면 안 됩니다.");

            var tacLock = new SkillSet();
            tacLock.SetLock(SkillId.Tactics, SkillLock.Locked);
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 4f, Skills = tacLock, TargetAlive = true });
            if (Math.Abs(tacLock.Get(SkillId.Tactics)) > 0.0001f)
                throw new InvalidOperationException("잠긴 전술은 오르면 안 됩니다.");
            if (Math.Abs(tacLock.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("전술 잠금이 검술 상승을 막으면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Tactics) != StatId.Str)
                throw new InvalidOperationException("전술 Primary는 STR이어야 합니다.");

            var anaLock = new SkillSet();
            anaLock.SetLock(SkillId.Anatomy, SkillLock.Locked);
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 5f, Skills = anaLock, TargetAlive = true });
            if (Math.Abs(anaLock.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("잠긴 해부학은 오르면 안 됩니다.");
            if (Math.Abs(anaLock.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("해부학 잠금이 검술 상승을 막으면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Anatomy) != StatId.Int)
                throw new InvalidOperationException("해부학 Primary는 INT이어야 합니다.");
            var anaStats = new StatSet();
            int intWas = anaStats.Int;
            SkillGain.TryRaise(new SkillSet(), SkillId.Anatomy, 20f, out _, out _, anaStats);
            if (anaStats.Int != intWas + 1)
                throw new InvalidOperationException("해부학 상승 시 INT가 올라야 합니다.");

            snap.Inventory = new[] { new ItemRecord { Slot = 0, TemplateId = "wood", Amount = 3 } };
            snap.Bank = System.Array.Empty<ItemRecord>();
            CharacterStore.Save(snap);
            var bankBody = new GameObject("selfcheck-bank");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = bankBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bankBody.AddComponent<InventoryBag>();
                bag.Add("wood", 3);
                var dep = world.DepositAll(body);
                if (!dep.Applied)
                    throw new InvalidOperationException("은행 맡기기 실패: " + dep.FailReason);
                if (bag.Items.Count != 0)
                    throw new InvalidOperationException("맡긴 뒤 가방이 비어야 합니다.");
                var vault = bankBody.GetComponent<BankVault>();
                if (vault == null || vault.Items.Count != 1 || vault.Items[0].Amount != 3)
                    throw new InvalidOperationException("은행에 wood x3이 있어야 합니다.");
                snap = CharacterBinder.Capture("selfcheck", body, new SkillSet(), new StatSet());
                snap.AccountId = "selfcheck";
                snap.CharacterId = "selfcheck";
                CharacterStore.Save(snap);
                loaded = CharacterStore.Load("selfcheck");
                if (loaded == null || loaded.Bank == null || loaded.Bank.Length != 1 || loaded.Bank[0].Amount != 3)
                    throw new InvalidOperationException("persist 은행 왕복 실패");
                CharacterBinder.Apply(body, loaded, new SkillSet(), new StatSet());
                var wd = world.WithdrawAll(body);
                if (!wd.Applied || bag.Items.Count == 0 || vault.Items.Count != 0)
                    throw new InvalidOperationException("은행 찾기 실패");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bankBody);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }

            if (CharacterCreate.Validate("", 30, 25, 25, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 50f, 30f, 20f }) == null)
                throw new InvalidOperationException("빈 이름은 거절해야 합니다.");
            if (CharacterCreate.Validate("검사", 50, 25, 10, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 50f, 30f, 20f }) == null)
                throw new InvalidOperationException("스탯 총합 80이 아니면 거절해야 합니다.");
            if (CharacterCreate.Validate("검사", 30, 25, 25, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 60f, 20f, 20f }) == null)
                throw new InvalidOperationException("시작 스킬 개별 50 초과는 거절해야 합니다.");
            var created = CharacterCreate.Build("create-check", "검사", 1, 30, 25, 25,
                new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing },
                new[] { 50f, 30f, 20f });
            if (created.Name != "검사" || created.Str != 30 || created.Appearance != 1 || created.Hp != 50)
                throw new InvalidOperationException("생성 스냅샷 기본값 실패");
            if (created.Skills.Length != 3 || Math.Abs(created.Skills[0].Value - 50f) > 0.01f)
                throw new InvalidOperationException("생성 시작 스킬 실패");
            bool hasSword = false, hasOre = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == "iron_sword") hasSword = true;
                if (created.Inventory[i].TemplateId == "iron_ore" && created.Inventory[i].Amount == 2) hasOre = true;
            }
            if (!hasSword || !hasOre)
                throw new InvalidOperationException("시작 장비 실패");
            var archerCreate = CharacterCreate.Build("archer-check", "궁수", 0, 20, 40, 20,
                new[] { SkillId.Archery, SkillId.Lumberjacking, SkillId.Carpentry },
                new[] { 50f, 30f, 20f });
            bool hasBow = false;
            for (int i = 0; i < archerCreate.Inventory.Length; i++)
                if (archerCreate.Inventory[i].TemplateId == ItemCatalog.WoodenBow)
                    hasBow = true;
            if (!hasBow)
                throw new InvalidOperationException("궁술 시작은 나무활을 줘야 합니다.");
            var parryCreate = CharacterCreate.Build("parry-check", "방패", 0, 20, 40, 20,
                new[] { SkillId.Parrying, SkillId.Swordsmanship, SkillId.Mining },
                new[] { 50f, 30f, 20f });
            bool hasShieldStart = false;
            for (int i = 0; i < parryCreate.Inventory.Length; i++)
                if (parryCreate.Inventory[i].TemplateId == ItemCatalog.WoodenShield)
                    hasShieldStart = true;
            if (!hasShieldStart)
                throw new InvalidOperationException("방패술 시작은 나무방패를 줘야 합니다.");
            CharacterStore.Save(created);
            loaded = CharacterStore.Load("create-check");
            if (loaded == null || loaded.Name != "검사" || loaded.Appearance != 1 || loaded.Skills == null || loaded.Skills.Length != 3)
                throw new InvalidOperationException("생성 persist 왕복 실패");

            var mageCreate = CharacterCreate.Build("mage-check", "마법", 0, 20, 20, 40,
                new[] { SkillId.Magery, SkillId.Meditation, SkillId.EvaluateIntelligence },
                new[] { 50f, 30f, 20f });
            if (mageCreate.Spells == null || mageCreate.Spells.Length != 3)
                throw new InvalidOperationException("마법 시작은 주문 3개를 줘야 합니다.");
            bool hasBoltStart = false;
            for (int si = 0; si < mageCreate.Spells.Length; si++)
                if (mageCreate.Spells[si] == (int)SpellId.Bolt)
                    hasBoltStart = true;
            if (!hasBoltStart)
                throw new InvalidOperationException("마법 시작 주문에 벼락이 있어야 합니다.");
            bool hasResin = false;
            for (int i = 0; i < mageCreate.Inventory.Length; i++)
                if (mageCreate.Inventory[i].TemplateId == SpellCast.Reagent && mageCreate.Inventory[i].Amount >= 8)
                    hasResin = true;
            if (!hasResin)
                throw new InvalidOperationException("마법 시작 시약은 resin x8");

            var book = new Spellbook();
            if (SpellCast.ManaCost(SpellId.Ember) != 6)
                throw new InvalidOperationException("불씨 마나 비용");
            var mageBody = new GameObject("selfcheck-mage");
            GameObject mageWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    mageWorldGo = new GameObject("selfcheck-mage-world");
                    world = mageWorldGo.AddComponent<OfflineWorld>();
                }
                var body = mageBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                body.RecalcFromInt(40);
                var bag = mageBody.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);
                world.BookOf(body).Learn(SpellId.Ember);
                world.BookOf(body).Learn(SpellId.Mend);
                var noTgt = world.TryCast(body, SpellId.Ember, null);
                if (noTgt.Applied)
                    throw new InvalidOperationException("대상 없는 불씨는 실패해야 합니다.");
                var dummy = new GameObject("selfcheck-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 30f;
                skel.ResetHp();
                dummy.transform.position = mageBody.transform.position;
                var ember = world.TryCast(body, SpellId.Ember, skel);
                if (!ember.Applied || skel.Hp >= 30f)
                    throw new InvalidOperationException("불씨 피해 실패: " + ember.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("불씨 후 마법이 올라야 합니다.");
                body.SetHp(20f);
                var mend = world.TryCast(body, SpellId.Mend, body);
                if (!mend.Applied || body.Hp <= 20f)
                    throw new InvalidOperationException("봉합 실패: " + mend.FailReason);

                bag.Add("iron_sword", 1);
                var death = world.HandleDeath(body, "mage-check");
                if (!death.Applied || !body.Ghost || bag.Items.Count != 0)
                    throw new InvalidOperationException("사망 시 가방이 시체로 가야 합니다.");
                var corpse = OfflineWorld.FindCorpse("mage-check");
                if (corpse == null || corpse.Items.Count < 1)
                    throw new InvalidOperationException("시체 아이템 없음");
                var healerGo = new GameObject("Healer");
                healerGo.transform.position = body.transform.position;
                var healer = healerGo.AddComponent<HealerStation>();
                var rez = world.TryResurrect(body, healer);
                if (!rez.Applied || body.Ghost)
                    throw new InvalidOperationException("부활 실패: " + rez.FailReason);
                var corpseRends = corpse.GetComponentsInChildren<Renderer>(true);
                for (int ri = 0; ri < corpseRends.Length; ri++)
                {
                    var mat = corpseRends[ri].sharedMaterial;
                    if (mat != null && mat.name.IndexOf("Default-Material", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("시체는 Default-Material 프리미티브면 안 됩니다.");
                }
                var loot = world.TryLootCorpse(body, corpse);
                if (!loot.Applied)
                    throw new InvalidOperationException("시체 회수 실패: " + loot.FailReason);
                var palGo = new GameObject("selfcheck-pal");
                palGo.transform.position = body.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.DisplayName = "동료";
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                // **HUD가 고르는 것과 서버가 받는 것이 같은가** — 초대 대상 선정을 씬 이름에서
                // 거리 기반으로 바꿨으니(2026-09-08), 오프라인에서도 옆에 선 몸을 집어야 한다.
                // 이걸 안 재면 「온라인을 고치다 오프라인을 잃는」 회귀를 못 본다(검수 조건 2).
                var picked = OfflineWorld.NearestInvitee(body, PartyResolve.InviteRange);
                if (picked == null)
                    throw new InvalidOperationException("초대 대상 선정 실패 — 사거리 " +
                        PartyResolve.InviteRange + "m 안에 몸이 있는데 HUD가 쓰는 선정 함수가 아무것도 못 골랐습니다.");
                var invited = world.TryPartyInvite(body, pal);
                if (!invited.Applied || body.Party == null || !body.Party.Contains(pal))
                    throw new InvalidOperationException("파티 초대 실패: " + invited.FailReason);
                var said = world.TryPartySay(body, "hi");
                if (!said.Applied || body.Party.Chat.Count < 1)
                    throw new InvalidOperationException("파티 채팅 실패");
                bag.Add("resin", 1);
                world.HandleDeath(body, "mage-check");
                var partyCorpse = OfflineWorld.FindCorpse("mage-check");
                var palLoot = world.TryLootCorpse(pal, partyCorpse);
                if (!palLoot.Applied)
                    throw new InvalidOperationException("파티 룻 실패: " + palLoot.FailReason);
                var strangerGo = new GameObject("selfcheck-stranger");
                strangerGo.transform.position = body.transform.position;
                var stranger = strangerGo.AddComponent<WorldBody>();
                stranger.ResetHp();
                bag.Add("wood", 1);
                var healer2 = healerGo.GetComponent<HealerStation>();
                world.TryResurrect(body, healer2);
                world.HandleDeath(body, "mage-check");
                var locked = OfflineWorld.FindCorpse("mage-check");
                var denied = world.TryLootCorpse(stranger, locked);
                if (denied.Applied)
                    throw new InvalidOperationException("파티 밖은 룻하면 안 됩니다.");
                world.TryLootCorpse(pal, locked);
                world.TryResurrect(body, healer2);
                world.TryPartyLeave(body);
                UnityEngine.Object.DestroyImmediate(palGo);
                UnityEngine.Object.DestroyImmediate(strangerGo);
                bag.Add("wood", 1);
                world.HandleDeath(body, "mage-check");
                var rotting = OfflineWorld.FindCorpse("mage-check");
                if (rotting == null)
                    throw new InvalidOperationException("두 번째 시체 없음");
                rotting.SpawnedAt = -9999f;
                rotting.DecaySeconds = 1f;
                world.TickCorpses(0f);
                if (OfflineWorld.FindCorpse("mage-check") != null)
                    throw new InvalidOperationException("시체가 소멸해야 합니다.");
                UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(healerGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mageBody);
                if (mageWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(mageWorldGo);
            }

            if (ItemCatalog.CarryCap(30) != 120 || ItemCatalog.WeightOf("iron_ore") != 2f)
                throw new InvalidOperationException("무게 공식 실패");
            var miner = CharacterCreate.Build("tool-check", "광부", 0, 30, 25, 25,
                new[] { SkillId.Mining, SkillId.Lumberjacking, SkillId.Swordsmanship },
                new[] { 40f, 30f, 30f });
            bool pick = false, hat = false;
            for (int i = 0; i < miner.Inventory.Length; i++)
            {
                if (miner.Inventory[i].TemplateId == ItemCatalog.Pickaxe && miner.Inventory[i].Uses == 20) pick = true;
                if (miner.Inventory[i].TemplateId == ItemCatalog.Hatchet && miner.Inventory[i].Uses == 20) hat = true;
            }
            if (!pick || !hat)
                throw new InvalidOperationException("채광/벌목 시작 도구 실패");

            var toolBody = new GameObject("selfcheck-tool");
            GameObject toolWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    toolWorldGo = new GameObject("selfcheck-tool-world");
                    world = toolWorldGo.AddComponent<OfflineWorld>();
                }
                var body = toolBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = toolBody.AddComponent<InventoryBag>();
                var veinGo = new GameObject("IronVein");
                veinGo.transform.position = toolBody.transform.position;
                var vein = veinGo.AddComponent<ResourceNode>();
                vein.ResourceId = "iron_ore";
                vein.GatherSkill = SkillId.Mining;
                vein.Remaining = 5;
                var noTool = world.TryGather(body, vein);
                if (noTool.Applied)
                    throw new InvalidOperationException("곡괭이 없이 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 1 });
                var g1 = world.TryGather(body, vein);
                if (!g1.Applied)
                    throw new InvalidOperationException("곡괭이 채광 실패: " + g1.FailReason);
                var g2 = world.TryGather(body, vein);
                if (g2.Applied)
                    throw new InvalidOperationException("내구 0 곡괭이로 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 1 });
                var forgeGo = new GameObject("Forge");
                forgeGo.transform.position = toolBody.transform.position;
                var forge = forgeGo.AddComponent<CraftStation>();
                var repaired = world.TryCraft(body, forge);
                if (!repaired.Applied)
                    throw new InvalidOperationException("도구 수리 실패: " + repaired.FailReason);
                if (bag.ToolUses(ItemCatalog.Pickaxe) < 10)
                    throw new InvalidOperationException("수리 후 내구가 올라야 합니다.");

                body.CharacterId = "smith-mark";
                bag.Add("iron_ore", 2);
                var forged = world.TryCraft(body, forge);
                if (!forged.Applied)
                    throw new InvalidOperationException("철검 제작 실패: " + forged.FailReason);
                bool markedSword = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    if (bag.Items[i].MakerId != "smith-mark")
                        throw new InvalidOperationException("제작품 Maker Mark가 제작자 id여야 합니다.");
                    if (bag.Items[i].Uses != ItemCatalog.MaxUsesOf(ItemCatalog.IronSword))
                        throw new InvalidOperationException("제작품 내구가 최대여야 합니다.");
                    markedSword = true;
                }
                if (!markedSword)
                    throw new InvalidOperationException("철검 제작 결과가 가방에 있어야 합니다.");
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    var worn = bag.Items[i];
                    worn.Uses = 12;
                    bag.Items[i] = worn;
                    break;
                }
                bag.Add("iron_ore", 1);
                var swordFix = world.TryCraft(body, forge);
                if (!swordFix.Applied)
                    throw new InvalidOperationException("철검 수리 실패: " + swordFix.FailReason);
                bool restored = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    if (bag.Items[i].Uses < 22)
                        throw new InvalidOperationException("수리 후 철검 내구가 올라야 합니다.");
                    if (bag.Items[i].MakerId != "smith-mark")
                        throw new InvalidOperationException("수리는 Maker Mark를 지우면 안 됩니다.");
                    restored = true;
                }
                if (!restored)
                    throw new InvalidOperationException("수리 대상 철검이 없습니다.");

                world.StatsOf(body).ForceSet(10, 25, 25);
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                var weakSkelGo = new GameObject("selfcheck-str");
                var weakSkel = weakSkelGo.AddComponent<WorldBody>();
                weakSkel.IsEnemy = true;
                weakSkel.MaxHp = 30f;
                weakSkel.ResetHp();
                weakSkelGo.transform.position = toolBody.transform.position;
                var blocked = world.TryAttack(body, weakSkel);
                if (blocked.Applied)
                    throw new InvalidOperationException("STR 부족인데 철검 공격이 들어가면 안 됩니다.");
                world.StatsOf(body).ForceSet(30, 25, 25);
                var okAtk = world.TryAttack(body, weakSkel);
                if (!okAtk.Applied)
                    throw new InvalidOperationException("STR 충족 공격 실패: " + okAtk.FailReason);

                bag.Add("iron_ore", 80);
                if (!bag.Overweight(30))
                    throw new InvalidOperationException("과적 판정 실패");
                UnityEngine.Object.DestroyImmediate(veinGo);
                UnityEngine.Object.DestroyImmediate(forgeGo);
                UnityEngine.Object.DestroyImmediate(weakSkelGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(toolBody);
                if (toolWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(toolWorldGo);
            }

            RunCombatCraftRules();
            RunLifeSkillRules();
            Debug.Log("[Ulon] Slice self-check PASS — 몬스터 8종(스켈레톤+도적+야만인+자객+기사+주술사+졸병+해골도적), 검술/채광/제작, 목공, 궁술/나무활, 전술 0.0→0.1, 방패술 0.0→0.1, 해부학 0.0→0.1, 치유 붕대 0.0→0.1, 명상 마나 0.0→0.1, 마법 저항 0.0→0.1, 지능 평가 0.0→0.1, 낚시 0.0→0.1, 요리 0.0→0.1, 창술/나무창 0.0→0.1, 둔기술/나무곤봉 0.0→0.1, 연금술/회복물약 0.0→0.1, 각인 1(TryInscribe 천/blank+불씨 주문→scroll_ember 0.0→0.1, 주문서 1회 불씨 후 소모, 마법/연금술과 별개), 독 1(TryPoisonWeapon 연금 물약/천 독병 근접무기 도포 0.0→0.1, 다음 TryAttack 짧은 HP 틱, 마법/연금술/수의학과 별개), 추적 0.0→0.1, 음악/류트 0.0→0.1, 평화 0.0→0.1, 도발 0.0→0.1, 은신 0.0→0.1, 잠행 0.0→0.1, 자물쇠따기 0.0→0.1, 동물지식 0.0→0.1, 수의학 0.0→0.1, 하우징 지정 부지 1(가드존 밖 claim/lockdown/secure), 플레이어 상점 1(Public House Vendor Slot 가방 1개 골드 구매), 조련 1(야생하트 follow/release, AnimalTaming 0.0→0.1), 펫 명령 Stay/Guard/Attack/Come 1(TryPetAttack 근처 몹 추격·공격, TryPetCome 공격해제·Follow·주인으로 이동, 아바타 Open PvP 없음), 마구간 1(마을 Stable Master TryStable/TryClaimStable, 골드 2), 여행 1(공개 문게이트 광장 워프), Mark/Recall 1(한 슬롯 필드 기록·귀환, 골드 5, 문게이트는 광장), 동쪽 필드 FieldOak 벌목, 남쪽 필드 FieldFlax 재봉 채집, 북쪽 필드 FieldOre 채광, 던전 1 서쪽 입구/지하 방(지면 −" + WorldTerrain.DungeonDepth + "m·암반 뚜껑) 입장·퇴장+내부 스켈레톤 1+본워든(HP " + MobCatalog.MaxHpOf("bonewarden") + ", warden_crest), 던전 2 동쪽 입구/지하 방 입장·퇴장+내부 도적 1+섀도우캡틴(KayKit Rogue HP " + MobCatalog.MaxHpOf("shadowcaptain") + ", captain_sigil), 필드 보스 헥사크(동쪽 필드, KayKit Mage HP " + MobCatalog.MaxHpOf("hexarch") + ", hex_seal, 던전 보스와 별개), 길드 1(창설 골드25·이름1~12·초대/수락·GuildId/GuildName 공유·탈퇴, 파티와 별개, HUD 태그), 길드전 1(TryGuildWarDeclare 필드 합의 PvP·무고 유지·가드존 차단, TryGuildWarPeace, Open PvP와 별개), 결투 1(TryDuelInvite/Accept 필드 합의 PvP·무고 유지·가드존 차단, yield/death/TryDuelEnd, Guild War·Open PvP와 별개), 벼락 1(마법 0.0→0.1, 불씨보다 사거리·피해), Exceptional 1(TryCraft 롤/Force·seed, 플래그+내구/피해, MakerId 별개), 은신 감지 1(TryDetectHidden DEX, 은신 대상 해제 0.0→0.1, 은신/잠행과 별개), 야영 1(TryCamp 화덕 근처 또는 나무 불씨, Camping 0.0→0.1, CampSafeUntil 안전로그아웃, 요리/은신과 별개), 도둑질 1(TrySteal 마을 LockedCrate 팩, 최저가 골드/천 1, Stealing 0.0→0.1, 가드존/목격 실패→Criminal, 자물쇠따기/플레이어가방 아님), 붕대 부활 1(TryResurrectBandage 아바타 Ghost·근접·붕대1, Healing 0.0→0.1, HealerStation TryResurrect 유지), Strength Requirement 1(iron_sword StrReq 25·TryEquip 저STR 실패/고STR 성공, catalog-only, AssertStrengthRequirement), 중갑 명상 패널티 1(iron_plate HeavyArmor·명상 틱 마나 회복 ½, AssertMeditationArmorPenalty), 시전 중단 1(Bolt CastingUntil 풍업·TryAttack Applied 피격 취소·효과 없음·마나 소모 유지, AssertCastInterrupt), 정화 1(SpellId.Cleanse 즉시·자가/근처 아바타 독 틱 해제·마나/시약 Ember급·Magery 0.0→0.1, AssertCleanse), 붕대 해독 1(TryCurePoison 독 틱·생존·붕대1·근접, PoisonTicks 해제, Healing 0.0→0.1, Magery Cleanse/Veterinary/rez 아님, AssertBandageDetox), Bonded Pet+Veterinary 부활 1(조련 시 Bonded, HP0→pet Ghost·슬롯 유지·시체 없음, HUD 수의학 버튼 → TryVet → TryVetResurrect 붕대1·Veterinary 0.0→0.1, AssertPetBondVetRez), Weight/과적 1(CarryCap=STR*4, 가방+아이템>한도 시 TryGather/TryBuy/TryCraft 실패·명확 메시지, AssertOverweight), 수호 1(SpellId.Ward 즉시·자가 WardUntil~8s·TryAttack 피해×0.5·마나/시약 Ember급·Magery 0.0→0.1, AssertWard), 속박 1(SpellId.Bind 즉시·근처 적 몹 RootUntil~4s·추격/이동·반격 불가·마나/시약 Ember급·Magery 0.0→0.1, AssertBind), 약화 1(SpellId.Weaken 즉시·근처 적 몹 WeakenUntil~6s·출격 TryAttack/strike 피해×0.5·마나/시약 Ember급·Magery 0.0→0.1, AssertWeaken), 섬광 1(SpellId.Spark 즉시·근처 적 몹 짧은 사거리·불씨보다 낮은 피해·마나/시약 Ember급·Magery 0.0→0.1, AssertSpark), 회복 1(SpellId.Restore 즉시·자가/근처 아군 아바타 HP 회복·봉합보다 높음·마나/시약 봉합보다 약간 높음·Magery 0.0→0.1, AssertRestore), 도약 1(SpellId.Blink 즉시·자가 전방 3.5m 단거리 텔레포트·마나/시약 Ember급·전투/유령 실패·Magery 0.0→0.1, AssertBlink), 축복 1(SpellId.Bless 즉시·자가/근처 아군 BlessUntil~8s·출격 TryAttack 피해×1.25·마나/시약 Ember급·Magery 0.0→0.1, AssertBless), Nested Container 1(pouch parent_container_id·backpack→pouch→item depth1·TryMoveToPouch/TryTakeFromPouch·무게 합산, AssertNestedBag), Ground Drop 1(월드 GroundItem DecayAt·TickGroundItems 만료 삭제, 집 Lockdown/secure 예외, AssertGroundDecay), Reputation Title 1(Murderer→살인자/Criminal→범죄자/Fame≥100→유명인, HUD 이름 옆 SkillTitles와 별개, AssertReputationTitle), Keyword Speech 1(TrySpeechKeyword bank/은행·guards/경비·vendor/상점, 기존 Banker/GuardStrike/Vendor 배선, AssertKeywordSpeech), Follower Control Slots 1(MaxControlSlots=2·하트/멧돼지 cost1·둘 OK 셋째 no_slot·release/stable 해제, AssertControlSlots), CraftOrder/제작의뢰 1(Forge/Vendor TryAcceptOrder·TryTurnInOrder·직접 제작 iron_sword 1·골드10·한 건, AssertCraftOrder), 던전 3 남서 입구/지하 방 입장·퇴장+내부 야만인 1+강철폭군(HP " + MobCatalog.MaxHpOf("irontyrant") + ", tyrant_core), 던전 입구 단서 1(등불 2개↑·깃발·진입로 타일 3장↑·던전3 이정표, AssertDungeonEntrance), 보스 차별화 1(§10.2 왕관·큰 무기·오라 2개↑ + 왕관 정수리 부착(바닥 ≤ 정수리+0.03m)·지름 ≤ 머리 폭 1.4배 + 무기 그립 끝 손 0.10m 안·팔뚝 정렬 60° 안·무기 중심 머리 밖, 스킨 베이크 정점 판정(BossFit), 1몹 1무기(P1 #7)·그립 y ≤ 목 y, 네거티브 컨트롤 왕관 +0.3m/무기 +1m/무기 90°/무기 2개/그립 +0.5m FAIL, AssertBossTraits), 보스 실루엣 1(§10.2 잡몹 대비 1.3~1.5배 범위·모자 폭 몸 폭 0.7배 이하, 렌더러 바운드 판정, AssertBossSilhouette), 플레이 카메라 시야 1(§4.2 고정 쿼터뷰 유지·천장/벽은 DungeonBlocker 레이어 런타임 페이드, 씬 카메라 값으로 판정, AssertPlayCameraSight), 던전 내부 실내 1(벽 8개↑·천장 차단·바닥·몹 간격 4m↑, AssertDungeonInterior), 던전 지하화 1(§8.2 플레이 카메라 화면 잔디 45%↓, 네거티브 컨트롤 0.60 FAIL), 던전 실내 조명 1(§8.2 주광 차단·등불 점광 3개↑ 지하, AssertDungeonLighting), 도달 가능 1(서버 public Try* 전량이 클라 호출부에서 도달·전이 포함, 착용/해제·주머니 넣기/꺼내기·펫 놓아주기 배선, 네거티브 컨트롤 5건 FAIL, AssertReachableFeatures), 서버 권한 배선 1(§7.2 클라 OfflineWorld.Try* 직접 호출 금지·조련/펫/키워드 Rpc 실재, 소스 정적 스캔, AssertServerAuthorityWiring), 월드 지형 1(§6.1 산지·§8.2 산 기복 18m↑·바다/강/호수 수면 아래 6%↑·풀/바위/모래 3종 도포·콘텐츠 전부 뭍, AssertWorldTerrain), 월드 재질 1(§8.2 마을 밖까지 무텍스처/기본 재질 금지·바위 소품은 암석 재질·던전 뚜껑 타일 전부 지표 아래, 네거티브 컨트롤 Kenney colormap 102개 FAIL·뚜껑 0.35m 솟음 FAIL, AssertWorldMaterials), 지역 배치 1(§6.1 농경지·숲·광산 실물 배치·지역 반경 소품 하한/사분면 4개↑·마을 밖 평지 12m 안 소품 75%↑, 네거티브 컨트롤 36.6% FAIL, AssertWorldRegions), 사냥터 1(§8.1 잡몹 8체 발 지표 오차 0.1m↓·z 산포 3m↑ 무리 배치, 잡몹 드레싱 11체 손 소품 없음·무기 1개↓·의상 켜짐, 네거티브 컨트롤 매몰 -1.20m/일직선 0.0m/술잔 FAIL, AssertHuntGround), 지역 지표·길 1(§6.1·§8.2 밭 갈아엎은 흙·숲 부엽토·광산 자갈 도포 0.5↑·마을에서 세 지역으로 흙길·지역 밖 평지 풀 0.8↑, 알파맵 실측 판정, 네거티브 컨트롤 지역 도포 0.00/길 끊김 0.00 FAIL, AssertRegionSplat), 실내 줌 상한 유도 1(§4.2 방 깊이 5.8m에서 유도한 실내 줌 8.11m ≥ 요구 8.0m, 네거티브 컨트롤 깊이 5.6m→7.76m FAIL), 아이템 수치 외부화 1(12.2 StreamingAssets/Data/items.json 27종이 원장·코드는 폴백, 레코드를 구조체로 고쳐 다시 써서 ItemCatalog 반영 확인·GM 「원장 다시 읽기」 배선 확인, AssertItemDataFile), 몬스터 수치 외부화 1(12.2 Data/mobs.json 14종이 원장·HP/키/STR/저항/피해대/보스/드랍/조련·코드는 폴백, 레코드를 구조체로 고쳐 다시 써서 MobCatalog 반영 확인·조련 이름 이중 원장 제거, AssertMobDataFile), 제작법 외부화 1(12.2 Data/recipes.json 11종이 원장·재료/개수/결과/스킬/난이도/수리가능·코드는 폴백, 레코드를 구조체로 고쳐 다시 써서 CraftRecipes 반영 확인·불량 2건 폐기 후 코드 폴백·GM 재적재 배선, AssertRecipeDataFile), 원장 레코드 검증 1(12.2 아이템 weight>0·buy≥0·uses≥0·strReq≥0 / 몹 hp>0·height>0·name·dmgMax≥dmgMin, 불량 레코드만 폐기·코드 폴백·사유 LogError+LoadError, 네거티브 컨트롤 3건 통과 FAIL, AssertDataRecordSanity), STR/HP, 700캡↓, 은행, 캐릭터 생성, 주문책, 시체/부활, 무게/도구, 리스폰/시약, 상점, 훈련, 운영툴, 명성/가드존, 내구도/수리/Maker Mark, 직업명/숙련 칭호, 야외 Open PvP 1(가드존 밖 아바타, 마을 가드존은 기존)");
        }






    }
}
