using System;

namespace Ulon.Editor
{
    /// <summary>
    /// **역할 ↔ 외형 원장**(검수 지시 2026-09-07: 「표를 게이트로 만들어라」).
    ///
    /// `docs/ROLE_LOOK_TABLE.md`는 오늘은 맞지만 내일 새 시설이 좌판으로 들어오면 아무도 모른다.
    /// 그래서 표의 「무엇처럼 보여야 하나」를 **코드가 강제하는 원장**으로 옮긴다.
    /// 감사 도구 `RoleLookAudit`은 그대로 두되(사람이 읽는 표의 근거), 판정은 여기서 한다.
    ///
    /// 원장에 없는 역할 오브젝트가 씬에 서면 **실패**한다 — 그래야 새 시설이 조용히 들어오지 못한다.
    /// 요구는 검수가 못박은 셋이다:
    ///   (a) 시설끼리 **주 메시 중복 0** — 지금 상점과 대장간이 같은 `stall.fbx`라 화면에서 구분이 안 된다.
    ///   (b) **높이 하한을 플레이어 키 비율로** — 40cm·20cm 좌판은 「대장간」이 될 수 없다(§8.1 실루엣).
    ///   (c) 기능이 읽히는 **부속 최소 1개** — 등불이 화덕 노릇을 하는 건 안 된다.
    ///
    /// 높이 비율은 내가 정한 값이고 기준은 이렇다(§8.1 「멀리서도 무엇인지 즉시 읽히는 실루엣」):
    ///   건물(은행)은 사람이 **들어가야** 하므로 키의 1.5배, 작업 시설은 사람이 **서서 쓰는** 물건이라
    ///   허리(0.5배) 아래로는 못 내려간다. 표식(집터)은 사람 눈에 걸리기만 하면 되므로 0.5배.
    ///   실측 대비: 지금 상점 0.22배·목공소 0.11배·절구 0.11배·치유소 0.17배다(전부 하한 아래).
    /// </summary>
    public static class RoleLook
    {
        /// <summary>
        /// **사람이 들어가는 역할**에만 걸리는 세 축(검수 2026-09-07: 「높이 3.1m로 통과하는데 풍차
        /// 날개다」 — 한 축만 재면 다른 축으로 빠져나간다).
        ///   (i)  수평 최소 두께 — 얇은 판은 건물이 아니다(입구 문틀에서 쓴 축 그대로).
        ///   (ii) 렌더러 1개면 실패 — 건물은 벽·지붕·문이 조립된 것이다.
        ///   (iii) **안에 사람이 설 자리** — 이게 「들어갈 수 있다」의 정의다. 물리로 잰다.
        /// 두께 1.5m는 사람 캡슐(지름 0.6m)이 서고 양쪽에 벽이 남는 최소치다.
        /// </summary>
        public const float EnterableThickMin = 1.5f;
        public const int EnterableRendererMin = 2;

        public struct Facility
        {
            public string Object;          // 씬 오브젝트 이름
            public string Role;            // 역할(사람이 읽는 이름)
            public float MinHeightFrac;    // 플레이어 키 대비 높이 하한
            public string[] PartMeshes;    // 기능이 읽히는 부속 — 이 중 하나라도 붙어 있어야 한다
            public string PartWhy;         // 그 부속이 무엇을 읽히게 하는가
            public bool MustBePerson;      // 표시명이 사람인 역할(§18.19) — 사람 모델이어야 한다
            public bool Enterable;         // 사람이 **들어가는** 역할 — 높이만으로는 못 잰다(검수 2026-09-07)
            public bool ServiceDesk;       // 플레이어가 **말을 걸어 서비스를 받는** 자리(§18.19) — 상대가 서 있어야 한다
        }

