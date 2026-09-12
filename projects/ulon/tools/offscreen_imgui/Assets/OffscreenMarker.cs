using UnityEngine;

// 진단 전용. 실제 HUD 캡처에서는 같은 OnGUI 문맥에서 실제 그리기 함수를 호출한다.
[ExecuteAlways]
public sealed class OffscreenMarker : MonoBehaviour
{
    public static int Calls, Repaints;
    public static bool HideText;
    void OnGUI()
    {
        Calls++;
        if (Event.current.type == EventType.Repaint) Repaints++;
        GL.LoadPixelMatrix(0, Screen.width, 0, Screen.height);
        GUI.Box(new Rect(20, 20, 300, 100), HideText ? "" : "OFFSCREEN IMGUI");
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(Screen.width - 60, 20, 40, 40), Texture2D.whiteTexture);
        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(20, Screen.height - 60, 40, 40), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
