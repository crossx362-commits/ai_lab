# SESSION HANDOFF — tankfall (2026-09-15)

## 현재 상태
- 맵 3종 Sim: `MapHeightFunction` + `SdfVolume` 해석 기울기 master.
- Unity 빌드: `./projects/tank-artillery/tools/unity_build.sh` → `unity/Build/Tankfall.exe`.
  메서드: `Tankfall.EditorTools.BuildScript.BuildWindows`.
- BattleDemo Play 맵 연결은 아직(파일 63KB 업로드 한도). Play 는 기존 언덕.

## 빌드
```bash
cd projects/tank-artillery
./tools/unity_build.sh          # 배치 빌드
./tools/unity_build.sh run      # 빌드 + -autoshot
```
Windows 직접:
```
"C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath D:\ai_lab\projects\tankfall\unity -executeMethod Tankfall.EditorTools.BuildScript.BuildWindows -logFile D:\ai_lab\projects\tankfall\tools\.unity_build.log
```
