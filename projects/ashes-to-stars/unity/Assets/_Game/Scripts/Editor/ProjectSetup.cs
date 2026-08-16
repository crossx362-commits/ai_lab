using System.Collections.Generic;
using System.IO;
using AshesToStars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 프로젝트 구조 생성기 — 에디터에서 열었을 때 **실제로 보이는 것**을 만든다.
///
/// 지금까지 W1~W3는 전부 런타임에 GameObject를 코드로 만들었다. 성능 검증에는
/// 충분했지만 유니티 에디터로 프로젝트를 열면 빈 씬만 보여서, 기획자가 수치를
/// 확인하거나 배치를 만져볼 수가 없다.
///
/// 이 스크립트는 다음을 **에셋으로** 만든다:
///   - Data/    종족 4 · 직업 11 · 몬스터 · 전투 스타일 4 · 밸런스 설정 (ScriptableObject)
///   - Prefabs/ 캐릭터·몬스터·투사체 프리팹 (인스펙터에서 열어볼 수 있음)
///   - Scenes/Sandbox  실제로 배치된 씬 — 열면 바로 보인다
///
/// 메뉴: 재와별 ▸ 프로젝트 구조 생성
/// </summary>
public static class ProjectSetup
{
    const string ROOT = "Assets/_Game";
    static readonly string[] FOLDERS =
    {
        ROOT, ROOT + "/Art", ROOT + "/Art/Sprites", ROOT + "/Art/Ground", ROOT + "/Art/FX",
        ROOT + "/Data", ROOT + "/Data/Races", ROOT + "/Data/Jobs", ROOT + "/Data/Mobs",
        "Assets/Resources/styles",   // 전투 스타일은 런타임이 읽어야 하므로 Resources 아래
        "Assets/Resources/races",    // 종족도 런타임(W3Party.ApplyRaceModifiers)이 읽는다 — 같은 이유
        ROOT + "/Prefabs", ROOT + "/Prefabs/Characters", ROOT + "/Prefabs/Mobs", ROOT + "/Prefabs/FX",
        ROOT + "/Scenes", ROOT + "/Materials",
        ROOT + "/Scripts", ROOT + "/Scripts/Runtime", ROOT + "/Scripts/Editor",
    };

    [MenuItem("재와별/프로젝트 구조 생성", priority = 0)]
    public static void Generate()
    {
        EnsureFolders();
        var mat = EnsureSpriteMaterial();
        var balance = MakeBalance();
        MakeRaces();
        var jobs = MakeJobs();
        var mobs = MakeMobs();
        MakeStyles();
        var prefabs = MakePrefabs(jobs, mobs, mat);
        MakeSandboxScene(prefabs, balance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[구조] 완료 — 종족4 · 직업{jobs.Count} · 몬스터{mobs.Count} · 스타일4 · 프리팹{prefabs.Count} · Sandbox 씬");
    }

    // ── 폴더 ─────────────────────────────────────────────
    static void EnsureFolders()
    {
        foreach (var f in FOLDERS)
        {
            if (AssetDatabase.IsValidFolder(f)) continue;
            var parent = Path.GetDirectoryName(f).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(f));
        }
    }

    static T Save<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(asset as Object, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static Material EnsureSpriteMaterial()
    {
        const string path = ROOT + "/Materials/Sprite_Shared.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default")) { enableInstancing = true };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Sprite LoadSprite(string name)
        => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/sprites/{name}.png");

    /// <summary>
    /// 오너가 준 픽셀아트 스프라이트를 먼저 찾고, 없으면 블렌더 플레이스홀더로 떨어진다.
    /// 방향별 시트가 아직 안 왔으므로(오너: "방향별로는 나중에 줄게") 정면 대기 프레임만 쓴다.
    /// </summary>
    static Sprite LoadPixelArt(RoleId role, string fallback)
    {
        string job = role switch
        {
            RoleId.탱 => "탱커",
            RoleId.딜 => "딜러",
            RoleId.힐 => "힐러",
            _ => "버퍼",
        };
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(
            $"{ROOT}/Art/Sprites/{job}/{job}_대기_00.png");
        return s != null ? s : LoadSprite(fallback);
    }

