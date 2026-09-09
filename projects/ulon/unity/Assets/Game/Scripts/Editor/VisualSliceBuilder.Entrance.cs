using System;
using System.Collections.Generic;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// **VisualSliceBuilder 분할**(오너 상시 지시 2026-09-08, 파일 비대화 정리).
// 이 파일이 담는 것: 던전 입구 문틀·은행 건물·마을 시설 부속·모닥불 불꽃·마구간 짐승과 울타리.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// **상호작용 표적의 메시를 끈다 — 자리와 콜라이더는 그대로**(검수 판정 2026-09-08).
        ///
        /// `Dungeon*Entrance`는 「누르면 들어가는 곳」이지 장식이 아니다. 그런데 그 자체 메시가
        /// 0.28×1.0×0.28짜리 무늬 없는 회색 막대라, 하필 **문 정중앙**에 서서 화면에서는
        /// 「통로 한가운데 놓인 잔재 기둥」으로 읽혔다(검수 관찰, §8.2 원시 도형 금지).
        ///
        /// **자리는 못 옮긴다** — 도달·워프 게이트가 이 좌표를 전제한다. 그래서 **렌더러만 끈다**:
        /// 콜라이더·`DungeonGate`·좌표는 살아 있으므로 누르는 것은 그대로 되고, 화면에서 문으로
        /// 읽히는 것은 뒤에 선 `EntrancePortal`이 맡는다.
        /// **다음 사람에게**: 이건 「빠진 메시」가 아니다. 되살리지 마라 — 되살리면 문 한가운데
        /// 회색 막대가 다시 선다. 눌리는지는 셀프체크의 던전 입장·퇴장이 매번 증명한다.
        /// </summary>
        public static void HideGateMesh(string gateObjectName)
        {
            var go = GameObject.Find(gateObjectName);
            if (go == null)
                return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                rends[i].enabled = false;
            Debug.Log("[Ulon] 입구 표적 메시 끔 — " + gateObjectName + " 렌더러 " + rends.Length +
                      "개(자리·콜라이더·상호작용은 그대로, 문은 EntrancePortal이 읽힌다)");
        }

        public static void BuildDungeonEntrance(Transform parent, Vector3 pos, float approachYaw)
        {
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            const string PathTile = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/ground_pathTile.fbx";
            string[] models = { Lantern, Banner, PathTile };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            float rad = approachYaw * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));   // 입구가 바라보는 쪽(=접근로)
            var right = new Vector3(fwd.z, 0f, -fwd.x);

            // 문틀 치수와 **같은 값**을 쓴다 — 등불·배너가 문구멍 안으로 들어오지 않게 하려면
            // 문 반폭과 기둥 반폭을 알아야 한다(`BuildEntranceFrame`의 DoorHalf 1.25m·기둥 지름 2.23m).
            const float DoorHalf = 1.25f;
            const float PillarHalf = 1.12f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pillar = pos + right * (DoorHalf * side);
                // **등불은 문 통로 밖에 선다**(검수 판정 2026-09-08). 예전엔 `flank + fwd*1.3`이라
                // 지나가는 자리에 서 있었고, 입구 샷에서는 검은 문판 한가운데 회색 기둥으로 읽혔다.
                // 물건 자체는 맞으니 옮기기만 한다 — 기둥 바깥쪽으로.
                Decor(parent, Lantern, pillar + right * (PillarHalf * side) + fwd * 0.6f,
                      new Vector3(0f, approachYaw, 0f));
                // **벽걸이 물건은 벽에 붙인다**(검수 판정 2026-09-08). `banner-red`는 장대+브래킷+천이
                // 한 몸인 **벽에 거는** 소품인데, 걸 것 없이 공중(up 1.6m)에 세워 둬서 화면에서는
                // 「문 양옆에 뜬 도끼·망치 4개」로 읽혔다(검수 관찰). 기둥 앞면에 붙여 건다.
                // **배너는 기둥의 「바깥쪽 옆면」에 건다**(실측 2026-09-09).
                // 예전엔 `fwd`(=안쪽) 면에 걸어 배너가 **문구멍을 43~71% 덮고** 있었다. 반대 면(-fwd)으로
                // 옮겨 보니 이번엔 던전 3이 **100%** — 카메라 방위가 입구마다 달라서 **앞뒤 어느 면도
                // 세 입구에 동시에 안전하지 않다.** 문구멍은 두 기둥 **사이**에 있으므로, 기둥에서
                // **문 반대쪽(바깥 옆면)**에 걸면 어느 각도에서도 문과 카메라 사이에 들어오지 않는다.
                // 치우지 않고 자리만 옮긴다 — 배너는 입구 표식이다.
                var bannerGo = Place(Banner, pillar + right * ((PillarHalf + 0.05f) * side) + Vector3.up * 2.0f,
                                     new Vector3(0f, approachYaw + 90f * side, 0f));
                if (bannerGo != null)
                {
                    bannerGo.transform.SetParent(parent, true);
                    bannerGo.transform.localScale = bannerGo.transform.localScale * 1.8f;   // 얇은 판때기로 보이던 것을 키운다
                }
                var lightGo = new GameObject("DungeonEntranceLight");
                lightGo.transform.SetParent(parent, true);
                lightGo.transform.position = OnGround(pillar + right * (PillarHalf * side)) + Vector3.up * 2.2f;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.42f);
                light.intensity = 3.2f;
                light.range = 9f;
                light.shadows = LightShadows.None;
            }

            for (int i = 1; i <= 4; i++)
                Decor(parent, PathTile, pos + fwd * (1.6f * i), new Vector3(0f, approachYaw, 0f));

            BuildEntranceFrame(parent, pos, approachYaw);
        }

        // **입구 둔덕은 실패했다 — 다시 만들지 마라**(화면 랩 2, 2026-09-09).
        // 「평지에 선 문」을 고치려고 문 뒤 반원에 `rock_largeA` 일곱을 문틀 높이(4.09m)에서 유도해
        // 둘렀다. 두 번 다 화면을 잃었다: ①`fwd`가 안쪽이라 첫 판은 바위가 **카메라와 문 사이**에
        // 쌓였고, ②부호를 고친 뒤에도 그 바위는 이 프로젝트에서 **잔디색이고 4m가 넘어** 근접 샷을
        // 통째로 덮었다(`07_d1_entrance`가 초록 덩이). **두 판 모두 셀프체크는 EXIT=0**이었다 —
        // 자는 입구가 가려졌는지 안 본다. 지하감은 소품으로 두르는 것이 아니라 **지형을 파는 쪽**이고,
        // 그건 워프 착지·도달 게이트를 건드리므로 판정을 받고 시작할 일이다.
        /// <summary>입구 문틀 루트·문구멍의 이름 원장 — 빌더와 자격 게이트가 **같은 상수**를 본다.</summary>
        public const string EntranceFrameObject = "DungeonEntranceFrame";
        public const string EntrancePortalObject = "EntrancePortal";

        /// <summary>
        /// 입구 문틀만 따로 세운다 — 등불·깃발·돌길과 분리해 **문틀만 다시 지을 수 있게** 한다
        /// (`EnsureEntranceFramesQualified`). 한 함수 안에 두면 옛 씬을 고칠 때 등불이 겹겹이 쌓인다.
        /// </summary>
        static void BuildEntranceFrame(Transform parent, Vector3 pos, float approachYaw)
        {
            float rad = approachYaw * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            var right = new Vector3(fwd.z, 0f, -fwd.x);

            // 문틀 — **등록 CC0 조각으로만 세운다**(검수 2026-09-07 반려). 옛 문틀은 무텍스처 검은
            // 직육면체 3개(기둥 2 + 상인방)라 §8.2가 막는 「프리미티브 색칠 큐브」 그 자체였다 —
            // 소품 랩에서 같은 이유로 이미 반려된 결함이 **입구에만 남아 있었다**(대낮 야외의 검은 비석 셋).
            // 「아치 한 장은 얇은 판때기」라는 옛 문제는 톤을 올려서가 아니라 **입체 돌기둥이 아치 양옆에
            // 서서 깊이를 만드는 것**으로 푼다(아치 자체는 이 함수 밖의 DungeonGate 오브젝트다).
            // **배치 반려(검수 2026-09-07)**: 조각을 등록 메시로 바꿨는데도 화면은 「돌기둥 넷이 흩어져
            // 선 모습」이었다. 원인 셋을 진단으로 갈랐다 —
            //  ① 「상인방은 기존 아치가 대신한다」던 내 말이 **틀렸다**. 게이트 오브젝트(`Dungeon*Entrance`)는
            //     0.30×1.00×0.10m짜리 손바닥만 한 조각이라 상인방 노릇을 할 수 없다. 상인방을 직접 세운다.
            //  ② 옆벽이 기둥과 **같은 줄에** 서서 「기둥 넷」으로 읽혔다 — 뒤로 물려 벽처럼 겹치게 한다.
            //  ③ 조각이 두 종류라 톤이 갈렸다(베이지 하나 + 회색 셋) — **한 종류로 통일**.
            const string Pillar = "Assets/_ThirdParty/KayKit/Dungeon/RAW/Models/pillar_decorated.obj";
            // 눕히는 조각·옆벽은 무늬 없는 민 기둥으로 — 장식은 **세운 것에만** 둔다.
            const string PlainPillar = "Assets/_ThirdParty/KayKit/Dungeon/RAW/Models/pillar.obj";
            var portalMat = MakeNoiseMat("DungeonPortal", new Color(0.03f, 0.03f, 0.05f), new Color(0.08f, 0.07f, 0.10f));
            var frame = new GameObject(EntranceFrameObject);
            frame.transform.SetParent(parent, true);
            float gy = OnGround(pos).y;
            const float DoorHalf = 1.25f;      // 문구멍 반폭 + 기둥 반폭 — 기둥이 문을 좌우로 낀다
            const float PillarH = 3.2f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pillar = pos + right * (DoorHalf * side);
                RoomPropObject(frame.transform, "EntrancePillar" + (side > 0 ? 1 : 2), Pillar,
                    new Vector3(pillar.x, gy, pillar.z), approachYaw, PillarH, true);
            }
            // 상인방 — 눕혀 잇는다. **장식 기둥이 아니라 민 기둥**(`pillar.obj`)을 쓴다(검수 지적
            // 2026-09-07): 장식 기둥을 눕히니 방패 무늬가 **가로로 누워** 「옆으로 눕힌 기둥」으로 읽혔다.
            // 원인은 자동 배치 로직이 아니라 **어떤 조각을 쓰느냐**다 — 등록 킷에 들보 조각이 없어
            // 같은 계열의 민 조각으로 바꾼다(무늬가 없으면 눕혀도 방향이 안 읽힌다).
            var lintel = RoomPropObject(frame.transform, "EntranceLintel", PlainPillar,
                new Vector3(pos.x, gy, pos.z), approachYaw, DoorHalf * 2f + 0.9f, true);
            if (lintel != null)
            {
                lintel.transform.rotation = Quaternion.Euler(0f, approachYaw, 90f);
                if (BoundsOf(lintel.transform, true, out Bounds lb))
                    lintel.transform.position += new Vector3(pos.x, gy + PillarH + 0.25f, pos.z) - lb.center;
            }
            // 옆벽 — 기둥과 **같은 줄이 아니라 뒤로 물려** 벽처럼 겹치게 한다(기둥보다 낮되 0.5m 안).
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wing = pos + right * (2.3f * side) - fwd * 1.1f;
                // 옆벽도 민 조각이다 — 장식 기둥으로 세우면 「기둥이 네 개」로 읽힌다(검수 지적).
                RoomPropObject(frame.transform, "EntranceWing" + (side > 0 ? 1 : 2), PlainPillar,
                    new Vector3(wing.x, gy, wing.z), approachYaw, 2.8f, true);
            }
            // 문구멍 — **자격 원장의 유일한 예외**다. 이건 물건이 아니라 안쪽의 「어둠」이고, 아래가
            // 실제 방이 아니라 지표라서 뚫어 두면 잔디가 비친다(검수: 「문구멍 자체는 어두워도 된다」).
            // 예외가 샛길이 되지 않게 게이트가 **이 이름 하나만·어두운 색일 때만** 봐준다.
            // **자리를 안쪽으로 물린다**(검수 2026-09-08): 앞면이 기둥과 거의 같은 평면에 있어서
            // 그림자가 안 생겨 「통로」가 아니라 **세워 둔 검은 판**으로 읽혔다. 문설주보다 0.55m
            // 안으로 넣으면 기둥·상인방이 그늘을 드리워 **그림자 진 구멍**이 된다. 조각은 그대로다.
            // **부호 주의**: `fwd`는 진입로 쪽이 아니라 **안쪽**을 가리킨다 — 실측으로 확인했다
            // (옛 `-fwd*0.55`는 판을 기둥보다 **앞으로** 내놓고 있었고, 그래서 통로가 아니라 판으로 읽혔다).
            // 0.9m까지 밀었더니 **나가기 워프 착지 자리**를 판이 차지해 게이트가 빨간불을 냈다 —
            // 물리는 깊이는 「보기 좋은 만큼」이 아니라 착지 자리가 허락하는 만큼이다.
            Vector3 back = pos + fwd * 0.55f;
            RoomSlab(frame.transform, EntrancePortalObject, new Vector3(back.x, gy + 1.25f, back.z),
                new Vector3(1.5f, 2.5f, 0.12f), portalMat, approachYaw);
        }

        /// <summary>
        /// **은행을 들어갈 수 있는 건물로 세운다**(검수 2026-09-07 P1: 은행이 풍차 날개 한 장이었다).
        /// 새 팩을 받지 않고 저장소의 Kenney 마을 조각(벽·문·창·지붕·굴뚝)을 조립한다.
        /// 멱등 — 매번 헐고 다시 짓는다(옛 씬의 풍차 날개도 이 패스가 치운다).
        /// </summary>
        public static void EnsureBankBuilding()
        {
            const string Town = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/";
            string[] models = { Town + "wall-door.fbx", Town + "wall-window-shutters.fbx",
                                Town + "roof.fbx", Town + "chimney.fbx" };
            for (int i = 0; i < models.Length; i++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);

            var bank = GameObject.Find("Banker");
            if (bank == null)
                return;
            // 옛 시각물(풍차 날개)과 지난 조각을 통째로 헐어낸다 — 남겨 두면 날개가 건물 옆에 계속 선다.
            for (int c = bank.transform.childCount - 1; c >= 0; c--)
                UnityEngine.Object.DestroyImmediate(bank.transform.GetChild(c).gameObject);
            var stray = bank.GetComponents<Component>();
            for (int i = 0; i < stray.Length; i++)
                if (stray[i] is Renderer || stray[i] is MeshFilter || stray[i] is Collider)
                    UnityEngine.Object.DestroyImmediate(stray[i]);

            // `OnGround`는 지면 높이를 **더한다**(오프셋용) — 이미 지면 위에 선 오브젝트의 위치를 그대로
            // 넣으면 높이가 두 배가 된다. 처음에 그렇게 써서 은행이 지면 10m 위에 떠 있었다(2026-09-07).
            var at = OnGround(new Vector3(bank.transform.position.x, 0f, bank.transform.position.z));
            bank.transform.position = at;
            // 벽재는 **얇은 판 조각**만 쓴다 — `wall-block`은 이 킷에서 2m 정육면체라(실측) 벽이 아니라
            // 돌덩이로 읽히고, 높이도 2m에 그쳐 쿼터뷰 카메라가 지붕 위로 넘겨다봤다(첫 시도 실패).
            const float Half = 2.4f;        // 한 변 4.8m — 사람이 서고도 남는 안쪽
            const float PieceW = 2.4f;      // 벽 판 높이(=폭) — 2m 벽은 카메라가 넘어다본다
            // 네 벽. 앞면(남쪽) 한 칸은 문, 한 칸은 창 — 문·창이 붙어야 「들어갈 수 있는 집」으로 읽힌다(§8.2).
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                float r = yaw * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
                var along = new Vector3(outward.z, 0f, -outward.x);
                for (int k = -1; k <= 1; k += 2)
                {
                    string fbx = (side == 0 && k < 0) ? Town + "wall-door.fbx" : Town + "wall-window-shutters.fbx";
                    // 두 판을 **살짝 겹친다** — 딱 맞춰 놓으면 벽 한가운데에 이음매(폭 0)가 생겨
                    // 그 선으로 광선과 도약이 그대로 통과했다(도약 게이트가 3.50m로 안 막혀 드러남).
                    var p = at + outward * Half + along * ((PieceW * 0.5f - 0.15f) * k);
                    var piece = RoomPropObject(bank.transform, "BankWall" + side + (k > 0 ? "a" : "b"), fbx,
                        // 이 킷의 벽 판은 **yaw 0에서 X축으로 얇다** — `yaw+180`으로 세웠더니 네 벽이 전부
                        // 90° 돌아가 고리가 아니라 바람개비가 됐다(도약 광선이 그 사이로 통과해 드러남).
                        new Vector3(p.x, at.y, p.z), yaw + 90f, PieceW, true);
                    // 조각 원점이 가운데가 아니라 모서리인 프리팹이 있다 — **바운드 중심으로 다시 맞춘다**.
                    // 안 맞추면 벽 고리가 한쪽으로 밀려 틈이 생기고, 그 틈으로 시선·사람이 샌다(실측).
                    if (piece != null && BoundsOf(piece.transform, true, out Bounds pb))
                        piece.transform.position += new Vector3(p.x - pb.center.x, 0f, p.z - pb.center.z);
                }
            }
            // 지붕·굴뚝 — 지붕이 없으면 위에서 본 화면에서 그냥 벽 네 장이다(§8.2 「문·창·지붕」).
            // 지붕은 벽 위에 **걸친다** — 딱 붙여 놓으면 실측에서 0.8m 틈이 생겨 쿼터뷰 광선이 그 틈으로
            // 새고(페이드 대상 0개), 화면에서도 지붕이 떠 보였다.
            float wallTop = BankWallHeight(bank);
            // **한 장을 늘리지 않고 네 장을 깐다**(검수 2026-09-08 P3).
            //
            // ①처음엔 지붕 한 장을 건물 폭(5.4m)에 맞췄다 — `RoomPropObject`는 **한 배율**로 키우므로
            //   높이도 3.3m로 따라 올라가 벽 2.4m와 합쳐 5.7m, 민가(약 4m) 옆에서 회색 판때기였다.
            // ②그래서 세로만 눌렀더니 높이는 맞았지만 **기울기가 뭉개져** 창고처럼 읽혔다(검수 지적).
            // ③답은 민가가 이미 쓰는 방식이었다 — 민가(`PlaceHouse`)는 지붕을 **모듈 조각으로 타일링**한다.
            //   조각을 원 비율 그대로 두고 여러 장 까니 기울기도 높이도 저절로 맞는다.
            //   **큰 건물에 큰 조각이 아니라, 같은 조각을 더 많이.**
            const float Tile = Half + 0.3f;             // 한 장이 덮는 폭(겹침 0.3m 포함)
            float ridgeY = 0f;
            for (int gx = 0; gx < 2; gx++)
                for (int gz = 0; gz < 2; gz++)
                {
                    // x 두 장이 마주 보며 용마루를 만든다(민가와 같은 yaw 0/180), z로 두 장 잇는다.
                    var piece = RoomPropObject(bank.transform, "BankRoof" + gx + gz, Town + "roof.fbx",
                        new Vector3(at.x, at.y, at.z), gx == 0 ? 0f : 180f, Tile, false);
                    if (piece == null || !BoundsOf(piece.transform, true, out Bounds pb))
                        continue;
                    float px = at.x + (gx == 0 ? -1f : 1f) * Tile * 0.5f;
                    float pz = at.z + (gz == 0 ? -1f : 1f) * Tile * 0.5f;
                    piece.transform.position += new Vector3(px, at.y + wallTop - 0.2f, pz)
                                                - new Vector3(pb.center.x, pb.min.y, pb.center.z);
                    if (BoundsOf(piece.transform, true, out Bounds after))
                        ridgeY = Mathf.Max(ridgeY, after.max.y - at.y);
                }
            // 박공(양 끝 막이)은 **안 붙인다** — 붙여 보니 조각 깊이가 그대로 더해져 건물 z가 8.1m가 됐고,
            // `GroundFit`이 가로 8m 넘는 것을 「한 물건이 아니라 통」으로 보고 자식으로 내려가 굴뚝이
            // 「지표에서 3.4m 떠 있다」로 빨간불이 났다(실측). 이 킷의 `roof.fbx`는 한 장이 이미 양 사면을
            // 가진 조각이라 끝이 뚫리지 않는다 — 민가가 박공을 쓰는 이유는 그쪽 조각이 외사면이기 때문이다.
            BoundsOf(bank.transform, true, out Bounds bankB);
            Debug.Log("[Ulon] 은행 지붕 — 조각 " + Tile.ToString("0.0") + "m 네 장, 용마루 높이 " +
                      ridgeY.ToString("0.00") + "m(벽 " + wallTop.ToString("0.00") + "m 위) · 건물 바운드 " +
                      bankB.size.x.ToString("0.00") + "×" + bankB.size.z.ToString("0.00") + "×" +
                      bankB.size.y.ToString("0.00") + "m");
            var chimney = RoomPropObject(bank.transform, "BankChimney", Town + "chimney.fbx",
                new Vector3(at.x + 1.2f, at.y, at.z + 1.2f), 0f, 1.2f, true);
            if (chimney != null)
                chimney.transform.position += Vector3.up * (wallTop + 1.0f);
            // **새로 만든 콜라이더는 동기화 전까지 광선에 안 잡힌다** — 이걸 안 부르면 시야 페이드
            // 게이트가 「걷힌 렌더러 0개」로 빨간불이 난다(2026-09-07 실측, 원인 찾는 데 세 번 헛짚었다).
            Physics.SyncTransforms();
            Debug.Log("[Ulon] 은행 건물 재건 — 벽 8칸(문 1·창 7)·지붕·굴뚝, 한 변 " + (Half * 2f) + "m");
        }

        /// <summary>
        /// **마을 시설을 그 기능으로 읽히게 꾸민다**(검수 랩 ①, 2026-09-07).
        /// 대조표에서 드러난 것: 상점과 대장간이 같은 좌판, 목공소 20cm, 화덕이 등불, 낚시터가 물레방아.
        /// 새 팩을 받지 않고 **저장소 조각**으로 붙인다 — 안 되는 것이 남으면 그게 오너 안건의 근거다.
        /// 멱등: 붙인 조각(`FacPart*`)을 매번 헐고 다시 붙인다.
        /// </summary>
        public static void EnsureVillageFacilities()
        {
            const string Town = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/";
            const string Dg = "Assets/_ThirdParty/KayKit/Dungeon/RAW/Models/";
            int touched = 0;

            // 상점 — 차양과 쌓인 물건이 「파는 곳」을 읽히게 한다. 대장간과 **다른 주 메시**가 되도록
            // 차양을 크게 씌운다(지금은 둘 다 stall.fbx 좌판이라 화면에서 구분이 안 됐다).
            touched += FacilityPart("Vendor", "Canopy", Town + "stall-red.fbx", new Vector3(0f, 0f, 0f), 2.2f, true) ? 1 : 0;
            touched += FacilityPart("Vendor", "Crate", Dg + "box_large.obj", new Vector3(0.9f, 0f, 0.6f), 0.8f, true) ? 1 : 0;

            // 대장간 — 굴뚝(화로의 연기)·통. 저장소에 모루·화로 메시가 없어 **굴뚝으로 대신**했다.
            // 화면에서 「대장간」으로 안 읽히면 그 샷이 곧 오너 안건의 근거다(검수 지시).
            touched += FacilityPart("Forge", "Chimney", Town + "chimney.fbx", new Vector3(0.7f, 0f, 0.5f), 2.6f, true) ? 1 : 0;
            touched += FacilityPart("Forge", "Barrel", Dg + "barrel_large.obj", new Vector3(-0.8f, 0f, 0.4f), 1.0f, true) ? 1 : 0;

            // 목공소 — 널빤지·톱질대. 20cm 걸상만으로는 멀리서 아무것도 아니다.
            touched += FacilityPart("Carpenter", "Planks", Town + "planks.fbx", new Vector3(0.6f, 0f, 0.3f), 1.4f, false) ? 1 : 0;
            touched += FacilityPart("Carpenter", "Post", Town + "poles.fbx", new Vector3(-0.2f, 0f, 0.8f), 1.5f, true) ? 1 : 0;
            touched += FacilityPart("Carpenter", "Stairs", Town + "stairs-wood.fbx", new Vector3(-0.7f, 0f, 0.2f), 0.9f, true) ? 1 : 0;

            // 화덕 — 등불이 화덕 노릇 하던 자리. 둘러싼 돌과 장작을 놓는다(불빛은 기존 등불이 낸다).
            // 화덕은 **등불 기둥**이 본체였다(검수 반려 「돌 놓인 데크」의 진짜 몸통) — 등불 시각을 치우고
            // 돌을 불 둘레에 **둥글게** 놓는다. 낚시터에서 물레방아를 치운 것과 같은 처방이다.
            HideOwnVisual("Campfire");
            touched += FacilityPart("Campfire", "Stone1", Dg + "rubble_half.obj", new Vector3(0.55f, 0f, 0f), 0.62f, true) ? 1 : 0;
            touched += FacilityPart("Campfire", "Stone2", Dg + "rubble_half.obj", new Vector3(-0.55f, 0f, 0f), 0.62f, true) ? 1 : 0;
            touched += FacilityPart("Campfire", "Stone3", Dg + "rubble_half.obj", new Vector3(0f, 0f, 0.55f), 0.62f, true) ? 1 : 0;
            touched += FacilityPart("Campfire", "Stone4", Dg + "rubble_half.obj", new Vector3(0f, 0f, -0.55f), 0.62f, true) ? 1 : 0;
            touched += FacilityPart("Campfire", "Wood", Town + "planks.fbx", new Vector3(0f, 0f, 0f), 0.9f, false) ? 1 : 0;

            // 절구 — 약병 대신 통·궤. 걸상 하나로는 연금 자리로 안 읽힌다.
            touched += FacilityPart("Mortar", "Barrel", Dg + "barrel_small.obj", new Vector3(0.5f, 0f, 0.3f), 1.0f, true) ? 1 : 0;
            touched += FacilityPart("Mortar", "Box", Dg + "box_small.obj", new Vector3(-0.5f, 0f, 0.2f), 0.6f, true) ? 1 : 0;

            // 훈련소 — 무엇을 가르치는지 표식. 사람 모델 교체는 별개 랩이다(대조표 §3).
            touched += FacilityPart("Trainer", "Banner", Town + "banner-red.fbx", new Vector3(1.0f, 0f, 0.4f), 2.0f, true) ? 1 : 0;

            // 마구간 — 축사 울타리. 지기(사람)는 서비스 NPC 랩에서 세운다.
            touched += FacilityPart("Stable", "Fence1", Town + "fence.fbx", new Vector3(1.4f, 0f, 0.6f), 1.2f, false) ? 1 : 0;
            touched += FacilityPart("Stable", "Fence2", Town + "fence-gate.fbx", new Vector3(-1.4f, 0f, 0.6f), 1.2f, false) ? 1 : 0;

            // 낚시터 — **물레방아를 치운다**(마을 시야를 막던 그 물건이기도 하다). 물가 발판·기둥·수레.
            HideOwnVisual("FishingSpot");
            // 발판을 크게 — **주 메시가 발판**이어야 한다. 기둥(poles)이 주 메시가 되면 집터와 같은 메시라
            // 화면에서 낚시터와 집터가 구분되지 않는다(게이트가 잡았다).
            touched += FacilityPart("FishingSpot", "Dock", Town + "planks.fbx", new Vector3(0f, 0f, 0f), 2.6f, false) ? 1 : 0;
            touched += FacilityPart("FishingSpot", "Pole", Town + "poles.fbx", new Vector3(0.9f, 0f, 0.6f), 1.0f, true) ? 1 : 0;

            Physics.SyncTransforms();
            Debug.Log("[Ulon] 마을 시설 꾸밈 — 조각 " + touched + "개(상점 차양·대장간 굴뚝·목공 널빤지·화덕 돌·절구 통·훈련 깃발·마구간 울타리·낚시 발판)");
        }

        /// <summary>
        /// **낚시터를 실제 물가로 옮긴다**(검수 반려 2026-09-07: 「데크가 잔디 위, 물이 화면에 없다」).
        ///
        /// 이 패스는 **지형이 다 만들어지고 지표 스냅까지 끝난 뒤에** 돌아야 한다 — 처음엔 배치 순서
        /// 앞쪽(EnsureFishSpot)에서 옮겼더니 ① 그때의 지형은 아직 옛 것이었고 ② 뒤따라 도는
        /// `EnsureFootOnGround`가 발판을 다시 둑 위로 끌어올렸다(실측: 수면 +5.1m인데 게이트 초록불).
        /// 그래서 **실제 하이트맵을 걸어 수면과 만나는 지점**을 찾고, 그 뒤에는 아무도 안 건드리게 한다.
        /// </summary>
        /// <summary>
        /// 잔교 조각을 놓아도 되는 자리인가 — **수면 위이고 그 둘레가 평평한가**.
        /// 둘레를 안 보면 조각이 둑 끝·둑 어깨에 걸쳐 놓여 `SnapRootToGround`가 가장 높은 모서리에
        /// 맞추고, 발 높이 게이트가 「1.44m 떠 있다」고 문다(실측 2026-09-09).
        /// </summary>
        static bool PierFooting(Vector3 p, float reach)
        {
            float c = GroundY(p.x, p.z);
            float worst = 0f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                float h = GroundY(p.x + Mathf.Cos(a) * reach, p.z + Mathf.Sin(a) * reach);
                worst = Mathf.Max(worst, Mathf.Abs(h - c));
            }
            bool ok = c >= WorldTerrain.SeaLevel + 0.05f && worst <= 0.15f;
            Debug.Log("[Ulon] 잔교 자리 (" + p.x.ToString("0.0") + "," + p.z.ToString("0.0") +
                      ") 지표 " + c.ToString("0.00") + " · 둘레 최대차 " + worst.ToString("0.00") +
                      " → " + (ok ? "놓음" : "건너뜀"));
            return ok;
        }

        public static bool EnsureFishingSpotAtWater()
        {
            var go = GameObject.Find("FishingSpot");
            if (go == null)
                return false;
            var center = new Vector2(WorldTerrain.LakeX, WorldTerrain.LakeZ);
            var dir = (new Vector2(0f, 0f) - center).normalized;      // 마을(원점) 쪽 물가
            Vector3 want = go.transform.position;
            bool found = false;
            for (float d = 0f; d <= WorldTerrain.LakeRadius + 14f; d += 0.25f)
            {
                var p = center + dir * d;
                float h = GroundY(p.x, p.y);
                if (h < WorldTerrain.SeaLevel)
                    continue;                                          // 아직 물속
                want = new Vector3(p.x, h, p.y);                       // 지표가 수면과 만나는 첫 지점
                found = true;
                break;
            }
            if (!found)
                return false;
            if (Vector3.Distance(go.transform.position, want) > 0.5f)
                Debug.Log("[Ulon] 낚시터를 물가로 옮긴다 — " + go.transform.position.ToString("0.0") + " → " +
                          want.ToString("0.0") + " (수면 " + WorldTerrain.SeaLevel + "m)");
            go.transform.position = want;

            // 잔교 널판 — 지형 둑(`WorldTerrain.RaisePier`) 위에 깔아 「물로 뻗은 나무 다리」로 읽히게.
            // 조각은 지표에 스냅되므로 둑을 따라 놓기만 하면 된다(띄우지 않는다).
            // 널판은 **낚시터의 자식으로 두지 않는다** — 뒤에 도는 시설 패스가 낚시터를 다시 지으면
            // 같이 사라진다(실측: 4장 만들었다는 로그는 남는데 저장된 씬에는 없었다).
            var pier = GameObject.Find("FishingPier");
            if (pier != null)
                UnityEngine.Object.DestroyImmediate(pier);
            pier = new GameObject("FishingPier");
            const string PierPlank = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string PierPole = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            // `want`는 호수 중심에서 걸어 나오다 처음 만난 뭍 = **둑의 끝**이다. 그러니 널판은
            // `dir`(뭍 쪽)으로 깔아야 둑을 덮는다 — 반대로 깔았더니 물속에 잠겨 보이지 않았다(실측).
            var away = new Vector3(dir.x, 0f, dir.y);
            float yaw = Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg;
            int made = 0, poles = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = want + away * (1.2f + i * 2.2f);
                // **물 위에는 안 놓는다** — 둑을 벗어난 조각은 호수 바닥에 스냅돼 「1.44m 떠 있다」로
                // 발 높이 게이트가 문다(실측). 지표가 수면 위인 자리만 쓴다.
                if (!PierFooting(p, 1.0f))
                    continue;
                var plank = Place(PierPlank, new Vector3(p.x, 0f, p.z), new Vector3(0f, yaw, 0f));
                if (plank == null)
                    continue;
                plank.name = "PierPlank" + (i + 1);
                plank.transform.SetParent(pier.transform, true);
                made++;
            }
            for (int i = 0; i < 2; i++)
            {
                // 말뚝은 **널판 옆**에 선다 — 가운데 박으면 잔교 한복판을 막는다(실측 화면).
                var side = new Vector3(-away.z, 0f, away.x) * 1.4f;
                var p = want + away * (1.6f + i * 2.8f) + side;   // 한쪽 갓길에 나란히 — 반대쪽은 비탈이라 발이 뜬다
                if (!PierFooting(p, 0.9f))
                    continue;
                var pole = Place(PierPole, new Vector3(p.x, 0f, p.z), new Vector3(0f, yaw, 0f));
                if (pole == null)
                    continue;
                pole.name = "PierPole" + (i + 1);
                pole.transform.SetParent(pier.transform, true);
                poles++;
            }
            Debug.Log("[Ulon] 잔교 — 널판 " + made + "장·말뚝 " + poles + "개 (둑 끝 " + want.ToString("0.0") +
                      ", 뭍 방향 " + away.ToString("0.00") + ")");
            return true;
        }

        /// <summary>
        /// **망토는 보스만**(검수 판정 2026-09-07 ②, §10.2·b2905b55). 예외 없음 —
        /// 플레이어도 Knight이고 보스도 Knight라, 망토가 유일한 구분 축인 방이 둘(던전 1·3) 있다.
        /// 플레이어 표식이 필요해지면 **다른 축**(색·문장·장비)으로 주고 그때 다시 판정받는다.
        /// 게이트는 `AssertCapeIsBossOnly`.
        /// </summary>
        public static int EnsureCapeIsBossOnly()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int off = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var wb = actors[i].GetComponent<Ulon.Server.WorldBody>();
                if (wb != null && !string.IsNullOrEmpty(wb.MobId) && Ulon.Shared.MobCatalog.IsBoss(wb.MobId))
                    continue;
                foreach (var t in actors[i].GetComponentsInChildren<Transform>(true))
                    if (IsCapeName(t.name) && t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                        off++;
                    }
            }
            if (off > 0)
                Debug.Log("[Ulon] 망토 정리 — 보스가 아닌 " + off + "개를 껐다(망토는 §10.2 보스 표식, 예외 없음)");
            return off;
        }

        public const string CampfireFlameObject = "CampfireFlame";

        /// <summary>
        /// **화덕에 불을 붙인다**(검수 반려 2026-09-07: 「널빤지 위에 회색 돌 두 개, 불이 없다」).
        /// 불 **메시**는 저장소에 없지만 §8.2가 막는 것은 무텍스처 프리미티브 메시이지 파티클이 아니다 —
        /// 등록 CC0 스프라이트(Kenney Particles)로 불꽃을 피우고 따뜻한 점광을 같이 둔다.
        /// 멱등: 있으면 헐고 다시 만든다.
        /// </summary>
        public static bool EnsureCampfireFire()
        {
            var go = GameObject.Find("Campfire");
            if (go == null)
                return false;
            // **다시 세울 수 있는지 먼저 확인하고 지운다**(랩 ③ 전수 조사에서 나온 비대칭).
            // 예전엔 불꽃을 먼저 지우고 텍스처가 없으면 false로 빠져 **불이 사라진 채** 남았다.
            const string texPath = "Assets/_ThirdParty/Kenney/Particles/RAW/Textures/flame_01.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogWarning("[Ulon] 불꽃 스프라이트가 없어 화덕에 불을 못 붙였습니다(기존 불은 그대로 둡니다): " + texPath);
                return false;
            }
            for (int c = go.transform.childCount - 1; c >= 0; c--)
                if (go.transform.GetChild(c).name == CampfireFlameObject)
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(c).gameObject);

            // 불은 **돌 사이 한가운데 위**에서 난다 — 기준은 오브젝트 원점이 아니라 보이는 것의 중심이다.
            // **자기 출력물(돌·장작)을 기준으로 삼지 않는다** — 그러면 돌 때마다 불이 밀린다(검수 판정 ③).
            var anchor = HostAnchor(go.transform);
            float footY = go.transform.position.y;
            if (BoundsOf(go.transform, true, out Bounds b) && b.size.sqrMagnitude > 0.0001f)
                footY = b.min.y;
            var at = new Vector3(anchor.x, footY + 0.25f, anchor.z);

            var flame = new GameObject(CampfireFlameObject);
            flame.transform.SetParent(go.transform, true);
            flame.transform.position = at;

            var ps = flame.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1.2f;
            main.startLifetime = 0.75f;
            main.startSpeed = 1.1f;
            main.startSize = 0.55f;
            main.startColor = new Color(1f, 0.55f, 0.13f, 0.95f);
            main.gravityModifier = -0.12f;                    // 위로 오른다
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var emission = ps.emission;
            emission.rateOverTime = 22f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.22f;
            var rend = flame.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = CampfireFlameMaterial(tex);

            // 불빛 — 밤·실내가 아니어도 「불이 켜져 있다」를 화면에 만든다.
            var lightGo = new GameObject("CampfireLight");
            lightGo.transform.SetParent(flame.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 6f;
            light.intensity = 2.2f;
            Debug.Log("[Ulon] 화덕에 불 — 파티클(등록 CC0 flame_01) + 점광. 불 메시가 없다고 화덕을 비워 두지 않는다.");
            return true;
        }

        static Material CampfireFlameMaterial(Texture2D tex)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Game/Art/VFX"));
            const string matPath = "Assets/Game/Art/VFX/CampfireFlame.mat";
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.mainTexture = tex;
            var tint = new Color(1f, 0.55f, 0.13f);
            mat.color = tint;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 4f);                    // Additive
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// **마구간 울타리를 닫는다**(검수 반려: 「끊어진 울타리 조각 셋이라 축사로 안 읽힌다」).
        /// 조각은 이미 있다 — 새 자산 없이 **둘러싸게** 놓는다. 한 변에 두 칸씩 네 변, 앞면 한 칸은 문.
        /// </summary>
        public const string StableBeastObject = "StableBeast";

        /// <summary>
        /// **마구간 마당에 짐승을 세운다**(검수 완료 기준 2026-09-07 동물 랩).
        /// 울타리만 닫아 놓으면 「빈 마당」이라 마구간으로 안 읽힌다 — 맡겨 둔 짐승 한 마리가 그 기능을 말한다.
        /// 조련 대상이 아니라 **배치물**이다(WorldBody 없음 — 잡거나 조련할 수 있는 것으로 오해되면 안 된다).
        /// 멱등: 있으면 헐고 다시 세운다.
        /// </summary>
        public static bool EnsureStableBeast()
        {
            var stable = GameObject.Find(StableYard.Object);
            if (stable == null)
                return false;
            for (int c = stable.transform.childCount - 1; c >= 0; c--)
                if (stable.transform.GetChild(c).name == StableBeastObject)
                    UnityEngine.Object.DestroyImmediate(stable.transform.GetChild(c).gameObject);
            const string fbx = "Assets/_ThirdParty/OpenGameArt/Deer/RAW/Deer.obj";
            var anchor = HostAnchor(stable.transform);          // 자기 울타리·짐승을 뺀 본체 중심
            var at = new Vector3(anchor.x + 1.6f, 0f, anchor.z - 1.2f);
            var go = Place(fbx, at, new Vector3(0f, 200f, 0f));
            if (go == null)
                return false;
            go.name = StableBeastObject;
            go.transform.SetParent(stable.transform, true);
            FitCreatureHeight(go, MobCatalog.HeightOf(TameCritter.Id));
            PaintCreature(go, true);
            EnsureCollider(go);
            BoundsOf(go.transform, true, out Bounds bb);
            Debug.Log("[Ulon] 마구간 짐승 — 자리 " + go.transform.position.ToString("0.00") + " 크기 " + bb.size.ToString("0.00") +
                      " (마구간 중심 " + anchor.ToString("0.00") + ")");
            return true;
        }

        public static int EnsureStableYardFence()
        {
            var go = GameObject.Find("Stable");
            if (go == null)
                return 0;
            const string fence = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx";
            const string gate = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence-gate.fbx";
            for (int c = go.transform.childCount - 1; c >= 0; c--)
                if (go.transform.GetChild(c).name.StartsWith("YardFence", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(c).gameObject);

            var center = HostAnchor(go.transform);   // 자기 울타리·부속을 뺀 본체 중심(검수 판정 ③)
            center.y = 0f;
            const float half = 2.4f;                          // 마당 반폭 — 말 한 마리와 사람이 서는 크기
            const float step = 1.6f;                          // 판 하나의 폭 = 칸 간격(딱 맞물려야 「닫혔다」로 읽힌다)
            int made = 0;
            // 네 변을 세 칸씩. 앞변(마을 쪽) 가운데 한 칸만 문으로 바꾼다 — 들어가는 길이 보여야 축사다.
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                var normal = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                for (int k = -1; k <= 1; k++)
                {
                    bool door = side == 2 && k == 0;
                    var at = center + normal * half + dir * (step * k);
                    var made1 = RoomPropObject(go.transform, "YardFence" + side + "_" + k,
                        // 이 킷 울타리는 **긴 축이 Z**다(실측 0.12×0.61×1.60) — yaw 그대로 놓으면 판이
                        // 변에 수직으로 서서 이음매가 벌어진다(입구 벽판에서 겪은 것과 같은 함정).
                        door ? gate : fence, OnGround(new Vector3(at.x, 0f, at.z)), yaw + 90f, step, false);
                    if (made1 != null)
                    {
                        // 조각 원점이 모서리인 프리팹이 있다 — 바운드 중심으로 다시 맞춘다.
                        if (BoundsOf(made1.transform, true, out Bounds mb))
                        {
                            made1.transform.position += new Vector3(at.x - mb.center.x, 0f, at.z - mb.center.z);
                            if (made == 0)
                                Debug.Log("[Ulon] 마구간 울타리 판 크기 — " + mb.size.ToString("0.00") +
                                          " (칸 간격 " + step + "m와 맞아야 이음매가 안 벌어진다)");
                        }
                        made++;
                    }
                }
            }
            Debug.Log("[Ulon] 마구간 마당 — 울타리 " + made + "칸으로 둘러쌌다(앞변 한 칸은 문). 새 자산 없이 배치로 푼다.");
            return made;
        }
    }
}
