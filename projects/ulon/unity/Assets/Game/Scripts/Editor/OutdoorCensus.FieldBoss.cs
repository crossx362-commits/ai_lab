using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **「필드 보스」가 정말 들판에 있나 — 센다**(검수 관찰 2026-09-09: `06` 배경이 마을 집·시설이다).
        ///
        /// 이름과 자리 중 무엇을 고칠지는 나중 문제고, 먼저 **세계가 어떻게 생겼는지**를 읽는다
        /// (샷이 세계를 따라가야지 그 반대가 아니다 — 검수). 재는 것:
        ///   ① 보스의 **원장 좌표와 씬 실제 자리**(둘이 다르면 그 자체가 결함이다)
        ///   ② 마을 중심·각 지역 중심까지의 거리, 가드존 밖인지
        ///   ③ 반경 20m 안의 **마을 것**(집·시설) 개수
        ///   ④ 보스 둘레 여덟 방위에서 **마을이 화면에 얼마나 들어오나**(근접 프레이밍과 같은 거리·화각).
        ///      ④가 처방을 가른다: 마을이 안 들어오는 방위가 있으면 **카메라만 돌리면 되고**,
        ///      어느 방위에서도 들어오면 그건 **자리(또는 이름)의 문제**다.
        /// </summary>
        public static void RunFieldBossPlace()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            var boss = GameObject.Find(FieldBoss.Object);
            if (boss == null)
            {
                Debug.LogError("[필드보스] 보스를 씬에서 못 찾았습니다: " + FieldBoss.Object);
                if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(1);
                return;
            }
            var p = boss.transform.position;
            Debug.Log("[필드보스] 원장 (" + FieldBoss.X.ToString("0.0") + ", " + FieldBoss.Z.ToString("0.0") +
                      ") · 씬 (" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ") · 어긋남 " +
                      new Vector2(p.x - FieldBoss.X, p.z - FieldBoss.Z).magnitude.ToString("0.00") + "m");

            float toVillage = new Vector2(p.x, p.z).magnitude;      // 마을 중심은 원점이다
            string dists = "";
            foreach (var r in WorldRegions.All)
                dists += " · " + r.Name + " " + (new Vector2(p.x - r.X, p.z - r.Z).magnitude - r.Radius).ToString("0") + "m";
            Debug.Log("[필드보스] 마을 중심까지 " + toVillage.ToString("0.0") + "m · 지역 경계까지" + dists);

            // ③ 반경 20m 안의 마을 것.
            var village = GameObject.Find("VillageDecor");
            int near = 0;
            string names = "";
            if (village != null)
                foreach (var t in village.GetComponentsInChildren<Transform>(true))
                {
                    var rd = t.GetComponent<Renderer>();
                    if (rd == null || !rd.enabled) continue;
                    float d = new Vector2(rd.bounds.center.x - p.x, rd.bounds.center.z - p.z).magnitude;
                    if (d > 20f) continue;
                    near++;
                    if (near <= 5) names += " " + t.name + "(" + d.ToString("0") + "m)";
                }
            Debug.Log("[필드보스] 반경 20m 안 마을 조각 " + near + "개 —" + names);

            // ④ 여덟 방위에서 마을이 화면에 얼마나 들어오나(근접 프레이밍과 같은 눈높이·거리로 근사).
            if (village == null)
            {
                Debug.LogWarning("[필드보스] VillageDecor가 없어 방위별 배경을 못 잽니다.");
            }
            else
            {
                var look = p + Vector3.up * 1.2f;
                float best = 1f, bestYaw = 0f;
                string row = "";
                for (float yaw = 0f; yaw < 360f; yaw += 45f)
                {
                    var dir = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
                    var eye = look + dir * 6f + Vector3.up * 2.2f;
                    float share = (float)EntranceCensus.Draw(village.transform, eye, look).Pixels /
                                  (EntranceCensus.ScreenW * EntranceCensus.ScreenH);
                    row += " · " + yaw.ToString("0") + "° " + (share * 100f).ToString("0.0") + "%";
                    if (share < best) { best = share; bestYaw = yaw; }
                }
                Debug.Log("[필드보스] 방위별 배경 마을 몫" + row);
                Debug.Log("[필드보스] 마을이 가장 적게 드는 방위 " + bestYaw.ToString("0") + "° — " +
                          (best * 100f).ToString("0.0") + "%");
            }
            // ⑤ **보스는 어디를 보고 서 있나** — 정면을 찍으면 카메라는 보스가 보는 쪽에 선다.
            // 그러니 보스가 마을을 보고 있으면 「정면」과 「마을 없는 배경」은 **동시에 안 된다**
            // (첫 처방에서 실제로 그랬다: 배경을 마을 반대로 주니 보스가 등을 보였다).
            float bossYaw = boss.transform.eulerAngles.y;
            float villageYaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;      // 보스에서 마을(원점)을 보는 방위
            float delta = Mathf.Abs(Mathf.DeltaAngle(bossYaw, villageYaw));
            Debug.Log("[필드보스] 보스가 보는 방위 " + bossYaw.ToString("0") + "° · 마을 방위 " +
                      villageYaw.ToString("0") + "° · 차이 " + delta.ToString("0") +
                      "° (작을수록 「정면 = 마을 배경」이 겹친다)");
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