    // ── 밸런스 ────────────────────────────────────────────
    static BalanceConfig MakeBalance()
        => Save(ScriptableObject.CreateInstance<BalanceConfig>(), ROOT + "/Data/GameBalance.asset");

    // ── 종족 (§18-9) ──────────────────────────────────────
    static void MakeRaces()
    {
        // (id, 정체성, 경험치, 전직재료, 이속, 체력, 방어, 회복h, 영지, 드랍, 수수료, 고유, 확률)
        var rows = new (RaceId, string, float, float, float, float, float, float, float, float, float, string, float)[]
        {
            (RaceId.인간, "빨리 크고 빨리 회복하는 범용형", 1.15f, 1.15f, 1f, 1f, 1f, 18f, 1f, 1f, 7f,
                "적응 — 합성 시 원하는 계열 패시브 확률 +20%p", 0f),
            (RaceId.엘프, "죽지 않으려 움직이는 유리대포", 1f, 1f, 1f, 0.85f, 0.80f, 24f, 1f, 1f, 10f,
                "바람의 인도 — 이동 중 회피 +15%, 월드맵 탐험 범위 +30%", 0.15f),
            (RaceId.드워프, "안 죽고 버티며 쌓아올리는 생산가", 1f, 1f, 0.85f, 1f, 1f, 24f, 1.20f, 1f, 10f,
                "불굴 — 치명 피해 시 HP1로 생존 (전투당 1회)", 0.25f),
            (RaceId.수인, "먼저 덮치고 먼저 빠지는 사냥꾼", 1f, 1f, 1f, 1f, 1f, 24f, 0.80f, 1.15f, 10f,
                "야성 감각 — 치명 피해 직전 2초 이속 +50% (전투당 1회)", 1f),
        };
        foreach (var r in rows)
        {
            var a = ScriptableObject.CreateInstance<RaceDef>();
            a.Id = r.Item1; a.정체성 = r.Item2;
            a.경험치배율 = r.Item3; a.전직재료배율 = r.Item4; a.이속배율 = r.Item5;
            a.체력배율 = r.Item6; a.방어배율 = r.Item7; a.회복시간 = r.Item8;
            a.영지생산배율 = r.Item9; a.드랍률배율 = r.Item10; a.경매수수료 = r.Item11;
            a.약탈량배율 = r.Item1 == RaceId.수인 ? 1.20f : 1f;
            a.인식범위배율 = r.Item1 == RaceId.엘프 ? 1.20f : 1f;
            a.고유메커니즘 = r.Item12; a.고유발동확률 = r.Item13;
            // 런타임(W3Party)이 Resources.LoadAll<RaceDef>("races")로 읽으므로 Resources 아래에 둔다
            // (스타일과 같은 이유). Data/Races에 두면 빌드에 안 실려 소비처가 다시 죽는다.
            Save(a, "Assets/Resources/races/Race_" + r.Item1 + ".asset");
        }
    }

