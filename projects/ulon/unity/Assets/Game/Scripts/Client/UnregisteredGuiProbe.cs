using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// **네거티브 컨트롤 전용** — OnGUI로 화면에 그리면서 `SliceHud.RegisterArea`를 부르지 않는 컴포넌트다.
    /// 등록 강제 게이트(`HudShots.CheckOwners`)가 이런 것을 실제로 잡는지 확인하려고 잠깐 붙였다 뗀다.
    /// 게임 코드가 이걸 씬에 남기면 안 된다 — 남으면 그 자체가 화면 규칙 위반으로 잡힌다.
    /// </summary>
    public sealed class UnregisteredGuiProbe : MonoBehaviour
    {
        void OnGUI()
        {
            GUI.Box(new Rect(4f, 4f, 120f, 24f), "probe");
        }
    }
}
