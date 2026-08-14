# 재와 별(Ashes to Stars) — Unity 파티클 시스템 저작 스펙

> v0.1 | 2026-08-14
> 상위 문서: `GAME_ART_RESOURCES.md` §0-A(픽셀아트 확정) — **충돌 시 아트문서가 이긴다.**
> 형제 문서: `GAME_SPEC_VFX.md`(기법 선택표) — **이 문서는 그 §2 표의 7번 "파티클 시스템" 칸을 확대한 것이다.** 뒤집는 항목은 §9에 전부 명시했다.
> 대상 코드: `unity/Assets/_Game/Scripts/Runtime/FxParticles.cs` · `unity/Assets/Scripts/W3Party.cs`
> 검증 환경: **Unity 6000.3.14f1 / 빌트인 렌더 파이프라인**(`ProjectSettings/GraphicsSettings.asset`의 `m_CustomRenderPipeline: {fileID: 0}`, `m_DefaultRenderingPath: 1`=Forward, `Packages/manifest.json`에 URP·HDRP·post-processing **없음** — 실측 확인)

---

## 0. 이 문서가 정하는 것 (3줄)

1. **파티클은 코드로만 만든다.** 프리팹·머티리얼 에셋을 늘리지 않는다(`FxParticles.cs`의 기존 판단 유지).
2. **`Lights` 모듈은 이 게임에서 쓸모가 없다.** 이 프로젝트의 스프라이트는 전부 `Sprites/Default`이고 그 셰이더는 `Lighting Off`다 — 실제 광원을 아무리 붙여도 화면에 변화가 0이다. "빛나 보이게"는 **Additive 블렌딩 + 광륜 레이어**로 만든다(§3).
3. **동시 시스템 24개 = 드로우콜 최대 24.** 빌트인은 파티클 시스템마다 드로우콜을 하나씩 낸다 — 500체 화면에서 이 상한을 늘리지 마라(§7).

---

## 1. 자료 근거 표

### 1-1. 검증된 1차 자료 (전부 URL 200 확인 또는 로컬 실측)

| # | 출처 | 무엇을 증명하는가 | URL |
|---|---|---|---|
| P1 | Unity Manual — Main module | `startLifetime`·`startSpeed`·`startSize`·`startRotation`·`gravityModifier`·`simulationSpace`·`maxParticles`·`Culling Mode`·`Ring Buffer Mode`·`Stop Action`의 정의. `Gravity Modifier`는 **0이면 중력 비활성**. `Culling Mode`가 화면 밖 시뮬레이션 중단(Pause/Always Simulate)을 정한다 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysMainModule.html |
| P2 | Unity Manual — Emission module | `Rate over Time`/`Rate over Distance`/`Bursts`. 이 게임은 **버스트 전용**(rate=0) | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysEmissionModule.html |
| P3 | Unity Manual — Shape module | 방출 형상(Sphere/Circle/Cone/Box/Edge…)·`radius`·`radiusThickness`·`arc` | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysShapeModule.html |
| P4 | Unity Manual — Velocity over Lifetime | 수명 동안 속도를 직접 준다. `space`(Local/World)로 축 기준을 고른다 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysVelOverLifeModule.html |
| P5 | Unity Manual — Limit Velocity over Lifetime | 속도 상한 + `Dampen`(감속). 스파크가 "튀었다가 멎는" 감을 만드는 유일한 표준 손잡이 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysLimitVelOverLifeModule.html |
| P6 | Unity Manual — Force over Lifetime | 가속도(바람·상승기류). `gravityModifier`와 달리 축별 커브 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysForceOverLifeModule.html |
| P7 | Unity Manual — Color over Lifetime | 수명 그라디언트. **알파 페이드의 정석 위치** | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysColorOverLifeModule.html |
| P8 | Unity Manual — Size over Lifetime | 수명 크기 커브 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysSizeOverLifeModule.html |
| P9 | Unity Manual — Rotation over Lifetime | 수명 회전 — ⚠️ **이 게임에서 금지**(§6) | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysRotOverLifeModule.html |
| P10 | Unity Manual — Noise module | 난류. 문서 명시: **"Lower quality settings reduce the performance cost significantly"**, `Octaves`는 **"significantly adds to the performance cost"** | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysNoiseModule.html |
| P11 | Unity Manual — Trails module | `Particles`/`Ribbon` 두 모드, `Ratio`·`Lifetime`·`Minimum Vertex Distance`·`Texture Mode`. ⚠️ 문서가 트레일 전용 머티리얼 필요 여부를 명시하지 않는다(§1-2) | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysTrailsModule.html |
| P12 | Unity Manual — Sub Emitters module | 입자의 birth/collision/death 시점에 다른 시스템을 트리거 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysSubEmitModule.html |
| P13 | Unity Manual — Texture Sheet Animation module | 그리드 또는 **Sprites 목록**으로 플립북. `Frame over Time` 커브, `Start Frame`으로 개체별 위상 랜덤화 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysTexSheetAnimModule.html |
| P14 | Unity Manual — Lights module | 입자에 **실제 Light 컴포넌트**를 붙인다. `Ratio`·`Use Particle Color`·`Size Affects Range`·`Alpha Affects Intensity`·`Maximum Lights`. `Maximum Lights`는 문서 표현대로 "엄청난 수의 라이트를 실수로 만들어 에디터가 멈추는" 사고 방지용 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysLightsModule.html |
| P15 | Unity Manual — Custom Data module | 셰이더 vertex stream으로 넘길 커스텀 값. ⚠️ **커스텀 셰이더가 있어야 의미가 있다** — 이 프로젝트엔 없다 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysCustomDataModule.html |
| P16 | Unity Manual — Renderer module | Render Mode 6종(Billboard / Stretched / Horizontal / Vertical / Mesh / **None**), Sort Mode 5종(None / By Distance / Oldest in Front / Youngest in Front / By Depth), `Sorting Fudge` 정의: **"Lower values increase the relative chance that Unity draws Particle Systems over other transparent GameObjects."**, `Pivot`, `Sorting Layer`/`Order in Layer`, `Min/Max Particle Size`(빌보드 전용), `Flip` | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysRendererModule.html |
| P17 | Unity Manual — Post-processing 개요 | **"The Built-in Render Pipeline does not include a post-processing solution by default. To use post-processing effects with the Built-in Render Pipeline, download the Post-Processing Version 2 package."** → **블룸은 패키지 추가 없이는 불가능** | https://docs.unity3d.com/6000.3/Documentation/Manual/PostProcessingOverview.html |
| P18 | Unity ScriptRef — `Shader.Find` | 아무것도 참조하지 않는 셰이더는 빌드에서 빠지고 **"Shader.Find will work only in the Editor, and will result in the pink error shader in a build."** 포함 방법 3가지: 씬의 머티리얼이 참조 / `ProjectSettings/Graphics`의 **Always Included Shaders** / `Resources` 폴더 | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Shader.Find.html |
| P19 | Unity Manual — Draw call batching | 런타임 생성 지오메트리에 대해: **"For geometry generated at runtime, such as particles, lines, and trails, Unity batches all the meshes into a single vertex buffer, then submits one draw call for each mesh."** → **파티클 시스템 하나 = 드로우콜 하나** | https://docs.unity3d.com/6000.3/Documentation/Manual/DrawCallBatching.html |
| P20 | Unity Manual — Standard Particle Shaders (빌트인) | 빌트인 파티클 셰이더 2종: **Standard Particles Surface** / **Standard Particles Unlit**("faster than the Surface Shader", 조명 계산 없음). Rendering Mode에 **Additive**("Adds the background and final particle color together. This is useful for glow effects, like those you might use for fire or magic spells")·Subtractive·Modulate 존재 | https://docs.unity3d.com/6000.3/Documentation/Manual/shader-StandardParticleShaders.html<br>https://docs.unity3d.com/6000.3/Documentation/Manual/shader-StandardParticleShadersCreate.html |
| P21 | Unity Manual — GPU instancing for Particle Systems | 인스턴싱은 **Render Mode가 Mesh일 때만**, 지원 셰이더(`Particles/Standard Surface`) 필요 | https://docs.unity3d.com/6000.3/Documentation/Manual/PartSysInstancing.html |
| P22 | Unity ScriptRef — `ParticleSystemRenderer.sortingFudge` / `.pivot` / `.renderMode` / `.sortMode` | 코드로 세팅할 실제 프로퍼티명 | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystemRenderer-sortingFudge.html |
| **L1** | **로컬 실측** — `strings` on `/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/Resources/unity_builtin_extra` | Unity 6000.3.14f1이 **실제로 담고 있는** 셰이더명 확인: `Sprites/Default`, `Sprites/Diffuse`, `Sprites/Mask`, `Particles/Standard Unlit`, `Particles/Standard Surface`, `Legacy Shaders/Particles/Additive`(+Soft), `Legacy Shaders/Particles/Alpha Blended`, `Mobile/Particles/Additive` 등 **전부 존재** | (로컬 파일) |
| **L2** | **로컬 실측** — `ProjectSettings/GraphicsSettings.asset` | `m_AlwaysIncludedShaders`에 **7개만** 등록돼 있고 Legacy·Particles 계열은 **없다**. `m_SpritesDefaultMaterial`은 등록돼 있어 `Sprites/Default`는 빌드 포함이 보장된다 | (로컬 파일) |
| **L3** | **로컬 실측** — `Packages/manifest.json` | URP·HDRP·`com.unity.postprocessing`·`com.unity.2d.pixel-perfect` **모두 미설치**. `com.unity.modules.particlesystem` 있음 | (로컬 파일) |
| G1 | Unity 빌트인 셰이더 소스(커뮤니티 미러) — `Sprites-Default.shader` | `Sprites/Default`의 SubShader가 **`Lighting Off`**, `Cull Off`, `ZWrite Off`, `Queue=Transparent`를 하드코딩. 조명 관련 프로퍼티가 **하나도 없다** → 실광원 무반응은 사양이지 버그가 아니다 | https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/DefaultResourcesExtra/Sprites-Default.shader |