    // ── 직업 11종 (§3) ────────────────────────────────────
    static List<JobDef> MakeJobs()
    {
        var list = new List<JobDef>();
        // (한글이름, 영문이름, 역할, 컨셉, 고유, HP, 공격, 사거리, 이동기형태, 리스크, 자동적합도, 스킬3)
        var rows = new (string, string, RoleId, string, string, float, float, float, string, string, float, string[])[]
        {
            ("수호기사", "guardian", RoleId.탱, "정통 방패탱", "수호 게이지 — 피격·보호로 축적, 소모해 보호막", 320, 10, 1.3f, "방패 돌진", "최저", 0.35f,
                new[]{ "도발의 함성|광역 어그로 3초 — 원거리 몹까지 끈다|6|0|4.5",
                       "성채 방패|게이지 60 소모, 파티 보호막 40|0|0|8|60",
                       "최후의 보루|3초간 HP 1 미만으로 안 떨어짐|40|0|0" }),
            ("광전사", "berserker", RoleId.탱, "브루저(몸빵+근딜)", "분노 — HP가 낮을수록 공격력 상승", 260, 20, 1.5f, "구르기", "높음", 0.65f,
                new[]{ "피의 갈증|타격 시 흡혈|0|1|0", "무모한 돌진|돌진+광역|8|1.4|3", "사혈|자기 HP 소모 폭딜|12|2.5|0" }),
            ("검사", "swordsman", RoleId.딜, "근접 콤보 폭딜", "연격 스택 — 5스택에 일섬", 130, 26, 5.5f, "구르기", "중상", 0.7f,
                new[]{ "연속베기|스택 축적|0|1|0", "발도|무적 프레임 대시 — 회피 겸용|8|1.2|0",
                       "일섬|스택 5 전량 소모 단일 폭딜|0|3.2|0|5" }),
            ("궁수", "archer", RoleId.딜, "원거리 단일 저격", "집중 — 제자리에 서면 딜 상승", 115, 24, 8f, "백스텝", "낮음", 0.5f,
                new[]{ "조준 사격|단일 최고딜|3|2|0", "후퇴 사격|뒤로 점프하며 발사|7|1|0",
                       "매의 눈|대상 약점 노출 — 파티 딜 증가|20|0|0" }),
            ("마법사", "mage", RoleId.딜, "광역 섬멸", "마나 순환 — 다른 스킬 연계 시 쿨감(마나 없음)", 110, 26, 6.5f, "점멸", "중", 0.9f,
                new[]{ "화염폭풍|장판 광역 — 4체 이상 밀집 시|5|1.2|3.2",
                       "점멸|순간이동 회피|8|0|0", "빙결|광역 슬로우|10|0.4|4" }),
            ("소환사", "summoner", RoleId.딜, "소환수 지속딜", "소환 슬롯 — 본체 대신 전투", 120, 14, 7f, "위치 교체", "최저급", 0.95f,
                new[]{ "바위 골렘|탱형 소환|15|0|0", "임프 무리|딜형 다수 소환|12|0|0",
                       "희생 명령|소환수 폭발 광역딜|8|2|3.5" }),
            ("사제", "priest", RoleId.힐, "순수 회복 특화", "신앙 — 회복 누적으로 축적, 소모해 기적", 150, 6, 6.5f, "짧은 스텝", "낮음", 0.2f,
                new[]{ "성광|단일 대회복|1|0|0", "치유의 파동|광역 힐|1.4|0|7",
                       "기적|신앙 100 소모 — 파티 전체 완전 회복|0|0|99|100" }),
            ("드루이드", "druid", RoleId.힐, "도트힐+도트딜 하이브리드", "자연 표식 — 아군 회복·적 피해 동시", 150, 10, 6f, "짧은 스텝", "중", 0.4f,
                new[]{ "재생의 씨앗|도트힐|2|0|0", "가시덩굴|도트딜+둔화|4|0.8|3",
                       "야성 변신|짧게 곰 형태 — 임시 탱|30|0|0" }),
            ("음유시인", "bard", RoleId.버퍼, "오라 증폭기", "악장 전환 — 진군가/수호가/질주가 상시 연주", 150, 8, 6.5f, "짧은 스텝", "낮음", 0.25f,
                new[]{ "진군가|파티 공격 +15%|0|0|8", "수호가|파티 피해 -18%|0|0|8",
                       "피날레|현재 악장 효과 폭발|25|1.5|6" }),
            ("주술사", "shaman", RoleId.버퍼, "적 약화(디버프)", "저주 스택 — 같은 대상에 쌓을수록 증폭", 145, 12, 6f, "짧은 스텝", "낮음", 0.3f,
                new[]{ "무기력의 저주|적 공깎|4|0|0", "부패|방깎+도트|5|0.6|0",
                       "속박 토템|설치형 — 범위 내 이속 감소|15|0|4" }),
            ("정령사", "elementalist", RoleId.버퍼, "아군 강화(정령 부착)", "정령 계약 — 화/수/풍을 아군 1인에게 부착", 145, 10, 6.5f, "짧은 스텝", "낮음", 0.45f,
                new[]{ "화염 정령|부착 대상 공격에 화상|10|0|0", "물의 정령|부착 대상 지속 회복|10|0|0",
                       "바람 정령|부착 대상 이속·쿨감|10|0|0" }),
        };

        foreach (var r in rows)
        {
            var a = ScriptableObject.CreateInstance<JobDef>();
            a.직업명 = r.Item1; a.역할 = r.Item3; a.컨셉 = r.Item4; a.고유메커니즘 = r.Item5;
            a.최대체력 = r.Item6; a.공격력 = r.Item7; a.사거리 = r.Item8;
            a.이동기형태 = r.Item9; a.사망리스크 = r.Item10; a.자동사냥적합도 = r.Item11;
            a.공격간격 = r.Item3 == RoleId.딜 ? 0.40f : 0.7f;

            var skills = new List<SkillDef>();
            foreach (var raw in r.Item12)
            {
                var p = raw.Split('|');
                var sk = new SkillDef { 이름 = p[0], 설명 = p[1] };
                if (p.Length > 2) float.TryParse(p[2], out sk.쿨다운);
                if (p.Length > 3) float.TryParse(p[3], out sk.위력배율);
                if (p.Length > 4) float.TryParse(p[4], out sk.반경);
                if (p.Length > 5) float.TryParse(p[5], out sk.자원소모);
                skills.Add(sk);
            }
            a.스킬 = skills.ToArray();
            list.Add(Save(a, $"{ROOT}/Data/Jobs/Job_{r.Item2}.asset"));
        }
        return list;
    }

