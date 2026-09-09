using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **던전 입구에 「지하로 내려간다」가 없다 — 세기만 한다**(검수 화면 랩 2, 2026-09-09).
    ///
    /// `07_d1_entrance`는 **평평한 잔디밭에 선 문틀**이다. 아래로 파인 자리도, 계단도, 그늘도 없어
    /// 「지하 입구」가 아니라 「들판의 기념문」으로 읽힌다. 고치기 전에 **무엇이 없는지를 센다**:
    ///   ① 문 둘레의 **고도차** — 고리(2·4·6·8m)마다 지표 최저·최고. 전부 평평하면 파인 자리가 없다.
    ///   ② 문구멍 **뒤쪽 지표** — 문 안으로 한 걸음 들어간 자리가 문 앞보다 낮은가(내려가는가).
    ///   ③ 둘레 **소품 수**(반경 8m 렌더러) — 던전 톤(바위·잔해)과 마을 톤(집·울타리)을 갈라 센다.
    /// 세 던전을 같이 재서 **D1만 그런지**를 가른다.
    ///
    /// **첫 판에서 자가 두 번 틀렸다**(자를 세울 땐 그 자의 구멍도 적는다):
    ///   ① 첫 히트를 지면으로 삼아 **문틀·상인방**을 맞고 「중심만 3.92m 솟았다」가 나왔다.
    ///   ② 가장 낮은 히트로 바꾸니 이번엔 **지하 방 바닥**을 맞아 4m 고리가 5.4m차로 뜬다.
    ///      (그 값은 세계가 아니라 지하 구조다 — 지면은 세 입구 모두 10.00m 평지다.)
    ///
    /// **결과**(2026-09-09): 세 입구 모두 지면 **10.00m 완전 평지**, **문 안팎 낙차 0.00m**,
    /// 반경 8m의 던전 톤 소품은 **잔해 하나**. 그래서 문 너머로 마을 지붕과 하늘이 그대로 보인다.
    ///
    /// **이 자가 못 보는 것**: 빛과 그림자는 안 잰다 — 화면(`qa_shots`)이 판정한다.
    /// 지표는 `WorldTerrain`의 하이트맵이 아니라 **씬의 실제 지형**을 물리로 찍는다(빌더가 뒤에 손댔을 수 있다).
    /// </summary>
    public static class EntranceCensus
    {
        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            One("D1", Dungeon1.EntranceX, Dungeon1.EntranceZ, 90f);
            One("D2", Dungeon2.EntranceX, Dungeon2.EntranceZ, -90f);
            One("D3", Dungeon3.EntranceX, Dungeon3.EntranceZ, 45f);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>지표를 위에서 쏘아 찍는다 — 못 맞으면 하이트맵 값으로 물러선다(그 사실을 적는다).</summary>
        static float GroundAt(float x, float z, out bool hit)
        {
            // **가장 낮은 히트가 지면이다** — 첫 히트를 쓰면 문틀·상인방을 맞고 「지표 13.92m」가 나온다
            // (첫 판에서 실제로 그랬다: 중심만 3.92m 솟은 것처럼 보였는데 그건 세계가 아니라 자의 구멍이었다).
            var hits = Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f);
            hit = hits.Length > 0;
            if (!hit)
                return WorldTerrain.HeightAt(x, z);
            float lowest = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
                lowest = Mathf.Min(lowest, hits[i].point.y);
            return lowest;
        }

        static void One(string tag, float ex, float ez, float approachYaw)
        {
            float rad = approachYaw * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));   // 안쪽(문 뒤)
            float center = GroundAt(ex, ez, out bool centerHit);
            string rings = "";
            foreach (float r in new[] { 2f, 4f, 6f, 8f })
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * (360f / 16f) * Mathf.Deg2Rad;
                    float y = GroundAt(ex + Mathf.Cos(a) * r, ez + Mathf.Sin(a) * r, out _);
                    lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                }
                rings += " · " + r.ToString("0") + "m 고리 " + (hi - lo).ToString("0.00") + "m차(중심 대비 " +
                         ((lo + hi) * 0.5f - center).ToString("+0.00;-0.00") + ")";
            }
            // 문 안쪽 한 걸음 — 내려가야 지하다
            float inner = GroundAt(ex + fwd.x * 2f, ez + fwd.z * 2f, out _);
            float outer = GroundAt(ex - fwd.x * 2f, ez - fwd.z * 2f, out _);

            int dungeonTone = 0, villageTone = 0, other = 0;
            string otherNames = "";
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer) continue;
                var p = r.bounds.center;
                if ((new Vector2(p.x - ex, p.z - ez)).sqrMagnitude > 64f) continue;
                string n = r.name;
                if (n.StartsWith("Rubble") || n.StartsWith("Rock") || n.StartsWith("Entrance") || n.Contains("pillar") || n.Contains("Pillar"))
                    dungeonTone++;
                else if (n.Contains("Wall") || n.Contains("Roof") || n.Contains("fence") || n.Contains("Fence") || n.Contains("House"))
                    villageTone++;
                else { other++; if (otherNames.Length < 400) otherNames += " " + n; }
            }
            Debug.Log("[입구] " + tag + " (" + ex.ToString("0.0") + "," + ez.ToString("0.0") + ") 지표 " +
                      center.ToString("0.00") + (centerHit ? "" : "(하이트맵 폴백)") + rings +
                      " · 문 안 " + inner.ToString("0.00") + "m vs 문 앞 " + outer.ToString("0.00") + "m → **" +
                      (inner - outer).ToString("+0.00;-0.00") + "m** · 반경 8m 렌더러 던전톤 " + dungeonTone +
                      "/마을톤 " + villageTone + "/기타 " + other + " —" + otherNames);
        }
    }
}