### 1-2. ⚠️ 확인하지 못한 것 — 근거로 쓰지 않는다

| 확인하려던 것 | 결과 | 이 문서의 처리 |
|---|---|---|
| `Legacy Shaders/Particles/Additive`의 **Unity 6 시점** 블렌드 식(`Blend SrcAlpha One`) | 셰이더 **이름**은 로컬 에디터에서 실측(L1)했으나, 블렌드 식은 2016년경 커뮤니티 미러 소스에서만 확인 | "Additive다"만 쓰고 **정확한 블렌드 계수를 코드에서 가정하지 않는다.** §8-1에서 오너가 에디터로 육안 확인 |
| `Particles/Standard Unlit`을 **코드로** Additive로 바꾸는 정확한 프로퍼티·키워드(`_Mode`/`_SrcBlend`/`_DstBlend`/`_ALPHABLEND_ON`) | Unity 문서는 **인스펙터 조작법만** 설명한다. 블렌드 전환은 에디터 GUI 스크립트가 수행하며 런타임 API가 문서화돼 있지 않다 | ⚠️ **이 경로를 쓰지 않는다.** §4에서 Legacy 셰이더를 권고 |
| Trails 모듈이 **별도 Trail Material**을 요구하는지 | 문서(P11)에 명시가 없다 | Trails를 쓰지 않는 것으로 결정(§2) — 확인 부담이 이득보다 크다 |
| 빌트인에서 파티클 시스템 여러 개가 **같은 머티리얼이면 드로우콜이 합쳐지는지** | P19는 "각 메시마다 드로우콜 하나 제출"이라고만 말한다. 합쳐진다는 근거를 못 찾았다 | **합쳐지지 않는다고 가정**하고 예산을 짠다(보수적, §7) |
| 파티클에 대한 픽셀 스냅 공식 기능 | `Pixel Perfect Camera`의 Pixel Snapping은 문서상 **Sprite Renderer** 대상이며 SRP 전용(VFX문서 S8). ParticleSystemRenderer용 스냅 기능은 **없다** | 스냅을 포기하고 §6의 완화책으로 대응 |

---

## 2. 모듈별 사용 지침표 ✅

> 비용 표기: **0** = 무시 가능 / **저** = 입자당 산술 몇 줄 / **중** = 입자당 반복 연산 / **고** = 드로우콜·오브젝트 증가