    // ── 몬스터 (§18-11) ───────────────────────────────────
    static List<MobDef> MakeMobs()
    {
        var list = new List<MobDef>();
        var basic = new (string, string, MobAi, float, string)[]
        {
            ("추적형", "chaser", MobAi.추적, 0.90f, "mob_chaser_0"),
            ("포위형", "swarmer", MobAi.포위, 0.85f, "mob_swarmer_0"),
            ("원거리형", "ranged", MobAi.원거리, 0.65f, "mob_ranged_0"),
            ("돌진형", "charger", MobAi.돌진, 0.90f, "mob_chaser_0"),
        };

        var familyNames = new (MobFamily, string)[]
        {
            (MobFamily.야수, "beast"),
            (MobFamily.언데드, "undead"),
            (MobFamily.마족, "demon"),
            (MobFamily.기계, "machine"),
            (MobFamily.정령, "elemental"),
        };

        foreach (var (f, fName) in familyNames)
            foreach (var (bName, bNameEng, ai, speed, sprite) in basic)
            {
                var a = ScriptableObject.CreateInstance<MobDef>();
                a.이름 = $"{f}_{bName}"; a.AI = ai; a.계열 = f; a.속도배율 = speed;
                a.스프라이트 = LoadSprite(sprite);
                a.색조 = FamilyColor(f);
                list.Add(Save(a, $"{ROOT}/Data/Mobs/Mob_{fName}_{bNameEng}.asset"));
            }

        // 정예 6유형 — 플레이어 직업 역할의 거울(§10-2)
        var elites = new (EliteKind, string, string, Color)[]
        {
            (EliteKind.수호자,   "수호자", "warden", new Color(0.6f, 0.7f, 1f)),
            (EliteKind.처형자,   "처형자", "executioner", new Color(1f, 0.45f, 0.35f)),
            (EliteKind.주술사,   "주술사", "shaman", new Color(0.4f, 1f, 0.5f)),
            (EliteKind.군단장,   "군단장", "legion_commander", new Color(1f, 0.85f, 0.3f)),
            (EliteKind.저주술사, "저주술사", "curser", new Color(0.7f, 0.4f, 0.9f)),
            (EliteKind.소환술사, "소환술사", "summoner", new Color(0.8f, 0.5f, 1f)),
        };
        foreach (var (kind, kindName, kindNameEng, color) in elites)
        {
            var a = ScriptableObject.CreateInstance<MobDef>();
            a.이름 = "정예_" + kindName; a.정예유형 = kind; a.AI = MobAi.추적;
            a.속도배율 = 0.6f; a.체력배율 = 3.5f; a.크기 = 3.2f;
            a.색조 = color; a.스프라이트 = LoadSprite("elite_healer_0");
            list.Add(Save(a, $"{ROOT}/Data/Mobs/Elite_{kindNameEng}.asset"));
        }
        return list;
    }

    static Color FamilyColor(MobFamily f) => f switch
    {
        MobFamily.야수 => new Color(0.85f, 0.55f, 0.35f),
        MobFamily.언데드 => new Color(0.65f, 0.75f, 0.65f),
        MobFamily.마족 => new Color(0.8f, 0.35f, 0.5f),
        MobFamily.기계 => new Color(0.6f, 0.65f, 0.7f),
        _ => new Color(0.5f, 0.8f, 1f),
    };