        /// <summary>
        /// **표시명이 사람을 가리키는가**(검수 랩 ② 「사람 자격 게이트를 표시명이 사람인 역할 전수로」).
        ///
        /// 원장 플래그만으로는 목록이라 새 시설이 조용히 빠져나간다 — 그래서 씬에 적힌 **표시명 문자열**을
        /// 직접 읽어 사람 접미사로 끝나면 사람을 요구한다. 「치유사」·「훈련사」·「마구간지기」·「은행원」·
        /// 「상인」이 여기에 걸린다. 장소 이름(「은행」·「잡화」·「대장간」·「주택 부지」)은 안 걸리므로,
        /// 그쪽은 원장의 `ServiceDesk`가 따로 요구한다(둘은 서로의 사각지대를 메운다).
        ///
        /// **한계(공개)**: 등록 접미사로 끝나지 않는 사람 이름 — 예컨대 「목수」 — 은 이 규칙이 못 잡는다.
        /// 「수」를 넣으면 「분수」 같은 물건 이름까지 사람으로 읽히므로 넣지 않았다. 그런 역할이 생기면
        /// 원장에 `MustBePerson`으로 적어라(규칙과 원장 둘 다 있는 이유가 이것이다).
        /// </summary>
        public static bool NamesAPerson(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return false;
            string s = displayName.Trim();
            string[] suffixes = { "지기", "사", "원", "인", "꾼", "장이" };
            for (int i = 0; i < suffixes.Length; i++)
                if (s.Length > suffixes[i].Length && s.EndsWith(suffixes[i], StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// 원장. **여기 없는 역할 오브젝트는 씬에 설 수 없다.**
        /// `PartMeshes`에 적힌 파일이 저장소에 아직 없으면 그 행은 빨간불로 남는다 — 그게 정상이다
        /// (검수: 「저장소 조각으로 안 되면 고치지 말고 목록 보고」).
        /// </summary>
        public static readonly Facility[] Facilities =
        {
            new Facility { Object = "Banker", Role = "은행", MinHeightFrac = 1.5f, Enterable = true, ServiceDesk = true,
                PartMeshes = new[] { "wall-door.fbx", "wall-window-glass.fbx", "wall-window-shutters.fbx" },
                PartWhy = "문·창이 붙어야 「들어갈 수 있는 건물」로 읽힌다(§8.2)" },
            // 은행·상점은 표시명이 「은행」·「잡화」라 접미사 규칙에 안 걸린다 — 그런데 §18.19가 말하는
            // 마을 서비스는 **말을 거는 상대**다(검수: 「은행원·상인이 좌판인 것이 위반의 본체」).
            // 제작대(대장간·목공소·절구·화덕)는 플레이어가 **직접 쓰는 도구 자리**라 상대를 요구하지 않는다.
            new Facility { Object = "Vendor", Role = "상점", MinHeightFrac = 0.5f, ServiceDesk = true,
                PartMeshes = new[] { "stall-green.fbx", "stall-red.fbx", "banner-red.fbx", "crates_stacked.obj", "box_large.obj" },
                PartWhy = "차양·쌓인 물건이 있어야 「파는 곳」으로 읽힌다" },
            new Facility { Object = "Forge", Role = "대장간", MinHeightFrac = 0.5f,
                PartMeshes = new[] { "anvil.fbx", "forge.fbx", "chimney.fbx" },
                PartWhy = "모루·화로·굴뚝이 있어야 상점과 갈린다(지금 둘 다 stall.fbx)" },
            new Facility { Object = "Carpenter", Role = "목공소", MinHeightFrac = 0.5f,
                PartMeshes = new[] { "planks.fbx", "table_medium_broken.obj", "stairs-wood.fbx" },
                PartWhy = "목재·작업대가 있어야 「나무 다루는 곳」으로 읽힌다" },
            new Facility { Object = "Campfire", Role = "화덕", MinHeightFrac = 0.3f,
                PartMeshes = new[] { "campfire.fbx", "rubble_half.obj", "rock-small.fbx" },
                PartWhy = "불과 둘러싼 돌이 있어야 화덕이다 — 등불이 화덕 노릇 하는 건 안 된다(검수)" },
            new Facility { Object = "Mortar", Role = "절구", MinHeightFrac = 0.5f,
                PartMeshes = new[] { "barrel_small.obj", "box_small.obj", "table_medium_broken.obj" },
                PartWhy = "약병·통이 있어야 연금 자리로 읽힌다(지금은 걸상 하나)" },
            new Facility { Object = "Trainer", Role = "훈련소", MinHeightFrac = 0.9f, MustBePerson = true,
                PartMeshes = new[] { "sword_1handed.fbx", "banner-red.fbx" },
                PartWhy = "가르치는 기술이 실루엣에 보여야 한다(지금은 마법사 차림이 모든 기술을 가르친다)" },
            new Facility { Object = "Healer", Role = "치유소", MinHeightFrac = 0.9f, MustBePerson = true,
                PartMeshes = new[] { "fountain-round.fbx" },
                PartWhy = "표시명이 「치유사」인데 화면엔 분수만 있다 — 사람이 서 있어야 한다(§18.19)" },
            new Facility { Object = "Stable", Role = "마구간", MinHeightFrac = 0.9f, MustBePerson = true,
                PartMeshes = new[] { "fence.fbx", "fence-gate.fbx", "poles.fbx" },
                PartWhy = "표시명이 「마구간지기」다 — 축사(울타리)와 지기(사람)가 같이 있어야 한다" },
            new Facility { Object = "HousePlotStation", Role = "집터", MinHeightFrac = 0.5f,
                PartMeshes = new[] { "poles.fbx", "banner-red.fbx", "fence.fbx" },
                PartWhy = "「여기에 집을 지을 수 있다」가 읽히는 표식" },
            // 낚시터는 `ResourceNode`지만 마을 시설과 같은 결함(물레방아가 낚시터 노릇)이라 같은 원장에 둔다.
            // 차폐 랩에서 잡은 마을 최악 13.3%의 진짜 원인이 이 행이었다(검수 2026-09-07).
            new Facility { Object = "FishingSpot", Role = "낚시터", MinHeightFrac = 0.3f,
                PartMeshes = new[] { "planks.fbx", "poles.fbx", "cart.fbx" },
                PartWhy = "물가 발판·낚싯대가 있어야 낚시 자리로 읽힌다(지금은 물레방아)" },
        };

        public static bool TryGet(string obj, out Facility f)
        {
            for (int i = 0; i < Facilities.Length; i++)
                if (string.Equals(Facilities[i].Object, obj, StringComparison.Ordinal))
                {
                    f = Facilities[i];
                    return true;
                }
            f = default;
            return false;
        }
    }
}
