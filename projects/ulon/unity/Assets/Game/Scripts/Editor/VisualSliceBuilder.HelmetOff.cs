using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// **보스의 투구를 걷는다 — 왕관은 남긴다**(검수 판정 2026-09-09, 화면 랩 1).
        ///
        /// 근접 샷에서 보스 얼굴은 **눈이 하나만** 나온다. 셈으로 후보를 하나씩 지웠다:
        /// 투구는 몸 정면과 0°·좌우 중심차 0.00m로 **비뚤지 않고**, 왕관 밴드는 12개 모두
        /// **얼굴 높이 아래로 안 내려오며**, 원근을 없앤 직교 판에서도 눈은 하나였다.
        /// 남은 것은 **투구 메시의 얼굴 구멍**이고, 그건 자리·크기로 못 고친다.
        ///
        /// 그래서 **가리는 쪽을 걷는다**: 「왕」을 말하는 것은 왕관이지 투구가 아니다 —
        /// 맨머리 + 왕관이면 얼굴이 온전히 보이고 보스 표식도 남는다(검수 판정).
        /// 머리 렌더러가 **있는** 보스만 손댄다(맨머리가 될 수 있는 것만) — 없는 것은 못 댄 수로 적는다.
        ///
        /// **왕관을 씌우는 그 함수(`DressBoss`) 안에서 걷는다** — 별도 패스로 맨 뒤에 두었더니
        /// 왕관은 **투구를 쓴 머리**를 재서 크기를 잡은 뒤였고, 투구만 사라져 왕관이 상대적으로
        /// 커진 채 남아 실루엣 게이트가 빨간불을 냈다(「모자 폭 1.42m가 몸 폭의 0.7배 초과」).
        /// **재는 쪽과 붙이는 쪽이 같은 함수여야 한다** — 걷는 것도 그 함수 안이어야 한다.
        ///
        /// NC: `ULON_HELMET_NC=1` — 투구를 그대로 둔다. 그러면 눈이 다시 하나로 돌아와야 한다.
        /// **NC는 로그에서 걸린 줄을 세어 확인한다**(스위치만 켜고 안 닿은 판의 「차이 없음」은 증거가 아니다).
        /// </summary>
        public static bool HelmetOffFor(GameObject boss)
        {
            bool nc = System.Environment.GetEnvironmentVariable("ULON_HELMET_NC") == "1";
            Renderer head = null, helm = null;
            foreach (var r in boss.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (head == null && r.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0) head = r;
                if (helm == null && r.name.IndexOf("Helmet", System.StringComparison.OrdinalIgnoreCase) >= 0) helm = r;
            }
            if (nc)
            {
                Debug.Log("[Ulon] 보스 투구 **NC — 그대로 둔다**(정상판 아님) · " + boss.name +
                          " · 걷을 수 있었던 것 " + (helm != null && head != null ? 1 : 0) + "개");
                return false;
            }
            if (helm == null)
            {
                Debug.Log("[Ulon] 보스 투구 — " + boss.name + " 못 댐(투구 렌더러 없음, 「끌 것이 없다」≠「안 걸렸다」)");
                return false;
            }
            if (head == null)
            {
                Debug.Log("[Ulon] 보스 투구 — " + boss.name + " 못 댐(머리 렌더러가 없어 걷으면 맨목이 된다)");
                return false;
            }
            // **걷어도 왕관이 머리 위에 남는 보스만 걷는다**(실측 2026-09-09).
            // 본워든의 투구는 두개골보다 높다 — 걷으면 왕관이 **어깨 높이로 내려앉아** 「왕관 최저점은
            // 몸 높이의 0.8배 위」 게이트가 문다(화면에서도 목걸이처럼 읽힌다). 그러니 **걷어 보고 재서**
            // 미달이면 되켠다. 재는 자는 왕관을 앉히는 자와 **같은 것**(`BossFit.HeadMetrics`)이어야 한다 —
            // 렌더러 바운즈로 재면 스킨드 바운드가 부풀어 「1.72m 넘는다」고 나오고 왕관은 1.38m에 앉는다
            // (실제로 그렇게 한 번 놓쳤다). 게이트를 낮추는 대신 **대상을 가른다** — 자는 판정 사항이다.
            var cc = boss.GetComponent<CharacterController>();
            float bodyH = cc != null && cc.height > 0.01f ? cc.height : 0f;
            float footY = boss.transform.position.y;
            helm.enabled = false;
            if (bodyH > 0.01f && BossFit.HeadMetrics(boss, out float bareTop, out float _, out Vector3 _c)
                && bareTop - footY < bodyH * 0.8f)
            {
                helm.enabled = true;
                Debug.Log("[Ulon] 보스 투구 — " + boss.name + " 못 댐(맨머리 정수리가 발끝에서 " +
                          (bareTop - footY).ToString("0.00") + "m로 몸 " + bodyH.ToString("0.00") +
                          "m의 0.8배에 못 미친다 — 걷으면 왕관이 어깨로 내려앉는다)");
                return false;
            }
            Debug.Log("[Ulon] 보스 투구 걷음 — " + boss.name + " · " + helm.name + " 끔, 맨머리 " + head.name +
                      " 폭 " + head.bounds.size.x.ToString("0.00") + "m (왕관은 이 폭에서 유도된다)");
            return true;
        }
    }
}
