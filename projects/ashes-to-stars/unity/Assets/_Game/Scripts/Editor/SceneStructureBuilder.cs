using AshesToStars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 게임 전체 구조를 **한 씬에** 펼쳐 놓는다 — 유니티 에디터에서 열어 보는 용도.
///
/// 왜 한 씬인가:
///   처음엔 화면마다 씬을 하나씩(11개) 만들었는데 배치모드에서 유니티가 크래시했다.
///   한 실행에서 씬을 여러 번 새로 만들고 저장하는 것이 불안정하다.
///   게다가 목적이 "구조를 한눈에 보는 것"이므로 **한 씬에 구역을 나눠 펼치는 편**이
///   씬을 열었다 닫았다 하는 것보다 낫다. 씬 뷰에서 줌아웃하면 전체가 보인다.
///
/// 구성 (기획서 절 번호와 1:1):
///   가운데 위  전체 흐름도 (§2 코어 루프)
///   그 아래    화면 10개를 격자로 — 시작화면·영지·필드·던전·탑·레이드·월드맵·침략·경매장·캐릭터
///
/// 메뉴: 재와별 ▸ 게임 구조 씬 생성
/// </summary>
public static class SceneStructureBuilder
{
    const string DIR = "Assets/_Game/Scenes";
    const string PATH = DIR + "/게임구조_전체.unity";

    static Material _matShared;
    static BalanceConfig _b;