    // ── 전투 스타일 (§3) ──────────────────────────────────
    static void MakeStyles()
    {
        var rows = new (StyleId, float, float, float, float, string)[]
        {
            (StyleId.공격형, 1.15f, 1.20f, 0.00f, 0.9f, "회피 시도 안 함, 적극 교전"),
            (StyleId.균형형, 1.00f, 1.00f, 0.15f, 1.4f, "위험 패턴만 회피"),
            (StyleId.방어형, 0.90f, 0.85f, 0.30f, 2.6f, "안전거리 유지, HP 30%에 후퇴"),
            (StyleId.생존형, 0.70f, 0.75f, 0.50f, 4.0f, "HP 50%에 즉시 이탈·귀환"),
        };
        foreach (var r in rows)
        {
            var a = ScriptableObject.CreateInstance<CombatStyleDef>();
            a.Id = r.Item1; a.딜배율 = r.Item2; a.피해배율 = r.Item3;
            a.후퇴체력 = r.Item4; a.유지거리 = r.Item5; a.행동설명 = r.Item6;
            // ⚠️ **Resources 아래에 쓴다.** 예전에는 `Data/Styles/`에 만들었는데
            //    런타임은 `Resources/` 밖을 못 읽어서(이 저장소가 겪은 「Resources 밖 자산」 함정)
            //    전투 코드가 이 에셋을 영영 못 봤고, 결국 코드 상수 표가 진짜 소스가 돼 있었다.
            //    여기와 W3Party.Spec()이 같은 경로를 보게 해야 "인스펙터에서 고친다"가 성립한다.
            Save(a, $"Assets/Resources/styles/Style_{r.Item1}.asset");
        }
    }