| 모듈 | 용도 | 이 게임에서 쓰나 | 비용 | 근거·이유 |
|---|---|---|---|---|
| **Main** | 수명·속도·크기·색·중력·시뮬레이션 공간·상한 | ✅ **전부** | 0 | P1. `simulationSpace = World`는 이미 옳다 — 캐릭터가 움직여도 불티가 제자리에 남는다 |
| **Emission** | 방출 | ✅ **버스트만**(`rateOverTime = 0`, 코드에서 `Emit(n)`) | 0 | P2. 지속 방출은 500체 화면을 덮는다 |
| **Shape** | 방출 형상 | ✅ **Circle / Cone / Edge**만 | 0 | P3. 쿼터뷰라 Sphere는 위아래로 새 나간다 — Circle(XY)이 화면 평면과 맞다 |
| **Velocity over Lifetime** | 축별 속도 | ✅ 상승 이펙트(불티·치유·기적)에 | 저 | P4. `gravityModifier` 음수보다 **의도가 명확하고 Y만 건드릴 수 있다** |
| **Limit Velocity over Lifetime** | 감속 | ✅ **스파크·먼지에 필수** | 저 | P5. `dampen` 없이는 스파크가 등속으로 날아가 "튀었다 멎는" 느낌이 안 난다 |
| **Force over Lifetime** | 가속도 | 💡 화염 상승기류에만 선택적 | 저 | P6. Velocity로 대체 가능하면 안 쓴다 |
| **Color over Lifetime** | 알파 페이드 | ✅ **전 이펙트 필수** | 저 | P7. 페이드가 없으면 입자가 툭 사라져 픽셀 화면에서 튄다 |
| **Size over Lifetime** | 크기 커브 | ✅ 전 이펙트 | 저 | P8. ⚠️ §6-3의 크기 양자화 규칙과 충돌 — 부드러운 축소는 서브픽셀을 만든다 |
| **Rotation over Lifetime** | 회전 | ❌ **금지** | 저 | P9 + 아트문서 §0-A. 도트가 회전하면 계단이 뭉개진다 |
| **Noise** | 난류 | ⚠️ **연기(사망)에만, Quality=Low·Octaves=1** | 중 | P10이 비용을 명시한다. 24시스템×120입자 규모에서 옥타브를 올리면 안 된다 |
| **Trails** | 궤적 | ❌ **쓰지 않는다** | 중~고 | 궤적은 VFX문서 §2-5대로 **스프라이트 잔상**이 정답(`fx_dash_trail_0~2` 3장이 이미 있다). 게다가 §1-2대로 머티리얼 요구사항이 미확인 |
| **Sub Emitters** | 연쇄 방출 | ❌ **쓰지 않는다** | 고 | P12. 시스템 개수가 곱으로 늘어 §7 예산(24)을 즉시 깬다. 연쇄가 필요하면 호출부에서 `Play()`를 두 번 부른다 |
| **Texture Sheet Animation** | 플립북 | 💡 **P2 단계에서 도입 검토** | 저 | P13. `fx_burst_0~3`(4장) 아트가 이미 있다. `Start Frame` 랜덤화로 군무 방지. **픽셀아트 정합성의 근본 해결책**(VFX문서 §6-13과 동일 판단) |
| **Lights** | 실제 광원 부착 | ❌ **절대 쓰지 않는다** | 고 | §3. 언릿 스프라이트에 아무 효과가 없는데 Light 컴포넌트 생성·컬링 비용만 든다 |
| **Custom Data** | 셰이더 vertex stream | ❌ | 0 | P15. 커스텀 셰이더가 있어야 의미가 있다 — 이 프로젝트엔 없고 만들 계획도 없다(VFX문서 §2-8) |
| **Renderer / Render Mode** | 빌보드·스트레치·메시 | ✅ **Billboard** 기본, **Stretched Billboard**는 스파크에만 | 0 | P16. Mesh는 쓰지 않는다(P21 인스턴싱도 함께 포기 — VFX문서 §6 결론 유지) |
| **Renderer / Sort Mode** | 시스템 **내부** 입자 정렬 | ✅ **None** 유지 | 0 | P16. 알파/애디티브 소프트 점은 내부 정렬이 안 보인다. 정렬 비용만 든다 |
| **Renderer / Sorting Order** | 다른 스프라이트와의 순서 | ✅ **종류별로 분리**(§5) | 0 | 현재 전부 900 → 캐릭터를 덮는다. VFX문서 §4-3 위반 |
| **Renderer / Sorting Fudge** | 반투명끼리의 편향 | 💡 필요 시에만 | 0 | P16. `sortingOrder`가 같은 값일 때만 의미가 있다 — 먼저 `sortingOrder`로 푼다 |
| **Renderer / Pivot** | 회전·크기의 기준점 | 💡 바닥 이펙트에 | 0 | P16. 링·마법진을 발밑에 붙일 때 |
| **Renderer / Min·Max Particle Size** | 화면 비율 기준 크기 클램프 | ⚠️ **건드리지 마라** | 0 | P16. 뷰포트 비율 기준이라 해상도가 바뀌면 픽셀 크기가 깨진다 |
| **Culling Mode** | 화면 밖 시뮬레이션 | ✅ **Pause**(기본 자동에 맡기지 말고 명시) | 0 | P1. 500체 화면에서 화면 밖 이펙트가 계속 돌 이유가 없다 |
| **Ring Buffer Mode** | 입자 재활용 | ❌ | 0 | P1. 짧은 원샷 버스트라 상한에 닿을 일이 없다 |
| **Stop Action** | 종료 처리 | ❌ | 0 | P1. 풀 방식(24개 재사용)이라 Disable/Destroy가 필요 없다 |
| Collision / Triggers / Inherit Velocity / External Forces / Lifetime by Emitter Speed | — | ❌ | 중~고 | 이 게임의 이펙트는 물리와 상호작용하지 않는다 |

---

## 3. 라이팅 결론 ⚠️ — 가장 중요한 절

### 3-1. 질문: `Lights` 모듈로 "빛"을 만들 수 있나 → **아니다. 화면에 아무 변화도 없다.**

| 단계 | 확인 내용 | 근거 |
|---|---|---|
| ① `Lights` 모듈은 무엇을 하나 | 입자에 **실제 `Light` 컴포넌트**(3D 광원)를 붙인다 | P14 |
| ② 이 프로젝트의 렌더 파이프라인은 | **빌트인 / Forward** — SRP 아님 | L2 (`m_CustomRenderPipeline: 0`, `m_DefaultRenderingPath: 1`) |
| ③ 빌트인에서 실광원은 동작하나 | **동작한다** — 단 **조명을 받는 셰이더**에 한해서 | P14에 파이프라인 제한 언급 없음 |
| ④ 이 게임의 스프라이트 셰이더는 | **전부 `Sprites/Default`**(`SpriteBank` 아틀라스 1장 + 머티리얼 1장, `FxParticles`도 같은 셰이더) | 코드 실측 (`FxParticles.cs:60`) |
| ⑤ `Sprites/Default`는 조명을 받나 | **안 받는다.** SubShader에 **`Lighting Off`**가 하드코딩돼 있고 조명 프로퍼티가 하나도 없다 | G1 |
| **결론** | **`Lights` 모듈 = 시각 효과 0, 비용만 발생.** Light 컴포넌트 생성·컬링·`Maximum Lights` 관리 비용을 내고 화면은 그대로다 | — |

- ⚠️ **"라이트를 켰는데 안 보인다"를 셰이더 탓이 아니라 라이트 설정 탓으로 오진하기 가장 쉬운 구조다.** Intensity·Range를 아무리 올려도 절대 안 보인다. 이 절을 읽고도 시도할 거라면 §8의 네거티브 컨트롤(라이트 완전 삭제 후 스크린샷 대조)을 먼저 하라.

### 3-2. 그럼 왜 안 쓰나 — 대안 4개를 전부 검토한 결과

| 대안 | 빌트인에서 가능한가 | 채택 | 이유 |
|---|---|---|---|
| **A. `Lights` 모듈 + 실광원** | 기술적으로 가능하나 **효과 0** | ❌ | §3-1. 스프라이트가 언릿 |
| **B. 스프라이트를 `Sprites/Diffuse`로 교체해 조명을 받게** | 가능 — `Sprites/Diffuse`는 6000.3.14f1에 실재(L1) | ❌ | ①머티리얼이 갈라져 **`SpriteBank`의 아틀라스 1장 배칭 전제가 깨진다**(W1 성능 500체 177~729fps의 전제) ②픽셀아트 팔레트가 조명으로 중간색을 얻어 아트문서 §0-A의 색 규칙이 무너진다 ③빌트인 Forward는 per-pixel 라이트 수에 품질 설정 상한이 있다 |
| **C. 포스트프로세싱 블룸** | ❌ **패키지 추가 없이는 불가능** | ❌ | P17이 명시: 빌트인은 **기본 제공 포스트프로세싱이 없다**. `com.unity.postprocessing`(PPv2)을 새로 넣어야 한다 = **의존성 추가**(CLAUDE.md: 승인 필요). 게다가 언릿 LDR 스프라이트는 색이 1.0에서 클램프돼 임계값을 1 아래로 낮춰야 하고, 그러면 **밝은 도트 전체가 번져** 픽셀아트가 뭉개진다 |
| **D. Additive 블렌딩 + 가짜 광륜 스프라이트(방사형 그라디언트)** | ✅ **셰이더·패키지 추가 0** | ✅ **채택** | Additive는 배경색과 입자색을 더한다 — P20 문서가 직접 "**glow effects, like those you might use for fire or magic spells**"에 쓰라고 말하는 용도다. 텍스처는 이미 런타임 생성 중이고(32×32 소프트 점), 광륜은 그 텍스처를 크게·저알파로 한 겹 더 까는 것뿐이다 |

### 3-3. 확정 — "빛나 보이게" 하는 이 게임의 방법 ✅

**빛 = ① Additive 블렌딩 + ② 큰 소프트 광륜 1~2겹 + ③ 알파 펄스(`colorOverLifetime`) + ④ 짧은 흰색 코어.**

