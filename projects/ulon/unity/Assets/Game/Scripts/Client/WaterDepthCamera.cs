using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// **수면이 제 요구를 들고 다닌다** — 이 물체가 씬에 있으면 모든 카메라가 깊이+법선 버퍼를 굽는다.
    ///
    /// 왜 카메라 쪽이 아니라 물 쪽인가: 카메라는 게임·QA 샷·셈(census) 세 갈래에서 각각 만들어지고,
    /// 그중 하나만 빠뜨려도 물이 **어디서나 깊은 색**으로 조용히 잘못 그려진다(깊이 텍스처가 없으면
    /// 셰이더는 「바닥이 무한히 멀다」로 읽는다 — 빨간불 없이 틀린다). 그래서 요구를 **물에 붙였다**.
    ///
    /// `OnWillRenderObject`가 아니라 `Camera.onPreCull`을 쓴다: 깊이 텍스처는 그 카메라의 렌더가
    /// 시작될 때 구워지므로, `cam.Render()`를 한 번만 부르는 배치 QA에서 `OnWillRenderObject`는
    /// **한 판 늦는다**(다음 프레임이 없으니 영영 안 온다).
    /// </summary>
    [ExecuteAlways]
    public class WaterDepthCamera : MonoBehaviour
    {
        void OnEnable()
        {
            Camera.onPreCull -= Ask;
            Camera.onPreCull += Ask;
        }

        void OnDisable()
        {
            Camera.onPreCull -= Ask;
        }

        static void Ask(Camera cam)
        {
            if (cam != null)
                cam.depthTextureMode |= DepthTextureMode.DepthNormals;
        }
    }
}