    // ── 프리팹 ────────────────────────────────────────────
    static List<GameObject> MakePrefabs(List<JobDef> jobs, List<MobDef> mobs, Material mat)
    {
        var made = new List<GameObject>();

        // 직업명 → 영문명 매핑
        var jobNameMap = new Dictionary<string, string>
        {
            { "수호기사", "guardian" }, { "광전사", "berserker" }, { "검사", "swordsman" },
            { "궁수", "archer" }, { "마법사", "mage" }, { "소환사", "summoner" },
            { "사제", "priest" }, { "드루이드", "druid" }, { "음유시인", "bard" },
            { "주술사", "shaman" }, { "정령사", "elementalist" },
        };

        foreach (var j in jobs)
        {
            var engName = jobNameMap.ContainsKey(j.직업명) ? jobNameMap[j.직업명] : j.직업명;
            var go = new GameObject(j.직업명, typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = LoadPixelArt(j.역할,
                j.역할 == RoleId.탱 ? "player_knight_0"
                : j.역할 == RoleId.힐 ? "player_priest_0" : "player_mage_0");
            sr.sharedMaterial = mat;
            sr.sortingOrder = 500;
            // 픽셀아트는 PPU 32라 이미 적정 크기 — 플레이스홀더처럼 키우면 뭉개진다
            go.transform.localScale = Vector3.one;
            var path = $"{ROOT}/Prefabs/Characters/{engName}.prefab";
            made.Add(PrefabUtility.SaveAsPrefabAsset(go, path));
            Object.DestroyImmediate(go);
        }

        // 몬스터 프리팹 이름 매핑
        var mobNameMap = new Dictionary<string, string>
        {
            // beast, undead, demon, machine, elemental + chaser, swarmer, ranged, charger
            { "야수_추적형", "beast_chaser" }, { "야수_포위형", "beast_swarmer" },
            { "야수_원거리형", "beast_ranged" }, { "야수_돌진형", "beast_charger" },
            { "언데드_추적형", "undead_chaser" }, { "언데드_포위형", "undead_swarmer" },
            { "언데드_원거리형", "undead_ranged" }, { "언데드_돌진형", "undead_charger" },
            { "마족_추적형", "demon_chaser" }, { "마족_포위형", "demon_swarmer" },
            { "마족_원거리형", "demon_ranged" }, { "마족_돌진형", "demon_charger" },
            { "기계_추적형", "machine_chaser" }, { "기계_포위형", "machine_swarmer" },
            { "기계_원거리형", "machine_ranged" }, { "기계_돌진형", "machine_charger" },
            { "정령_추적형", "elemental_chaser" }, { "정령_포위형", "elemental_swarmer" },
            { "정령_원거리형", "elemental_ranged" }, { "정령_돌진형", "elemental_charger" },
            // elites
            { "정예_수호자", "elite_warden" }, { "정예_처형자", "elite_executioner" },
            { "정예_주술사", "elite_shaman" }, { "정예_군단장", "elite_legion_commander" },
            { "정예_저주술사", "elite_curser" }, { "정예_소환술사", "elite_summoner" },
        };

        foreach (var m in mobs)
        {
            var engName = mobNameMap.ContainsKey(m.이름) ? mobNameMap[m.이름] : m.이름;
            var go = new GameObject(m.이름, typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = m.스프라이트; sr.sharedMaterial = mat; sr.color = m.색조;
            go.transform.localScale = Vector3.one * m.크기;
            var path = $"{ROOT}/Prefabs/Mobs/{engName}.prefab";
            made.Add(PrefabUtility.SaveAsPrefabAsset(go, path));
            Object.DestroyImmediate(go);
        }

        // 투사체
        var pj = new GameObject("Projectile", typeof(SpriteRenderer));
        var psr = pj.GetComponent<SpriteRenderer>();
        psr.sprite = LoadSprite("boss_0"); psr.sharedMaterial = mat; psr.sortingOrder = 400;
        pj.transform.localScale = Vector3.one * 0.9f;
        made.Add(PrefabUtility.SaveAsPrefabAsset(pj, $"{ROOT}/Prefabs/FX/Projectile.prefab"));
        Object.DestroyImmediate(pj);

        return made;
    }

    // ── Sandbox 씬 — 열면 바로 보이는 씬 ──────────────────
    static void MakeSandboxScene(List<GameObject> prefabs, BalanceConfig balance)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0, 0, -20f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);

        // 바닥 — 노이즈맵
        var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ground.name = "Ground";
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.transform.localScale = new Vector3(60f, 60f * balance.ISO_Y, 1f);
        ground.transform.position = new Vector3(0, 0, 1f);
        var gtex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ground/field_plain_albedo.png");
        var gmat = new Material(Shader.Find("Unlit/Texture"));
        if (gtex != null) { gmat.mainTexture = gtex; gmat.mainTextureScale = new Vector2(10, 10); }
        AssetDatabase.CreateAsset(gmat, ROOT + "/Materials/Ground_Field.mat");
        ground.GetComponent<MeshRenderer>().sharedMaterial = gmat;

        // 파티 진형 — 탱 앞, 딜 중간, 힐·버퍼 뒤 (§10-4가 만들어내는 진형)
        var party = new GameObject("Party").transform;
        var formation = new (string, Vector2)[]
        {
            ("guardian", new Vector2(0f, 1.8f)),
            ("swordsman", new Vector2(-1.4f, -0.4f)),
            ("mage", new Vector2(1.4f, -0.4f)),
            ("priest", new Vector2(-0.9f, -2.6f)),
            ("bard", new Vector2(0.9f, -2.6f)),
        };
        foreach (var (name, pos) in formation)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{ROOT}/Prefabs/Characters/{name}.prefab");
            if (pf == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, party);
            inst.transform.position = new Vector3(pos.x, pos.y * balance.ISO_Y, 0f);
        }

        // 몹 샘플 — 계열별로 한 줄씩 늘어놓아 색·크기를 눈으로 비교
        var mobRoot = new GameObject("Mobs_Sample").transform;
        var mobPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { ROOT + "/Prefabs/Mobs" });
        int i = 0;
        foreach (var guid in mobPrefabs)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, mobRoot);
            int col = i % 8, row = i / 8;
            inst.transform.position = new Vector3(-14f + col * 4f, (7f + row * 2.4f) * balance.ISO_Y, 0f);
            i++;
        }

        var doc = new GameObject("_읽어보기");
        doc.AddComponent<SandboxNote>();

        Directory.CreateDirectory(ROOT + "/Scenes");
        EditorSceneManager.SaveScene(scene, ROOT + "/Scenes/Sandbox.unity");
    }
}