| 층 | 무엇 | 값 | 왜 |
|---|---|---|---|
| **코어** | 작고 흰 입자 | size 0.06~0.12, lifetime 0.10~0.20, alpha 1.0 | 광원의 "중심". 짧아야 눈이 밝다고 읽는다 |
| **광륜(halo)** | 크고 흐린 입자 | size 0.8~1.6, lifetime 0.35~0.8, **alpha 0.15~0.30** | Additive라 알파를 낮춰도 밝다. 0.35를 넘으면 도트를 덮는다(VFX문서 §4-3) |
| **불티** | 코어 색의 작은 파편 | size 0.06~0.10, 수 8~20 | "터졌다"는 사건성 |
| **펄스** | 광륜의 알파를 수명 동안 0→peak→0 | `colorOverLifetime` 알파키 3개 | 켜졌다 꺼지는 것이 곧 "빛" |

- ⚠️ **Additive 금지 구역은 그대로다**(VFX문서 §4-2): 잿빛 연기(사망)·장판 예고. Additive면 어두운 것이 밝게 타올라 의미가 뒤집힌다.
- 💡 **Additive는 검은색이 투명이 된다.** 광륜 텍스처의 가장자리는 알파가 아니라 **색이 0으로** 가야 깨끗하다 — 현재 텍스처는 RGB=흰색 고정에 알파만 감쇠하므로 Additive에서도 정상 동작한다(`SrcAlpha`가 곱해지므로).

---

## 4. 머티리얼 — 코드로만 만드는 법 ✅

### 4-1. 필요한 머티리얼은 **2장뿐**

| 머티리얼 | 셰이더 | 용도 | 빌드 포함 보장 |
|---|---|---|---|
| `_matAlpha` | **`Sprites/Default`** | 연기·먼지·장판·예고·무적 오라 | ✅ **보장됨** — `GraphicsSettings.asset`의 `m_SpritesDefaultMaterial`이 참조한다(L2) |
| `_matAdd` | **`Legacy Shaders/Particles/Additive`** | 스파크·불티·성광·광륜·일섬 | ⚠️ **보장 안 됨** — §4-2 필수 조치 |

- ✅ 두 머티리얼 모두 **같은 런타임 생성 텍스처**를 공유한다. 텍스처가 하나이므로 아트 신규 0장 원칙(`FxParticles.cs` 주석 ①)이 유지된다.
- ❌ **`Particles/Standard Unlit`을 코드로 Additive 전환하지 마라.** 셰이더는 실재하고(L1) 인스펙터에 Additive 모드가 있지만(P20), **코드에서 그 모드를 켜는 프로퍼티·키워드가 문서화돼 있지 않다**(§1-2). 추측으로 `_SrcBlend`를 쓰면 에디터에선 되고 빌드에서 다르게 나올 수 있다.
- ❌ **커스텀 `.shader` 에셋을 만들지 마라.** VFX문서 §2-8의 "셰이더는 마지막" 원칙 + 머티리얼 분기가 배칭 전제를 깬다.

### 4-2. ⚠️ 빌드 사고 예방 — Additive 셰이더는 **Always Included Shaders에 등록해야 한다**

`Shader.Find("Legacy Shaders/Particles/Additive")`는 지금 상태로 **에디터에서만 동작하고 빌드에서는 분홍 에러 셰이더**가 된다. P18이 그대로 명시한다:

> "Shader.Find will work only in the Editor, and will result in the pink error shader in a build."

실측(L2): `ProjectSettings/GraphicsSettings.asset`의 `m_AlwaysIncludedShaders`에 7개가 있으나 Legacy·Particles 계열은 **없다.**

| 조치 | 방법 | 담당 |
|---|---|---|
| **필수** | `Project Settings > Graphics > Always Included Shaders`에 **`Legacy Shaders/Particles/Additive` 추가** | 오너(에디터 조작) |
| 대안 | 그 셰이더를 참조하는 머티리얼 에셋 1장을 `Resources/`에 두고 `Resources.Load` | ⚠️ 에셋 파일이 늘어 `FxParticles`의 "에셋 0장" 원칙과 충돌 — 권하지 않음 |
| **코드 방어** | `Shader.Find` 결과가 `null`이거나 `name`이 기대와 다르면 **경고 로그 + `Sprites/Default`로 폴백** | 프로그래머 |

- 💡 **이건 "에디터에서 잘 보였다"가 빌드에서 무너지는 전형이다.** 이 저장소는 같은 계열 사고를 이미 겪었다(배포본이 로컬과 달랐던 2026-07-26 건). §8의 검증 목록에 빌드 확인을 넣었다.

### 4-3. 코드 스케치

```csharp
static Material _matAlpha, _matAdd;

static Material MakeMat(string shaderName, Texture tex, Material fallback = null)
{
    var sh = Shader.Find(shaderName);
    if (sh == null || sh.name != shaderName)
    {
        Debug.LogWarning($"[FxParticles] 셰이더 '{shaderName}' 없음 — 폴백. " +
                         "빌드라면 Graphics > Always Included Shaders 등록을 확인하라.");
        return fallback;                       // null이면 호출부가 알파 머티리얼로 대체
    }
    return new Material(sh) { mainTexture = tex };
}
```

---

## 5. 이펙트 레시피 11종 ✅

### 5-0. 공통 규약 (모든 레시피에 적용)

| 항목 | 값 | 이유 |
|---|---|---|
| `main.playOnAwake` / `main.loop` | `false` / `false` | 원샷 |
| `main.simulationSpace` | `World` | 기존 코드 유지 — 캐릭터가 움직여도 이펙트는 제자리 |
| `main.startRotation` | **`0` 고정**, `startRotation3D = false` | §6. 회전 금지 |
| `main.cullingMode` | `Pause` | P1 |
| `emission.rateOverTime` | `0` — 전부 `ps.Emit(n)` | P2 |
| `renderer.renderMode` | `Billboard` (스파크만 `Stretch`) | P16 |
| `renderer.sortMode` | `None` | P16 |
| `renderer.alignment` | `View` | 쿼터뷰 카메라 정면 |
| `colorOverLifetime` | **모든 레시피가 알파 페이드를 반드시 켠다** | P7 |
| 크기 | **1/32 유닛의 정수배**(PPU 32) — 0.0625 / 0.125 / 0.1875 / 0.25 / 0.3125 | §6-3 |

**정렬 계층 확정** (VFX문서 §4-3 ⚠️ 항목의 해소):

| 층 | `sortingOrder` | 대상 |
|---|---|---|
| 그림자 | 200 | (기존) |
| **바닥 이펙트** | **205** | 마법진 예고·쇼크웨이브·먼지·장판 |
| **캐릭터 뒤 오라** | **210** | 무적 오라·광륜 펄스 |
| 유닛 | `Depth(y)` | (기존) |
| **앞 섬광** | **900** | 피격 스파크·일섬·기적 코어 |

---

### R1. 임팩트 스파크 (`FxKind.피격`) — 맞았다는 즉각 피드백

| 항목 | 값 |
|---|---|
| 머티리얼 | **Additive** |
| `startLifetime` | `0.16 ~ 0.26`(TwoConstants) |
| `startSpeed` | `3.0 ~ 5.0` |
| `startSize` | `0.0625 ~ 0.125` |
| `gravityModifier` | `1.4` |
| `shape` | `Sphere`, `radius = 0.08`, `radiusThickness = 1` |
| `limitVelocityOverLifetime` | `enabled`, `dampen = 0.55`, `limit = 1.2` ← **핵심** |
| `colorOverLifetime` | 알파 `1.0 @0 → 1.0 @0.5 → 0 @1` |
| `sizeOverLifetime` | `1 → 0.4` (EaseInOut) |
| `startColor` | `#FF5A4D → #B31A1A` (TwoColors) |
| `renderMode` | **`Stretch`**, `velocityScale = 0.06`, `lengthScale = 1.6` |
| `sortingOrder` | **900** |
| 방출 수 | **6** (⚠️ 현재 8 → 낮춘다. 500체에서 이게 제일 자주 터진다) |

