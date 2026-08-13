using UnityEngine;

// Unity는 MonoBehaviour마다 클래스명과 같은 이름의 .cs 파일을 요구한다.
// 이 컴포넌트가 ProjectSetup.cs / SceneStructureBuilder.cs 안에 들어 있었던 탓에
// 씬의 참조가 끊겨 m_ClassName만 남아 있었다. 또 Editor 폴더에 있어서
// Assembly-CSharp-Editor 소속이 되는 바람에 빌드에도 못 들어갔다.
// 그래서 Runtime으로 옮기고 파일을 나눈다 — 되돌리지 마라.

/// <summary>씬을 연 사람이 무엇을 보고 있는지 인스펙터에서 읽을 수 있게</summary>
public class SceneNote : MonoBehaviour
{
    public string 제목;
    public string 기획서;
    [TextArea(4, 14)] public string 설명;
}