    [MenuItem("재와별/게임 구조 씬 생성", priority = 1)]
    public static void Build()
    {
        _b = AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/_Game/Data/GameBalance.asset")
             ?? ScriptableObject.CreateInstance<BalanceConfig>();
        _matShared = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Sprite_Shared.mat");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 46f;                       // 전체가 한 화면에 들어오게
        cam.transform.position = new Vector3(0, -18f, -50f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.045f, 0.05f, 0.065f);

        var guide = new GameObject("★ 먼저 읽기");
        var note = guide.AddComponent<SceneNote>();
        note.제목 = "재와 별 — 게임 전체 구조";
        note.기획서 = "docs/GAME_DESIGN_ASHES_TO_STARS.md";
        note.설명 =
            "이 씬은 게임의 화면 구조를 한눈에 보라고 만든 것이다(플레이용 아님).\n\n" +
            "· 맨 위      전체 흐름도 — 영지를 중심으로 콘텐츠가 어떻게 이어지는가(§2)\n" +
            "· 그 아래    화면 10개를 격자로 배치. 각 구역의 이름 앞 번호가 진행 순서\n\n" +
            "계층(Hierarchy)에서 구역을 접었다 펴며 보는 것이 가장 편하다.\n" +
            "수치를 고치려면 Assets/_Game/Data/ 의 ScriptableObject를 인스펙터에서 연다.\n\n" +
            "검증용 씬은 별도: Assets/Scenes/W1(성능)·W2(조작감)·W3(파티 대조실험)";

        BuildFlow(new Vector2(0, 22f));

        // 화면 10개 — 3열 격자
        var cells = new (string, System.Action<Transform>)[]
        {
            ("00_시작화면 (§16 온보딩)",      시작화면),
            ("01_영지_허브 (§13·§16)",        영지),
            ("02_필드 (§6)",                  필드),
            ("03_던전 (§7)",                  던전),
            ("04_탑_100층 (§8)",              탑),
            ("05_레이드 (§9)",                레이드),
            ("06_월드맵_우주성계 (§14)",      월드맵),
            ("07_침략_비동기공성 (§15)",      침략),
            ("08_경매장 (§12)",               경매장),
            ("09_캐릭터관리 (§3·§11)",        캐릭터관리),
        };

        const float CW = 30f, CH = 20f;
        for (int i = 0; i < cells.Length; i++)
        {
            int col = i % 3, row = i / 3;
            var origin = new Vector2(-CW + col * CW, 4f - row * CH);

            var g = new GameObject($"■ {cells[i].Item1}").transform;
            g.position = new Vector3(origin.x, origin.y, 0f);
            Frame(g, new Vector2(CW - 2f, CH - 3f));
            cells[i].Item2(g);
        }

        EditorSceneManager.SaveScene(scene, PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[구조씬] 생성 완료 → {PATH}");
    }

    // ── 헬퍼 ──────────────────────────────────────────────
    static Material Mat(Color c)
    {
        var m = new Material(Shader.Find("Sprites/Default")) { color = c };
        m.hideFlags = HideFlags.None;
        return m;
    }

    static GameObject Box(Transform p, string name, Vector2 local, Vector2 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = new Vector3(local.x, local.y, 0.5f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
        return go;
    }

    /// <summary>구역 테두리 — 씬 뷰에서 구역이 어디까지인지 보이게</summary>
    static void Frame(Transform p, Vector2 size)
    {
        Box(p, "─ 구역", Vector2.zero, size, new Color(0.10f, 0.11f, 0.14f, 0.85f));
    }

    /// <summary>설명은 계층 이름으로 — 배치모드에서 TextMesh는 폰트가 없어 크래시한다(실측)</summary>
    static void Note(Transform p, string text)
    {
        var go = new GameObject("· " + text);
        go.transform.SetParent(p, false);
    }

    static void Chr(Transform p, string prefab, Vector2 local)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Characters/{prefab}.prefab");
        if (pf == null) { Note(p, $"(프리팹 없음: {prefab})"); return; }
        var o = (GameObject)PrefabUtility.InstantiatePrefab(pf, p);
        o.transform.localPosition = new Vector3(local.x, local.y * _b.ISO_Y, 0f);
    }

    // ── 전체 흐름도 (§2) ──────────────────────────────────
    static void BuildFlow(Vector2 at)
    {
        var g = new GameObject("★★ 전체 흐름 (§2 코어 루프)").transform;
        g.position = new Vector3(at.x, at.y, 0f);

        var nodes = new (string, Vector2, Color)[]
        {
            ("영지(허브)",   new Vector2(0, 0),      new Color(0.48f, 0.40f, 0.24f, 0.95f)),
            ("필드",         new Vector2(-22, 5),    new Color(0.22f, 0.36f, 0.22f, 0.95f)),
            ("던전",         new Vector2(-22, -3),   new Color(0.26f, 0.22f, 0.32f, 0.95f)),
            ("탑 100층",     new Vector2(0, 9),      new Color(0.32f, 0.26f, 0.44f, 0.95f)),
            ("레이드",       new Vector2(22, 9),     new Color(0.48f, 0.18f, 0.18f, 0.95f)),
            ("월드맵",       new Vector2(22, 1),     new Color(0.18f, 0.26f, 0.48f, 0.95f)),
            ("침략(30층~)",  new Vector2(22, -7),    new Color(0.48f, 0.30f, 0.15f, 0.95f)),
            ("경매장(30층~)",new Vector2(0, -8),     new Color(0.38f, 0.34f, 0.18f, 0.95f)),
            ("캐릭터관리",   new Vector2(-22, -11),  new Color(0.30f, 0.22f, 0.34f, 0.95f)),
        };
        foreach (var (n, p, c) in nodes) Box(g, n, p, new Vector2(11f, 4.4f), c);

        Note(g, "탑 10층 돌파 → 필드·던전 난이도 상승");
        Note(g, "30층 돌파 → 침략·경매장 동시 해금");
        Note(g, "사망 3회 → 캐릭터 삭제 → 영묘 → 환생석으로 부활");
    }

    // ── 화면 10개 ─────────────────────────────────────────
    static void 시작화면(Transform g)
    {
        Note(g, "직업 2종을 골라 시작 (§3)");
        var jobs = new[] { ("탱커", "수호기사"), ("딜러", "검사"), ("힐러", "사제"), ("버퍼", "음유시인") };
        for (int i = 0; i < 4; i++)
        {
            float x = -9f + i * 6f;
            Box(g, $"선택카드_{jobs[i].Item1}", new Vector2(x, 0), new Vector2(5.2f, 8f),
                new Color(0.16f, 0.18f, 0.24f, 0.95f));
            Chr(g, jobs[i].Item2, new Vector2(x, -1f));
        }
        Note(g, "첫 사망 시 전체화면 경고 — 삭제 규칙 고지");
    }

    static void 영지(Transform g)
    {
        Note(g, "허브 — 건물을 눌러 각 콘텐츠로");
        var b = new (string, Vector2, Color)[]
        {
            ("본성",           new Vector2(0, 2),     new Color(0.46f, 0.40f, 0.28f, 0.95f)),
            ("자원광산",       new Vector2(-8, 4),    new Color(0.34f, 0.30f, 0.20f, 0.95f)),
            ("창고(약탈대상)", new Vector2(-8, 0),    new Color(0.30f, 0.28f, 0.22f, 0.95f)),
            ("대장간(제작)",   new Vector2(8, 4),     new Color(0.42f, 0.26f, 0.20f, 0.95f)),
            ("영묘(환생)",     new Vector2(8, 0),     new Color(0.24f, 0.24f, 0.36f, 0.95f)),
            ("수비대주둔지",   new Vector2(0, -2.5f), new Color(0.28f, 0.34f, 0.40f, 0.95f)),
            ("화살탑",         new Vector2(-10, -5),  new Color(0.34f, 0.34f, 0.34f, 0.95f)),
            ("마법탑",         new Vector2(10, -5),   new Color(0.28f, 0.28f, 0.44f, 0.95f)),
        };
        foreach (var (n, p, c) in b) Box(g, n, p, new Vector2(5.6f, 3.2f), c);
        Box(g, "하단바 5칸 (영지·필드·탑·월드맵·캐릭터)", new Vector2(0, -7.5f),
            new Vector2(26f, 1.6f), new Color(0.10f, 0.10f, 0.14f, 0.98f));
        Note(g, "건설 시간은 레벨 비례(상한 24h) · 단축은 골드·재료로만");
    }

    static void 필드(Transform g)
    {
        var t = new GameObject("노이즈_터레인(절차생성)");
        t.transform.SetParent(g, false);
        var nt = t.AddComponent<NoiseTerrain>();
        nt.반경 = 12f; nt.ISO_Y = _b.ISO_Y; nt.격자간격 = 1.5f;
        Note(g, "지형·바닥만 노이즈맵 절차 생성");
        var party = new[] { "수호기사", "검사", "마법사", "사제", "음유시인" };
        for (int i = 0; i < 5; i++) Chr(g, party[i], new Vector2(-4f + i * 2f, 0));
        Note(g, "출전 1~5명 자유 · 경험치는 레벨 비례 분배");
    }

    static void 던전(Transform g)
    {
        Box(g, "웨이브 구간(자동)", new Vector2(-6, 0), new Vector2(12f, 10f), new Color(0.13f, 0.12f, 0.15f, 0.95f));
        Box(g, "종점 보스존(지휘 조작 열림)", new Vector2(7, 0), new Vector2(11f, 10f), new Color(0.30f, 0.11f, 0.13f, 0.95f));
        Chr(g, "검사", new Vector2(-6, -1));
        Note(g, "랜덤 생성 · 입장 제한 없음 · 사망 카운트 적용");
    }

    static void 탑(Transform g)
    {
        for (int f = 1; f <= 10; f++)
        {
            bool 대보스 = f % 2 == 0;
            bool 하드 = f >= 6;
            Box(g, $"{f * 10}층{(대보스 ? " ★대보스·환생석" : "")}{(하드 ? " [하드코어]" : "")}",
                new Vector2(0, -7f + f * 1.5f), new Vector2(20f, 1.2f),
                대보스 ? new Color(0.42f, 0.16f, 0.16f, 0.95f)
                       : new Color(0.20f, 0.22f, 0.30f, 0.95f));
        }
        Note(g, "5층마다 레이드 · 10층마다 대보스 · 50층+ 특수직업");
    }

    static void 레이드(Transform g)
    {
        Box(g, "보스(페이즈마다 스킬 +1)", new Vector2(0, 5), new Vector2(7f, 5f), new Color(0.48f, 0.14f, 0.14f, 0.95f));
        Box(g, "장판_예고A", new Vector2(-6, 0), new Vector2(6f, 3f), new Color(0.75f, 0.2f, 0.2f, 0.30f));
        Box(g, "장판_예고B", new Vector2(6, -2), new Vector2(6f, 3f), new Color(0.75f, 0.2f, 0.2f, 0.30f));
        Chr(g, "수호기사", new Vector2(0, 1));
        Chr(g, "검사", new Vector2(-3, -2));
        Chr(g, "마법사", new Vector2(3, -2));
        Chr(g, "사제", new Vector2(-2, -5));
        Chr(g, "음유시인", new Vector2(2, -5));
        Note(g, "진형: 탱 앞 / 딜 중간 / 힐·버퍼 뒤 — 1인 클리어 거의 불가능");
    }

    static void 월드맵(Transform g)
    {
        Box(g, "우주 배경", Vector2.zero, new Vector2(27f, 16f), new Color(0.02f, 0.02f, 0.06f, 1f));
        var rng = new System.Random(11);
        for (int i = 0; i < 12; i++)
        {
            float x = -11f + (float)rng.NextDouble() * 22f;
            float y = -6f + (float)rng.NextDouble() * 12f;
            float sz = 0.5f + (float)rng.NextDouble() * 1.5f;
            Box(g, $"인식범위_{i}", new Vector2(x, y), Vector2.one * (sz * 4f), new Color(0.3f, 0.5f, 0.9f, 0.09f));
            Box(g, $"타유저_별_{i}", new Vector2(x, y), Vector2.one * sz, new Color(0.70f, 0.80f, 1f, 0.95f));
        }
        Box(g, "★내 별(층 돌파할수록 커진다)", Vector2.zero, Vector2.one * 2.4f, new Color(1f, 0.88f, 0.45f, 1f));
        Note(g, "탐험한 곳만 밝혀짐 · 인식 범위에서 적 디버프·아군 버프");
    }

    static void 침략(Transform g)
    {
        Box(g, "상대 영지(격자 8x8~16x16)", Vector2.zero, new Vector2(20f, 11f), new Color(0.15f, 0.14f, 0.12f, 0.95f));
        Box(g, "창고 — 여기 도달해야 약탈 성공", Vector2.zero, new Vector2(3.4f, 2.4f), new Color(0.62f, 0.50f, 0.20f, 0.98f));
        foreach (var (n, p) in new (string, Vector2)[]{ ("성벽A", new Vector2(-5,3)), ("성벽B", new Vector2(5,3)),
                                                        ("화살탑", new Vector2(-5,-3)), ("함정", new Vector2(5,-3)) })
            Box(g, n, p, new Vector2(3f, 2f), new Color(0.34f, 0.34f, 0.40f, 0.95f));
        foreach (var (n, p) in new (string, Vector2)[]{ ("진입_북", new Vector2(0,6.5f)), ("진입_남", new Vector2(0,-6.5f)),
                                                        ("진입_서", new Vector2(-11,0)), ("진입_동", new Vector2(11,0)) })
            Box(g, n, p, new Vector2(3.4f, 1.1f), new Color(0.80f, 0.42f, 0.20f, 0.85f));
        Note(g, "침략자가 4면 중 진입 방향을 고른다 → 만능 배치 불가");
        Note(g, "보호막 12시간 = 수비대 회복 시간과 동일");
    }

    static void 경매장(Transform g)
    {
        Box(g, "매물 목록", new Vector2(-7, 0), new Vector2(11f, 12f), new Color(0.13f, 0.14f, 0.18f, 0.98f));
        Box(g, "상세·입찰", new Vector2(6, 2.5f), new Vector2(11f, 7f), new Color(0.16f, 0.16f, 0.21f, 0.98f));
        Box(g, "내 등록", new Vector2(6, -4.5f), new Vector2(11f, 4f), new Color(0.14f, 0.17f, 0.14f, 0.98f));
        Note(g, "드랍·제작 아이템만 거래 · 업적 보상은 귀속");
        Note(g, "수수료 소각(등록2%+판매8%) · 부활초는 소지 상한");
    }

    static void 캐릭터관리(Transform g)
    {
        Box(g, "보유 캐릭터 목록", new Vector2(-8, 0), new Vector2(8f, 12f), new Color(0.13f, 0.14f, 0.18f, 0.98f));
        Box(g, "장비 6부위", new Vector2(4, 3), new Vector2(12f, 6f), new Color(0.16f, 0.16f, 0.21f, 0.98f));
        Box(g, "합성(패시브 흡수, 슬롯 4)", new Vector2(4, -3.5f), new Vector2(12f, 4f), new Color(0.20f, 0.15f, 0.21f, 0.98f));
        Chr(g, "수호기사", new Vector2(-8, 1));
        Note(g, "목숨 ❤❤❤ 상시 노출 · 마지막 목숨은 경고");
        Note(g, "전직 · 합성 · 장비 · 영묘(환생)");
    }
}

/// <summary>씬을 연 사람이 무엇을 보고 있는지 인스펙터에서 읽을 수 있게</summary>
public class SceneNote : MonoBehaviour
{
    public string 제목;
    public string 기획서;
    [TextArea(4, 14)] public string 설명;
}