- 💡 `Limit Velocity`의 `dampen`이 이 레시피의 전부다(P5). 없으면 스파크가 등속으로 날아가 "튀었다 멎는" 물리감이 사라진다.
- ⚠️ VFX문서 §4-1: **몹 피격에는 이 이펙트를 쓰지 않는다.** 파티원 피격 전용.

### R2. 화염/잉걸 플룸 (`FxKind.화염폭풍`)

| 항목 | 값 |
|---|---|
| 머티리얼 | **Additive** |
| `startLifetime` | `0.55 ~ 0.85` |
| `startSpeed` | `0.3 ~ 0.7` |
| `startSize` | `0.125 ~ 0.25` |
| `gravityModifier` | `0`(← **Velocity 모듈로 옮긴다**) |
| `velocityOverLifetime` | `space = World`, `y = 1.2 ~ 2.2` |
| `shape` | `Circle`, `radius = 1.6 * scale`, `radiusThickness = 1`, **`arc = 360`** |
| `noise` | `enabled`, `quality = Low`, `octaveCount = 1`, `strength = 0.35`, `frequency = 0.8`, `scrollSpeed = 0.6` |
| `colorOverLifetime` | 알파 `0 @0 → 1 @0.15 → 0 @1` |
| `sizeOverLifetime` | `1 → 0.25` |
| `startColor` | `#FFB833 → #FF4719` |
| `sortingOrder` | **205**(바닥 위, 캐릭터 뒤) |
| 방출 수 | **32** (현재 40 → 낮춤) |

- ⚠️ 화염폭풍은 **환경 위험**이다(VFX문서 §4-2: 주황~적 + 바닥에만 + 캐릭터보다 뒤). 현재 `sortingOrder 900`은 규칙 위반이다.
- 💡 Noise는 **여기와 R7(연기)에만** 쓴다. P10의 비용 경고 때문이다.

### R3. 성광 기둥 / 기적 (`FxKind.기적`) — 판을 뒤집는 순간

3층 구성. **한 번의 `Play()`에서 시스템 2개를 쓴다**(코어+기둥 / 광륜) — 풀에서 2칸을 소비한다.

| 층 | 머티리얼 | 값 |
|---|---|---|
| **기둥** | Additive | `startLifetime 0.7~1.0`, `startSpeed 0`, `velocityOverLifetime.y = 3.5~5.5`, `startSize 0.125~0.25`, `shape = Circle radius 0.35*scale`, 수 **40** |
| **코어** | Additive | `startLifetime 0.18`, `startSpeed 0`, `startSize 0.5`, 수 **1**, `sizeOverLifetime 0.3→1.4` |
| **광륜** | Additive | `startLifetime 0.55`, `startSize 1.5`, **알파 0.22**, 수 **1**, `sizeOverLifetime 0.6→1.0` |
| 색 | — | `#FFF2A0 → #FFFFFF` |
| `sortingOrder` | — | 기둥·코어 **900** / 광륜 **210** |

- ⚠️ **광륜은 캐릭터 뒤(210)** 여야 한다. 앞에 두면 §4-3 "실루엣을 덮지 않는다"를 정면으로 어긴다.

### R4. 힐 파동 (`FxKind.치유파동`)

| 항목 | 값 |
|---|---|
| 머티리얼 | **Additive** |
| `startLifetime` | `0.65 ~ 0.9` |
| `startSpeed` | `0.2 ~ 0.5` |
| `startSize` | `0.09375 ~ 0.1875` |
| `velocityOverLifetime` | `y = 1.4 ~ 2.0` |
| `shape` | `Circle`, `radius = 0.9 * scale`, `radiusThickness = 0.4` |
| `colorOverLifetime` | 알파 `0 @0 → 0.9 @0.2 → 0 @1` |
| `startColor` | `#99FFB3 → #FFFFD9` |
| `sortingOrder` | **210**(캐릭터 뒤 — 치유는 상태이지 사건이 아니다) |
| 방출 수 | **18** |

### R5. 쇼크웨이브 링 — **신규**

바깥으로 퍼지는 얇은 고리. 도발·보스 착지·폭발에 쓴다.

| 항목 | 값 |
|---|---|
| 머티리얼 | Additive(아군 금색) / **Alpha**(적·환경) |
| `startLifetime` | `0.35 ~ 0.45` |
| `startSpeed` | `4.0 * scale` (**전 입자 동일 — 랜덤 금지**. 랜덤이면 고리가 아니라 구름이 된다) |
| `startSize` | `0.125` 고정 |
| `gravityModifier` | `0` |
| `shape` | `Circle`, `radius = 0.05`, `radiusThickness = 1`, `arc = 360`, `arcMode = Loop` |
| `limitVelocityOverLifetime` | `dampen = 0.85` ← 고리가 멎으며 사라진다 |
| `colorOverLifetime` | 알파 `1 @0 → 0.8 @0.6 → 0 @1` |
| `sizeOverLifetime` | `1 → 0.35` |
| `sortingOrder` | **205** |
| 방출 수 | **24**(고리 밀도. 32 이상은 낭비) |
| ⚠️ 쿼터뷰 | **Y를 눌러야 한다** — `main.startSizeYMultiplier`가 아니라 **`transform.localScale = new Vector3(1, ISO_Y, 1)`**로 시스템 전체를 눌러라(`PlaceRing`이 이미 쓰는 방식) |

### R6. 먼지 퍼프 — **신규** (착지·대시 시작·몹 스폰)

| 항목 | 값 |
|---|---|
| 머티리얼 | **Alpha** (⚠️ Additive 금지 — 먼지가 빛나면 안 된다) |
| `startLifetime` | `0.3 ~ 0.5` |
| `startSpeed` | `0.8 ~ 1.6` |
| `startSize` | `0.09375 ~ 0.1875` |
| `gravityModifier` | `0.15` |
| `shape` | `Circle`, `radius = 0.2`, `radiusThickness = 1`, ⚠️ 시스템을 `ISO_Y`로 눌러 바닥에 눕힌다 |
| `limitVelocityOverLifetime` | `dampen = 0.8` |
| `colorOverLifetime` | 알파 `0.55 @0 → 0.35 @0.4 → 0 @1` (**최대 0.55** — 먼지는 배경이다) |
| `sizeOverLifetime` | `0.6 → 1.0` (퍼지며 옅어진다) |
| `startColor` | `#9A8E7A → #6B6154` |
| `sortingOrder` | **205** |
| 방출 수 | **7** |

### R7. 사망 연기 (`FxKind.사망`)

| 항목 | 값 |
|---|---|
| 머티리얼 | **Alpha** (⚠️ Additive 절대 금지 — VFX문서 §4-2, 기존 코드 주석의 판단 유지) |
| `startLifetime` | `0.7 ~ 1.0` |
| `startSpeed` | `0.3 ~ 0.6` |
| `startSize` | `0.1875 ~ 0.3125` |
| `velocityOverLifetime` | `y = 0.4 ~ 0.8` |
| `noise` | `quality = Low`, `octaveCount = 1`, `strength = 0.25`, `frequency = 0.5` |
| `colorOverLifetime` | 알파 `0.7 @0 → 0.5 @0.5 → 0 @1` |
| `sizeOverLifetime` | `0.7 → 1.3` |
| `startColor` | `#8C8C99 → #40404D` |
| `sortingOrder` | **900**(파티원 사망은 놓치면 안 되는 사건) |
| 방출 수 | **14** |

### R8. 일섬 (`FxKind.일섬`) — 순간 흰 섬광

