using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesToStars
{
    /// <summary>
    /// 화면(씬) 사이 이동과 게임 종료를 한 곳에서 관리한다.
    ///
    /// 왜 이 방식인가:
    ///   이 프로젝트에는 uGUI 패키지(com.unity.ugui)가 없어 Button·Text를 못 쓰고,
    ///   TextMesh는 배치모드에서 폰트가 없어 크래시한 이력이 있다(SceneStructureBuilder 주석).
    ///   그래서 화면은 전부 **IMGUI(OnGUI)** 로 그린다 — W2·W3 검증 빌드가 이미 쓰는 방식이다.
    ///
    ///   씬 파일은 "카메라 + 화면 스크립트 1개"만 담은 껍데기로 유지한다.
    ///   과거 화면마다 무거운 씬을 11개 만들다 배치모드가 죽은 적이 있어(§21 기록),
    ///   씬을 가볍게 두고 내용은 런타임에 그리는 편이 안전하다.
    /// </summary>
    public static class GameFlow
    {
        // 씬 이름을 문자열로 흩뿌리면 오타가 런타임까지 안 잡힌다 — 한 곳에 모은다.
        public const string Title = "Title";
        public const string Estate = "Estate";        // 허브(§16 "허브는 영지")
        public const string Field = "Field";
        public const string Tower = "Tower";
        public const string WorldMap = "WorldMap";
        public const string Character = "Character";
        public const string Battle = "Battle";
        public const string Result = "Result";

        /// <summary>빌드 세팅에 등록할 순서. 0번이 시작 씬이 된다.</summary>
        public static readonly string[] All =
        {
            Title, Estate, Field, Tower, WorldMap, Character, Battle, Result,
        };

        /// <summary>§16 하단 고정바 5칸. 어떤 화면에서도 2번 클릭 안에 이동 가능해야 한다.</summary>
        public static readonly (string scene, string label)[] BottomBar =
        {
            (Estate, "영지"), (Field, "필드"), (Tower, "탑"), (WorldMap, "월드맵"), (Character, "캐릭터"),
        };

        /// <summary>직전 화면 — 전투·결과에서 돌아갈 곳을 기억한다.</summary>
        public static string ReturnTo { get; private set; } = Estate;

        /// <summary>마지막 전투 결과. Result 화면이 읽는다.</summary>
        public static string LastBattleSummary = "";

        public static void Go(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene))
            {
                SceneManager.LoadScene(scene);
                return;
            }
            // 씬이 빌드 세팅에 없으면 LoadScene은 조용히 실패하지 않고 예외를 던진다.
            // 원인을 명확히 남긴다 — "버튼을 눌렀는데 아무 일도 안 난다"로 보이면 진단이 오래 걸린다.
            Debug.LogError($"[GameFlow] 씬 '{scene}'이 빌드 세팅에 없다. " +
                           "재와별 ▸ 플레이 씬 생성 을 실행해 등록할 것");
        }

        /// <summary>전투로 들어가면서 돌아올 화면을 기억해 둔다.</summary>
        public static void GoBattle(string returnScene)
        {
            ReturnTo = returnScene;
            Go(Battle);
        }

        /// <summary>
        /// 게임 종료. 에디터에서는 Application.Quit이 아무 일도 하지 않으므로
        /// 플레이 모드를 직접 끈다 — 안 그러면 "종료가 안 된다"고 오진하게 된다.
        /// </summary>
        public static void Quit()
        {
            Debug.Log("[GameFlow] 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
