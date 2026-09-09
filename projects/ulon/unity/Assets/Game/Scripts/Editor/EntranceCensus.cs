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

        /// <summary>
        /// **문 구멍이 소품에 가려졌나 — 센다**(검수 관찰 2026-09-09: 「기둥이 포털 판의 절반을 먹는다」).
        /// QA 입구 샷과 **같은 카메라 자리**(`Orbit` 8m/20°)에서 문구멍 표면 격자로 광선을 쏘아,
        /// 문틀·포털·게이트가 아닌 것이 막으면 가림으로 센다. 막은 것의 이름을 같이 적는다 —
        /// 이름이 없으면 무엇을 옮겨야 하는지 알 수 없다(왕관 사건에서 값을 치른 교훈).
        /// </summary>
        /// <summary>보스 두 샷의 방위 차를 찍기만 한다 — 판정 전에 「지금 얼마나 벌어져 있나」를 본다.</summary>
        public static void RunBearing()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            float gap = QaShots.BossShotBearingGap(out string report);
            Debug.Log("[샷] 보스 두 샷 방위 — " + report);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>문틀 조각들의 실제 치수 — 문구멍이 어디부터 어디까지인지 유도하려면 먼저 재야 한다.</summary>
        public static void RunFrame()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var root = GameObject.Find(Dungeon1.RootObject);
            if (root != null)
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    var r = tr.GetComponent<Renderer>();
                    if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                    bool inFrame = false;
                    for (var q = tr; q != null; q = q.parent)
                        if (q.name.StartsWith("Entrance") || q.name.StartsWith("Dungeon" ) || q.name.StartsWith("banner") || q.name.StartsWith("lantern"))
                        { inFrame = q.name != Dungeon1.RootObject; if (inFrame) break; }
                    if (inFrame)
                        Debug.Log("[문틀] " + PathOf(tr) +
                                  " 중심 " + r.bounds.center.ToString("0.00") + " 크기 " + r.bounds.size.ToString("0.00"));
                }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            for (var q = t.parent; q != null; q = q.parent) s = q.name + "/" + s;
            return s;
        }

        public static void RunMouth()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Mouth("07/D1", Dungeon1.RootObject, Dungeon1.EntranceX, Dungeon1.EntranceZ);
            Mouth("09/D2", Dungeon2.RootObject, Dungeon2.EntranceX, Dungeon2.EntranceZ);
            Mouth("11/D3", Dungeon3.RootObject, Dungeon3.EntranceX, Dungeon3.EntranceZ);
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static void Mouth(string tag, string rootName, float ex, float ez)
        {
            float share = MouthBlockShare(rootName, ex, ez, out string who, out float leak);
            Debug.Log("[입구] " + tag + " 문구멍 가림 **" + (share * 100f).ToString("0") + "%** · 바깥이 새는 " +
                      (leak * 100f).ToString("0") + "% · 막은 것:" + (who == "" ? " 없음" : who));
        }

        /// <summary>
        /// **문구멍이 가려진 비율**(0~1)과 막은 것의 이름 — 게이트와 셈이 **같은 함수**를 쓴다.
        /// 같은 판정이 두 곳에 살면 하나만 고쳐져 어긋난다(이 저장소가 여러 번 밟은 함정).
        /// </summary>
        public static float MouthBlockShare(string rootName, float ex, float ez, out string who)
            => MouthBlockShare(rootName, ex, ez, out who, out _);

        /// <summary>
        /// 문구멍이 **소품에 가려진 비율**과, 그 구멍으로 **바깥 세계가 새는 비율**을 함께 잰다.
        /// 「샘」은 구멍 격자를 지나 **끝까지** 쏜 광선의 첫 히트가 지형·마을이거나 아무것도 없는 경우다 —
        /// `qa_sky.py`가 파랑만 세어 0.10%를 냈을 때 실제로 새던 것은 하늘이 아니라 **초록 들판**이었다.
        /// **한 색만 세는 자는 다른 색으로 새는 것을 못 본다**(검수 원장 2026-09-09).
        /// </summary>
        public static float MouthBlockShare(string rootName, float ex, float ez, out string who, out float leak)
        {
            who = "";
            leak = 0f;
            var root = GameObject.Find(rootName);
            Renderer portal = null;
            Transform frame = null;
            if (root != null)
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name == VisualSliceBuilder.EntrancePortalObject && portal == null)
                        portal = tr.GetComponent<Renderer>();
                    if (tr.name == VisualSliceBuilder.EntranceFrameObject && frame == null)
                        frame = tr;
                }
            if (portal == null)
            {
                who = "(포털 없음)";
                return 0f;
            }
            int leaked = 0;
            // QA 샷과 같은 눈: `Orbit(대상, 8m, 20°)`.
            var hits = Physics.RaycastAll(new Vector3(ex, 500f, ez), Vector3.down, 1000f);
            float gy = float.MaxValue;
            for (int i = 0; i < hits.Length; i++) gy = Mathf.Min(gy, hits[i].point.y);
            var look = new Vector3(ex, gy + 1.2f, ez);
            float rad = 20f * Mathf.Deg2Rad;
            var eye = look + new Vector3(-8f * Mathf.Cos(rad), 8f * Mathf.Sin(rad) + 1.5f, -8f * Mathf.Cos(rad)) * 0.7071f;

            // **영역은 포털이 아니라 문틀에서 잡는다** — 포털 바운드로 격자를 만들면 자가 대상을 따라가서,
            // 판을 절반으로 줄여도 격자도 같이 줄어 「샘 0%」가 나온다(네거티브 컨트롤이 실제로 그렇게 실패했다).
            // 구멍은 **두 기둥 사이·상인방 아래**다 — 판이 그 구멍을 덮는지 물어야 판이 작아진 것을 본다.
            var b = portal.bounds;
            if (frame != null)
            {
                Transform q1 = null, q2 = null, ql = null;
                foreach (var tr in frame.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name == "EntrancePillar1") q1 = tr;
                    else if (tr.name == "EntrancePillar2") q2 = tr;
                    else if (tr.name == "EntranceLintel") ql = tr;
                }
                if (q1 != null && q2 != null)
                {
                    Bounds a1 = new Bounds(), a2 = new Bounds();
                    bool ok1 = false, ok2 = false;
                    foreach (var r in q1.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled && !(r is ParticleSystemRenderer)) { if (!ok1) { a1 = r.bounds; ok1 = true; } else a1.Encapsulate(r.bounds); }
                    foreach (var r in q2.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled && !(r is ParticleSystemRenderer)) { if (!ok2) { a2 = r.bounds; ok2 = true; } else a2.Encapsulate(r.bounds); }
                    if (ok1 && ok2)
                    {
                        var mouth = new Bounds(a1.center, Vector3.zero);
                        mouth.Encapsulate(a2.center);
                        float top = Mathf.Min(a1.max.y, a2.max.y);
                        if (ql != null)
                            foreach (var r in ql.GetComponentsInChildren<Renderer>(true))
                                if (r.enabled && !(r is ParticleSystemRenderer)) { top = r.bounds.min.y; break; }
                        float floorY = Mathf.Min(a1.min.y, a2.min.y);
                        mouth.Encapsulate(new Vector3(mouth.center.x, floorY, mouth.center.z));
                        mouth.Encapsulate(new Vector3(mouth.center.x, top, mouth.center.z));
                        // 판이 있는 평면으로 옮긴다(깊이는 얇게) — 격자는 그 면 위에서 만든다.
                        mouth.center = new Vector3(portal.bounds.center.x, mouth.center.y, portal.bounds.center.z);
                        b = mouth;
                    }
                }
            }
            int blocked = 0, total = 0;
            // **점 49개는 얇은 것을 놓친다**(검수 관찰 2026-09-09: 자는 0%인데 화면엔 붉은 선).
            // 배너는 폭 0.19m라 성긴 격자 사이로 그대로 빠졌다. 격자를 화면 실루엣만큼 촘촘히 한다.
            const int G = 48;
            var names = new System.Collections.Generic.Dictionary<string, int>();
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int gx = 0; gx < G; gx++)
                for (int gyi = 0; gyi < G; gyi++)
                {
                    var p = new Vector3(
                        Mathf.Lerp(b.min.x, b.max.x, (gx + 0.5f) / G),
                        Mathf.Lerp(b.min.y, b.max.y, (gyi + 0.5f) / G),
                        Mathf.Lerp(b.min.z, b.max.z, (gx + 0.5f) / G));
                    total++;
                    var seg = p - eye;
                    float len = seg.magnitude;
                    var ray = new Ray(eye, seg / len);
                    // **끝까지 쏴서 첫 히트를 본다** — 그것이 지형·마을이거나 아무것도 없으면 구멍이 샌다.
                    float firstD = float.MaxValue;
                    bool firstIsWorld = true;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var rr = all[i];
                        if (!rr.enabled || rr is ParticleSystemRenderer) continue;
                        if (!rr.bounds.IntersectRay(ray, out float dd) || dd >= firstD) continue;
                        var trr = rr.transform;
                        bool isDoor = trr == portal.transform || (frame != null && trr.IsChildOf(frame));
                        bool isWorld = trr.root.name == "Terrain" || trr.name == "Ground" ||
                                       !(isDoor || trr.root.name == rootName);
                        firstD = dd;
                        firstIsWorld = isWorld && !isDoor;
                    }
                    if (firstD == float.MaxValue || firstIsWorld)
                        leaked++;
                    foreach (var r in all)
                    {
                        if (!r.enabled || r is ParticleSystemRenderer) continue;
                        var tr = r.transform;
                        if (tr == portal.transform) continue;
                        if (frame != null && tr.IsChildOf(frame)) continue;      // 문틀 자신은 문이다
                        if (tr.root.name == "Terrain" || tr.name == "Ground") continue;
                        if (r.bounds.IntersectRay(ray, out float d) && d < len - 0.05f)
                        {
                            blocked++;
                            string nm = tr.parent != null ? tr.parent.name + "/" + r.name : r.name;
                            names[nm] = names.TryGetValue(nm, out int c) ? c + 1 : 1;
                            break;
                        }
                    }
                }
            foreach (var kv in names) who += " " + kv.Key + "×" + kv.Value;
            leak = total > 0 ? leaked / (float)total : 0f;
            return total > 0 ? blocked / (float)total : 0f;
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