| 항목 | 값 |
|---|---|
| 머티리얼 | **Additive** |
| `startLifetime` | `0.10 ~ 0.18`(⚠️ **가장 짧다** — 길면 섬광이 아니다) |
| `startSpeed` | `6 ~ 9` |
| `startSize` | `0.0625 ~ 0.125` |
| `shape` | `Cone`, `angle = 12`, `radius = 0.05` — **방향성이 있어야 "베었다"로 읽힌다** |
| `renderMode` | **`Stretch`**, `lengthScale = 2.2` |
| `colorOverLifetime` | 알파 `1 → 0` (선형) |
| `startColor` | `#FFFFFF → #CCE0FF` |
| `sortingOrder` | **900** |
| 방출 수 | **10** |

- 💡 Cone의 방향은 `ps.transform.rotation`으로 준다 — 공격자→피격자 벡터를 호출부가 이미 안다.

### R9. 마법진 텔레그래프 — **신규** (§10-5 예고 표식)

⚠️ **이건 게임 규칙 정보다.** 화려함보다 **읽힘**이 우선이고, 지속 시간이 예고 시간과 **정확히** 일치해야 한다.

| 항목 | 값 |
|---|---|
| 머티리얼 | **Alpha** (⚠️ Additive 금지 — 예고는 위험이지 빛이 아니다) |
| 구성 | **파티클은 보조**다. 범위 자체는 기존 `warn_circle_*` 스프라이트(3장, 로드조차 안 되고 있다)로 그린다 |
| `startLifetime` | `= 예고 시간`(고정, 랜덤 금지) |
| `startSpeed` | `0` |
| `startSize` | `0.125` |
| `shape` | `Circle`, `radius = 장판 반경`, `radiusThickness = 0.05`(테두리만) |
| `colorOverLifetime` | 알파 `0.3 @0 → 0.9 @0.85 → 0.9 @1` ← **끝으로 갈수록 밝아진다**(= 임박) |
| `sortingOrder` | **205** |
| `emission` | 버스트 1회, 수 **20** |
| ⚠️ | **깜빡임(펄스)을 넣지 마라** — 위험이 사라진 것처럼 보이는 프레임이 생긴다 |

### R10. 소프트 광륜 펄스 — **신규 · §3-3의 "빛" 구현체**

무적 오라·보스 페이즈·기적 등 "빛나야 하는" 모든 곳의 공용 부품.

| 항목 | 값 |
|---|---|
| 머티리얼 | **Additive** |
| `startLifetime` | `0.45`(1회 펄스) 또는 반복 호출 |
| `startSpeed` | `0` |
| `startSize` | `1.0 ~ 1.6 * scale` (⚠️ 캐릭터 2.0유닛의 50~80% — **광륜만 유일하게 §4-3 크기 상한(0.35)의 예외**다. 캐릭터 뒤에 알파 0.3 이하로 깔리므로 실루엣을 안 가린다) |
| `shape` | `enabled = false`(정확히 중심 1개) |
| `colorOverLifetime` | 알파 **`0 @0 → 0.28 @0.35 → 0 @1`** ← **이 곡선이 "펄스"의 전부** |
| `sizeOverLifetime` | `0.75 → 1.15`(숨쉬듯) |
| `sortingOrder` | **210 (캐릭터 뒤)** |
| 방출 수 | **1** |
| 반복 펄스 | 호출부가 `0.45초`마다 `Play()` 재호출 — `loop`를 쓰지 마라(풀 회수가 꼬인다) |

- ⚠️ **알파 0.35를 넘기지 마라.** VFX문서 §4-3 + 아트문서 §0-A. 넘으면 그 캐릭터의 도트가 안 보인다.

### R11. 무적 표시 (`FxKind.무적`) — ⭐ 최우선

아트문서 §0-A가 **"무적이 눈에 안 보이면 그 기술은 학습 불가능"**이라고 못 박은 항목이다.

| 항목 | 값 |
|---|---|
| 구성 | **R10 광륜(직업군 색) + 작은 궤도 입자 8개** |
| 머티리얼 | **Additive** |
| `startLifetime` | **`0.30` 고정** — `W2Arena.DashIFrame`과 **정확히 같은 값**이어야 한다 |
| `startSpeed` | `0.8` |
| `startSize` | `0.09375` |
| `shape` | `Circle`, `radius = 0.5 * scale`, `radiusThickness = 0` (테두리에서만) |
| `colorOverLifetime` | 알파 `1 @0 → 1 @0.8 → 0 @1` (⚠️ **끝까지 밝게 유지**하다 급히 꺼진다 — 무적 종료 시점이 명확해야 한다) |
| `startColor` | 직업군별 (아트문서 §0-A): 탱 `#FFD24D` 금 / 근접딜 `#A855F7` 보라 / 원거리딜 `#22D3EE` 청록 / 힐·버퍼 `#FFD24D`+`#6EE7A0` |
| `sortingOrder` | **210** |
| 방출 수 | **8** |

- ⚠️ **`startLifetime`을 상수로 하드코딩하지 말고 `W2Arena.DashIFrame`을 인자로 받아라.** 두 값이 어긋나면 플레이어가 잘못된 타이밍을 학습한다 — 이 게임에서 가장 나쁜 종류의 버그다.

---

## 6. 픽셀아트 제약 대응 규칙 ✅

> 상위: 아트문서 §0-A — **충돌 시 아트문서가 이긴다.** 이 절은 VFX문서 §3을 파티클 관점으로 구체화한 것이다.

### 6-1. 전제 — 파티클용 픽셀 스냅 기능은 **존재하지 않는다**

| 사실 | 근거 |
|---|---|
| `Pixel Perfect Camera`의 Pixel Snapping은 **Sprite Renderer** 대상이고 **SRP 전용**이다 | VFX문서 S8 |
| 이 프로젝트에 `com.unity.2d.pixel-perfect`는 **미설치** | L3 |
| `ParticleSystemRenderer`용 스냅 옵션은 문서 어디에도 없다 | P16 (없음을 확인) |
| **결론** | 파티클은 **서브픽셀로 움직인다.** 이걸 없앨 수 없으므로 **눈에 덜 띄게** 만드는 것이 이 절의 전략이다 |

### 6-2. 회전 — **전면 금지** ❌

| 규칙 | 코드 |
|---|---|
| 시작 회전 0 | `main.startRotation = 0f; main.startRotation3D = false;` |
| 수명 회전 비활성 | `ps.rotationOverLifetime.enabled = false;` (P9) |
| 속도 기반 회전 비활성 | `ps.rotationBySpeed.enabled = false;` |
| ⚠️ 방향감이 필요하면 | **회전이 아니라 `renderMode = Stretch`** — 이동 방향으로 늘어나기만 하고 도트가 안 돌아간다(R1·R8) |

### 6-3. 크기 — **1/32 유닛의 정수배로 양자화** ✅

| 항목 | 값 |
|---|---|
| PPU | **32** (`TextureImportRules.cs`가 FX에 강제) |
| 허용 크기 | `0.0625`(2px) · `0.09375`(3px) · `0.125`(4px) · `0.1875`(6px) · `0.25`(8px) · `0.3125`(10px) |
| ⚠️ `sizeOverLifetime` | 연속 커브라 **중간값은 양자화가 안 된다.** 대응: 커브의 시작·끝만 양자화 값으로 잡고, **수명을 짧게 유지**해 중간 프레임이 적게 보이게 한다 |
| 💡 근본 해결 | `sizeOverLifetime` 대신 **`colorOverLifetime` 알파 페이드만** 쓴다 — 크기는 고정, 밝기만 변한다. 픽셀 격자가 절대 안 깨진다 |

