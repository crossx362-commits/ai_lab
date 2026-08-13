# 재와 별 — 개발 인수인계 (세션 이어가기용)

> **이 문서의 목적**: 대화 세션이 길어져 새 세션을 열 때, 이 파일 하나만 읽으면
> 하던 작업을 그대로 이어갈 수 있게 한다. 새 세션 첫 메시지에 이렇게 쓰면 된다:
>
> ```
> docs/GAME_DEV_HANDOFF.md 읽고 개발 루프 이어서 진행해
> ```
>
> 갱신 규칙: **작업을 하나 끝내고 커밋할 때마다 이 파일의 §3·§4를 함께 갱신한다.**
> 갱신 안 된 인수인계 문서는 없느니만 못하다.

---

## 1. 무엇을 만들고 있나

**재와 별 (Ashes to Stars)** — 스팀 PC용 2D 쿼터뷰 픽셀아트 게임.
뱀서라이크 전투 + 키우기 + 탑 100층 + 레이드 + 영지 + 비동기 PvP(침략).

핵심 정체성은 **"죽으면 캐릭터가 진짜 사라진다"** — 3회 사망 시 장비까지 영구 삭제,
환생석으로만 복구. 가챠 없음. 일일 퀘스트·숙제 없음.

| 문서 | 내용 |
|---|---|
| `docs/GAME_DESIGN_ASHES_TO_STARS.md` | **기획서 본문** — 23개 절. 모든 확정 사항의 출처 |
| `docs/GAME_ART_RESOURCES.md` | 아트 리소스 목록·물량 산정·최소화 방침 |
| `docs/GAME_DEV_HANDOFF.md` | 이 파일 |

---

## 2. 어디에 무엇이 있나

```
projects/ashes-to-stars/
  unity/                     유니티 프로젝트 (Unity 6000.0.36f1로 열 것)
    Assets/_Game/
      Data/                  ScriptableObject — 기획서 §18 수치가 여기 들어 있다
      Prefabs/               캐릭터 11 · 몬스터 26 · 투사체
      Art/Sprites/<직업>/    오너가 준 픽셀아트를 잘라낸 것 (32장)
      Scenes/                Sandbox · 게임구조_전체 ← 에디터에서 열어볼 씬
      Scripts/Runtime|Editor
    Assets/Scripts/          W1~W3 검증용 (StressTest·W2Arena·W3Party)
  blender/                   에셋 생성 파이프라인 (헤드리스)
    원본시트/                 오너가 준 시트 원본
    시트_분할.py              시트 → 프레임 단위 스프라이트
    생성_*.py                 캐릭터·바닥·이펙트·프랍 생성기
    출력_*/                   생성 결과
  빌드_W1성능/ W2조작/ W3파티/  스탠드얼론 빌드 (git 제외)
  실행결과/                   측정 CSV·스크린샷·로그 (git 제외)

projects/ai-team/skills/마루_게임개발/tools/
  game_build_verify.py       빌드→실행→렌더 검증
  game_regression.py         W1~W3 통합 회귀
  game_balance_sim.py        경제·성장 수치 검산
  game_kiting_sim.py         잡몹 속도비 시뮬레이션

output/qa/ashes-to-stars/    검증 리포트 (git 추적)
```

### 실행 명령 (검증된 것)

```bash
# 유니티 빌드 (배치모드) — Unity.exe 경로는 6000.0.36f1
"C:\Program Files\Unity\Hub\Editor\6000.0.36f1\Editor\Unity.exe" -batchmode -quit \
  -projectPath "D:\ai_lab\projects\ashes-to-stars\unity" \
  -executeMethod W1Runner.Build -logFile "...\실행결과\build.log"

# 블렌더 (반드시 --factory-startup — 설치된 애드온이 에러를 뿜는다)
"C:\Program Files\Blender Foundation\Blender 4.1\blender.exe" --background --factory-startup \
  --python "D:\ai_lab\projects\ashes-to-stars\blender\생성_프랍.py"
```

---

## 3. 지금까지 한 것 (완료)

