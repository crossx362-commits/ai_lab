using UnityEngine;

// Unity는 MonoBehaviour마다 클래스명과 같은 이름의 .cs 파일을 요구한다.
// 이 컴포넌트가 ProjectSetup.cs / SceneStructureBuilder.cs 안에 들어 있었던 탓에
// 씬의 참조가 끊겨 m_ClassName만 남아 있었다. 또 Editor 폴더에 있어서
// Assembly-CSharp-Editor 소속이 되는 바람에 빌드에도 못 들어갔다.
// 그래서 Runtime으로 옮기고 파일을 나눈다 — 되돌리지 마라.

/// <summary>Sandbox 씬을 연 사람이 뭘 보고 있는지 알 수 있게 하는 안내판</summary>
public class SandboxNote : MonoBehaviour
{
    [TextArea(10, 20)]
    public string 안내 =
        "재와 별 — Sandbox 씬\n\n" +
        "여기 보이는 것:\n" +
        "  Party      표준 5인 진형 (탱 앞 / 딜 중간 / 힐·버퍼 뒤)\n" +
        "  Mobs_Sample 계열 5종 × AI 4종 + 정예 6유형을 늘어놓은 것\n" +
        "  Ground     블렌더에서 베이크한 seamless 노이즈 바닥\n\n" +
        "수치를 고치려면 (플레이 없이 인스펙터에서):\n" +
        "  Assets/_Game/Data/GameBalance.asset  — 경제 앵커·성능 예산\n" +
        "  Assets/_Game/Data/Jobs/              — 직업 11종 스탯·스킬\n" +
        "  Assets/_Game/Data/Mobs/              — 몬스터 속도·체력\n" +
        "  Assets/Resources/races/              — 종족 4종 기울기(런타임이 읽는다)\n" +
        "  Assets/Resources/styles/             — 전투 스타일 4종(런타임이 읽는다)\n\n" +
        "검증 씬은 따로 있다:\n" +
        "  Assets/Scenes/W1  성능(500체 60fps)\n" +
        "  Assets/Scenes/W2  조작감(대시 무적)\n" +
        "  Assets/Scenes/W3  파티 대조 실험\n\n" +
        "쿼터뷰 규칙: 월드 y에 ISO_Y(0.5)를 곱해 화면에 그린다.\n" +
        "블렌더 스프라이트를 30° 하강각으로 렌더했으므로 이 값과 맞아야 한다.";
}