### 6-4. 텍스처 — **하드엣지 + Point** (VFX문서 §3-2 D3 해소)

| 현재 | 바꿀 것 |
|---|---|
| 32×32, 거리 제곱 감쇠, `FilterMode.Bilinear`(코드 실측 `FxParticles.cs:48`) | **8×8 하드엣지 + `FilterMode.Point`**, `wrapMode = Clamp`, `mipmapCount = 1`(밉맵 없음) |
| 소프트 점 1장 | **2장으로 분리**: ①`_texDot` 8×8 하드 원(입자용) ②`_texHalo` 32×32 소프트 방사(광륜 R10 전용 — 여기만 Bilinear 허용) |

- ⚠️ **광륜만 Bilinear 예외를 준다.** 광륜은 "도트"가 아니라 "빛"이고, 하드엣지 광륜은 그냥 큰 원판이라 빛으로 안 읽힌다. **이 예외를 다른 입자에 확대하지 마라.**
- 💡 밉맵을 끄는 이유: 카메라 거리가 고정된 쿼터뷰에서 밉맵은 이득이 0이고, 작은 입자가 저해상도 밉으로 떨어져 뿌옇게 보일 수 있다.

### 6-5. Texture Sheet Animation — 프레임 스테핑 (P2 단계)

| 항목 | 값 |
|---|---|
| 모드 | `ParticleSystemAnimationMode.Sprites` + `AddSprite(fx_burst_0..3)` (P13) |
| `frameOverTime` | ⚠️ **선형 커브가 아니라 계단**을 만들어라 — 4프레임 아트를 선형 보간으로 재생하면 프레임 사이가 뭉개진다. `AnimationCurve`의 키를 `Constant` 탄젠트로 |
| `startFrame` | `0 ~ 3` 랜덤 (P13) — **없으면 100개가 군무를 춘다** |
| `cycleCount` | `1` |
| 💡 | 플립북으로 가면 §6-3(크기 양자화)·§6-4(텍스처) 문제가 **동시에** 사라진다. 아트가 이미 4장 있다 |

### 6-6. 카메라 정합

| 규칙 | 이유 |
|---|---|
| `Camera.orthographicSize`는 **화면 세로 픽셀 / (2 × PPU)** 의 정수배로 | 1 텍셀 = 1 화면 픽셀이 되어야 도트가 선명하다 |
| ⚠️ 카메라가 서브픽셀 위치에 있으면 | **모든 스프라이트가 같이 흐려진다.** 파티클만의 문제가 아니므로 이 항목은 카메라 담당 코드에서 별도로 다룬다 |

---

## 7. 성능 예산 ✅

### 7-1. 근거

| 사실 | 근거 |
|---|---|
| 런타임 생성 지오메트리(파티클)는 **"submits one draw call for each mesh"** | P19 |
| 여러 파티클 시스템이 같은 머티리얼이라고 **합쳐진다는 근거는 없다** | §1-2 — 보수적으로 "안 합쳐진다"로 가정 |
| GPU 인스턴싱은 **Mesh 모드 전용** | P21 → 빌보드 스프라이트엔 적용 불가 |
| Noise의 옥타브는 **"significantly adds to the performance cost"** | P10 |

### 7-2. 예산표

| 항목 | 예산 | 초과 시 |
|---|---|---|
| **동시 파티클 시스템(풀)** | **24** (현행 유지) | 가장 오래된 것 재사용 — 이미 구현 |
| **평상시 동시 활성 시스템** | **≤ 8** | 8을 상시 넘으면 이펙트를 줄여야 한다는 신호 |
| **시스템당 `maxParticles`** | **120** (현행 유지) | — |
| **화면 전체 동시 입자** | **≤ 900** (24 × 평균 38) | — |
| **파티클 드로우콜** | **≤ 24** (최악), 평상시 **≤ 8** | 500체 스프라이트가 아틀라스 1장으로 배칭되는 것이 W1 성능의 전제 — 파티클이 그 위에 24 DC를 얹는다 |
| **Additive 머티리얼 추가로 인한 배치 분할** | **+1 머티리얼** = 알파·애디티브 두 그룹 | 3번째 머티리얼을 만들지 마라 |
| **Lights 모듈** | **0개** | §3 |
| **Noise 사용 시스템** | **동시 2개까지**(화염·연기) | P10 |
| **몹 피격 파티클** | **0** | VFX문서 §4-1 — 500체가 동시에 맞으면 화면이 죽는다 |

### 7-3. 풀링 비용

| 항목 | 비용 | 비고 |
|---|---|---|
| 24개 `GameObject` + `ParticleSystem` 상주 | **메모리만** — 비활성 시스템은 시뮬레이션도 렌더도 안 한다 | `EnsureBuilt()` 1회, 이미 구현 |
| `Stop(StopEmittingAndClear)` → 값 재설정 → `Emit(n)` | **프레임당 수십 회면 무시 가능** | ⚠️ 단 `main`/`shape` 구조체 재설정은 매 `Play()`마다 프로퍼티 setter를 수십 번 호출한다 — 초당 100회를 넘으면 프로파일링할 것 |
| 💡 개선 여지 | 종류별로 **미리 세팅된 시스템을 고정 배정**하면 `Play()`가 위치 설정 + `Emit()`만 남는다 | 8종 × 3개 = 24 — 지금 풀 크기와 정확히 맞는다 |

---

## 8. 구현 순서와 검증 ✅

> 이 저장소는 **"코드를 넣었다"와 "화면에 나왔다"를 구분한다.** 각 단계는 ①스크린샷 쌍 ②네거티브 컨트롤을 통과해야 다음으로 간다.
> ✅ **Unity 배치 빌드로 에이전트가 검증한다**(오너 확정 2026-08-14). 스크린샷 쌍·네거티브 컨트롤까지
> 에이전트가 뽑고, 오너는 결과 이미지만 본다. ⚠️ **에디터가 열려 있으면 프로젝트 락으로 배치가 죽는다** —
> 실행 전 `Temp/UnityLockfile`과 `-useHub`로 뜬 Unity 프로세스를 확인할 것(§8-3, 핸드오프 §5 「에디터 락」).

### 8-1. 순서

| # | 단계 | 무엇을 | 검증 (육안) | 네거티브 컨트롤 |
|---|---|---|---|---|
| **0** | **셰이더 등록** | `Graphics > Always Included Shaders`에 `Legacy Shaders/Particles/Additive` 추가(§4-2). **오너 조작** | 등록 후 `GraphicsSettings.asset`의 `m_AlwaysIncludedShaders` 항목 수가 7 → 8 | 등록 전에 **빌드**를 한 번 만들어 분홍 셰이더가 나오는지 확인 → 나오면 P18이 맞다는 증거 |
| **1** | **Additive 머티리얼 도입** | `_matAdd` 추가, R1(피격)만 Additive로 전환 | `vfx_spark_add.png` / `vfx_spark_alpha.png` **쌍** | `_matAdd`를 `_matAlpha`로 바꿔치기 → 스파크가 **눈에 띄게 어두워져야** 한다. 차이가 없으면 머티리얼이 적용 안 된 것 |
| **2** | **텍스처 Point화 + 2장 분리** | §6-4 | 400% 확대 스크린샷에서 입자 가장자리가 **계단**이어야 한다 | `FilterMode.Bilinear`로 되돌려 대조. 두 장이 같으면 필터가 적용 안 된 것 |
| **3** | **정렬 재배치** | §5-0 정렬표대로 `sortingOrder` 분리 | 캐릭터 위에 겹친 이펙트가 **뒤로 가야** 한다 | 전부 900으로 되돌려 대조 |
| **4** | **R11 무적 표시** ⭐ | `W2Arena`의 `_iframe`과 연동 | `vfx_iframe_on.png` / `vfx_iframe_off.png` | `FxKind.무적` 호출을 주석 처리 → 오라가 **사라져야** 한다 |
| **5** | **R10 광륜 펄스** | §3-3의 "빛" | `vfx_halo_peak.png` / `vfx_halo_off.png` (펄스 정점·소멸 프레임) | 알파를 `0`으로 → 완전히 사라져야 한다. 남아 있으면 다른 걸 보고 있던 것 |
| **6** | **R5·R6·R9 신규 3종** | 쇼크웨이브·먼지·마법진 | 각각 쌍 | 방출 수를 `0`으로 |
| **7** | **레시피 값 일괄 적용** | R2·R3·R4·R7·R8 | 500체 화면 1장 필수 | 풀 크기를 **1**로 → 겹칠 때 하나만 보여야 한다 |
| **8** | **(선택) 플립북 전환** | §6-5, `fx_burst_0~3` | 400% 확대에서 도트가 **원본 아트 그대로** | `AddSprite` 호출 제거 → 소프트 점으로 돌아가야 한다 |