| # | 작업 | 결과 |
|---|---|---|
| 1 | 기획서 v0.6 | 23개 절 · 악용 감사 3회(A20/B8/C8) 전건 대응 |
| 2 | W1 성능 검증 | **통과** — 500체 729fps (목표의 12배). DOTS 불필요 |
| 3 | W2 조작감 | 대시 무적 흡수 횟수로 계량화. 잡몹 속도 0.64→0.90 조정 후 포위 시간 90.8% |
| 4 | W3 파티 대조 실험 | 1인 불가·도발 효과 **검증**. 딜특화·힐러없음은 상한에 걸려 미검증 → 웨이브 점증으로 재측정 |
| 5 | 유니티 구조 | ScriptableObject·프리팹·Sandbox·게임구조_전체 씬 |
| 6 | 블렌더 파이프라인 | 캐릭터 64 · 바닥 4 · 이펙트 14 · 프랍 26 |
| 7 | 폴더·파일명 정리 | 뜻이 보이는 한글 이름으로, 참조 10개 파일 갱신 후 빌드 검증 |
| 8 | 오너 픽셀아트 반영 | 시트 3장 → 32프레임 분할 → 프리팹 연결 |

---

## 4. 다음에 할 것 (우선순위 순)

1. **W3 재측정 결과 문서화** — 웨이브 점증판으로 다섯 구성 전부 전멸시켰다.
   결과 CSV는 `실행결과/w3_result.csv`. 기획서 §21에 반영해야 함
2. **정합성 감사 지적 반영** — `W3Party.cs`의 포위형 0.80→**0.85**, 원거리형 0.70→**0.65**
   (기획서 §18-11과 어긋남. MobDef 에셋은 이미 올바름)
3. **방향별 스프라이트 도착 시 처리** — 오너가 "나중에 준다"고 함.
   규격은 `docs/GAME_ART_RESOURCES.md` §0-A의 「방향별 스프라이트 도착 시 처리」 표
4. **UI 골격** — 하단바 5칸·인벤토리·경매장 화면 (기획서 §16)
5. **수직 슬라이스 진입** — 기획서 §22 로드맵 1단계

---

## 5. 이 프로젝트에서 실제로 겪은 함정 (반복하지 말 것)

| 함정 | 무엇이 일어났나 | 어떻게 막나 |
|---|---|---|
| **빈 화면 700fps** | 텍스처 Read/Write 누락으로 아틀라스가 조용히 실패, 스프라이트를 하나도 안 그린 채 성능만 측정 | FPS와 **렌더 검증**(스크린샷의 유닛 픽셀 수가 몹 수에 비례하는지)을 짝지어 판정 |
| **낡은 빌드로 측정** | `W1.exe` 런처는 코드가 바뀌어도 타임스탬프가 안 변한다. 불가능한 수치(4초에 피격 10,508회)를 읽었다 | `*_Data/Managed/Assembly-CSharp.dll` 시각을 확인 |
| **유니티 크래시 오진** | 씬 생성이 두 번 죽어 TextMesh 탓으로 보고 멀쩡한 코드를 고쳤다. 진짜 원인은 **다른 Unity 인스턴스가 프로젝트를 잡고 있던 것** | 배치 실행 전 `taskkill /IM Unity.exe /F` + `Temp/UnityLockfile` 제거. 크래시는 스택을 먼저 읽을 것 |
| **runInBackground=false** | 창이 포커스를 잃자 Update가 멈춰 자동 봇 검증이 아예 안 돌았다 | 검증 빌드는 항상 `runInBackground = true` |
| **측정 상한에 걸린 결과** | W3 다섯 구성 중 넷이 45초를 완주해 생존 시간으로 구분 불가 | 상한에 닿은 결과는 통과가 아니라 **측정 실패**. 전멸까지 돌리고 압력을 점증 |
| **밝기로 배경 제거** | 시트 배경(밝기 13)보다 캐릭터 외곽선(0~4)이 더 어두워서, 임계값으로 지우니 캐릭터가 부서졌다 | 배경은 "어두운 색"이 아니라 **특정 색**이다. 색 비교 + 가장자리 flood fill |
| **대화 속 이미지** | 오너가 붙여넣은 이미지는 디스크에서 못 꺼낸다 | `Downloads` 등에서 파일을 찾거나, 저장 폴더를 안내할 것 |

---

## 6. 작업 방식 (오너 지시)

- **작업 하나 끝낼 때마다 커밋**하고, 다음 작업을 정한 뒤 실행한다. 이걸 반복한다
- 병렬 가능한 일은 **에이전트를 만들어 동시에** 돌린다
  (단 유니티는 프로젝트 락 때문에 **한 번에 하나만** — 에이전트에 유니티 실행 금지를 명시할 것)
- **항상 기획서를 참고**한다. 수치·규칙의 출처는 언제나 `GAME_DESIGN_ASHES_TO_STARS.md`
- 커밋·푸시는 master 직행. 푸시가 403이면
  `git config --local credential.https://github.com.username crossx362-commits` 후 `gh auth setup-git`