### 8-2. 계측 확인 (스크린샷을 못 믿을 때)

| 항목 | 확인법 |
|---|---|
| 입자가 실제로 방출됐나 | `ps.particleCount > 0` — **`Play()` 호출 수가 아니라 입자 수** |
| Additive가 실제로 걸렸나 | `ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name`을 로그. 기대 문자열과 **정확히** 일치해야 한다 |
| 무적 오라가 무적 구간과 일치하나 | `W2Arena._iframe`과 파티클 `startLifetime`을 같은 줄에 로그. **0.30초 ±1프레임** |
| 드로우콜 | Stats 창의 Batches를 이펙트 없는 프레임과 대조. **차이가 활성 시스템 수와 같아야** 한다 — 다르면 §7-1의 가정이 틀린 것이며 예산을 다시 짠다 |
| 500체 성능 | W1 기준(177~729fps)과 대조. **하한이 60 아래로 내려가면 즉시 되돌린다** |

### 8-3. ⚠️ 이 부류에서 자주 나는 사고

| 사고 | 증상 | 진단 |
|---|---|---|
| `Shader.Find` 빌드 누락 | 에디터는 정상, 빌드만 분홍 | §4-2 (P18) |
| 정렬로 가려짐 | `Play()`는 성공, 로그도 정상, 화면엔 없음 | `sortingOrder`를 **9999**로 임시 상향해 보인다면 정렬 문제 |
| 시뮬레이션 공간 혼동 | 이펙트가 캐릭터를 따라다닌다 | `simulationSpace`가 `Local`로 바뀐 것 |
| 크기가 0으로 보임 | 아무것도 안 보임 | `sizeOverLifetime` 커브의 시작값이 0인지 확인 |
| 화면 밖에서 멈춤 | 카메라가 돌아오면 이펙트가 얼어 있다 | `cullingMode` (P1) |

---

## 9. `GAME_SPEC_VFX.md`와 달라진 것 ⚠️ — 조용히 바꾸지 않는다

| # | VFX문서의 내용 | 이 문서의 결론 | 왜 |
|---|---|---|---|
| 1 | §2 표가 파티클을 **7위**(스프라이트 플래시·히트스톱·쉐이크 다음)에 둔다 | **유지.** 이 문서는 순위를 바꾸지 않는다 — 파티클을 어떻게 만드는지만 정한다 | 오너 지시("이펙트는 파티클로")는 **기법 선택이 아니라 저작 도구 지정**으로 해석했다. 히트스톱·쉐이크는 여전히 먼저다 |
| 2 | §4-2가 Additive를 **적 공격·투사체**에만 배정 | **확대.** 아군 성광·스파크·광륜에도 Additive를 쓴다 | §3-2. 빌트인+언릿에서 "빛"을 만드는 **유일한** 무비용 수단이다. 색 분리(§4-2의 진짜 목적)는 **블렌딩이 아니라 색상**으로 계속 유지된다 — 적=자주·심홍, 아군=금·청·초록 |
| 3 | §8-4가 URP 전환을 **미결**로 남김 | **URP는 필요 없다**고 답한다 | §3-2 A~D 검토 결과, Additive+광륜으로 목표를 달성한다. URP는 배칭·`SpriteBank`·IMGUI 재검증 비용이 이득을 넘는다 |
| 4 | §8-1이 파티클 텍스처 처리를 **ⓐ/ⓑ/ⓒ 미결**로 남김 | **ⓐ(8×8 하드엣지 Point) + 광륜만 예외**로 답한다 | §6-4. ⓑ(플립북)는 §8-1의 8단계로 남겨 순차 도입 |
| 5 | §4-3 ⚠️가 `sortingOrder 900` 문제를 지적만 함 | **정렬 계층을 확정**했다(205/210/900) | §5-0 |
| 6 | §6-9가 텍스처를 "8×8 하드엣지"로 통일 | **2장으로 분리**(입자 하드 / 광륜 소프트) | §6-4. 하드엣지 광륜은 빛으로 안 읽힌다 |
| 7 | (언급 없음) | **`Legacy Shaders/Particles/Additive`의 Always Included Shaders 등록**이 빌드 필수 조건 | §4-2. VFX문서 작성 시점에 없던 발견이다 |

---

## 10. 오너 판단 — ✅ 전건 확정 (2026-08-14)

| # | 안건 | 선택지 | 결정 |
|---|---|---|---|
| 1 | **Always Included Shaders 등록**(§4-2) | ⓐ등록한다 ⓑAdditive를 포기하고 전부 Alpha로 간다 | ✅ **ⓐ 확정.** `GraphicsSettings.asset`의 `m_AlwaysIncludedShaders`가 7→8로 등록 완료 |
| 2 | **광륜의 Bilinear 예외**(§6-4) | ⓐ광륜만 예외 허용 ⓑ전부 Point(광륜도) | ✅ **ⓐ 확정.** 아트문서 §0-A의 Point 규칙에 대한 **광륜 단 하나의 예외** — 다른 입자에 번지지 않게 §6-4에 예외 범위를 못박는다 |
| 3 | **광륜 크기 예외**(R10, §4-3 상한 0.35의 예외) | ⓐ알파 0.28 이하 조건으로 허용 ⓑ상한을 지켜 광륜을 포기 | ✅ **ⓐ 확정.** 조건 셋을 **전부** 지킬 때만 유효하다: 정렬 `SortBehind`(210) · 시작 알파 ≤0.30 · 실루엣을 가리지 않음을 §8-1 5단계 스크린샷으로 확인. 하나라도 어기면 예외는 무효 |
| 4 | **플립북 전환 시점**(§6-5) | ⓐ지금 ⓑ8단계에서 ⓒ안 한다 | **ⓑ**(권고 유지 — 오너 판단 대상 아님) |
| 5 | **포스트프로세싱 패키지 도입** | ⓐ`com.unity.postprocessing` 추가해 블룸 ⓑ추가하지 않는다 | **ⓑ**(권고 유지 — 의존성 추가는 하지 않는다) |

> ⚠️ **2·3번은 상위 문서(`GAME_ART_RESOURCES.md` §0-A 픽셀아트 확정)에 대한 명시적 예외**다.
> 예외의 범위는 **`FxKind.광륜` 하나**이며, 다른 이펙트가 "광륜처럼" Bilinear나 큰 크기를 쓰려 하면
> 그건 이 결정의 확대 적용이 아니라 **새 안건**이다 — 다시 물어라.
