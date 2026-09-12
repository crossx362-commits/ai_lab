# 창 없는 IMGUI 렌더 진단

Unity 6000.3.14f1 / macOS Metal에서 실행하는 별도 최소 프로젝트다.
게임 프로젝트·원본 Unity·사용자 창을 조작하지 않는다. `GameView`와 `HostView`는
메모리 객체만 만들고 `Show`/`Focus`를 호출하지 않는다.

기존 batch Player의 `ScreenCapture` 자동 프레임 대기와 다른 경로다.
`RenderPlayModeViewCamerasInternal`을 Editor update에서 호출하고 RenderTexture를 읽는다.
`s_RenderingView`/targetSize로 실제 `Screen` 크기를 맞추고, OnGUI에서 픽셀 투영을 설정한다.
폰트 준비에는 실제 Editor update 프레임이 필요하다. 같은 함수 안에서 렌더만 반복하는
방법은 초기 글자 텍스처 문제를 해결하지 못했다.

## 실행

이 디렉터리의 Assets, Packages, ProjectSettings를 **새 output 디렉터리**로 복사한다.
Unity에 다음 인자를 전달한다. 비동기 update 뒤 자체 종료하므로 `-quit`를 넣지 않는다.
외부 실행자도 120초 제한을 적용한다.

```text
-batchmode -projectPath <새 격리 디렉터리> -executeMethod OffscreenCheck.Run -logFile <로그 경로>
```

`-nographics`는 거부한다. evidence가 비어 있지 않아도 거부한다.
1440×900와 1024×768에서 실제 Screen 크기, OnGUI/Repaint, 모서리 색상 표식,
글자 픽셀을 확인한다. 같은 박스에서 글자만 제거한 대조와 IMGUI 전체를 끈 대조를
별도로 확인한다. 하나라도 실패하면 exit 1이다.

## 적용 범위

이 검사는 **진단 UI의 렌더 경로**만 검증한다. 지도/HUD/게임/실제 OS 입력/2인 플레이
합격 근거가 아니다. 실제 HUD를 연결할 때는 같은 OnGUI 문맥에서 실제 그리기 코드를
호출하거나 Play 모드용 어댑터를 만들고, 별도 캡처·입력 상태 전이를 검사해야 한다.
화면 저장 API를 ScreenCapture로 고정할 필요는 없지만 RenderTexture 해상도뿐 아니라
실제 Screen 크기와 글자·픽셀을 함께 검증해야 한다.

내부 API는 이 Unity 버전에 한정한다. 엔진 변경 시 이 프로브부터 다시 실행한다.

## 근거가 되는 Unity 소스

- [PlayModeView.RenderView](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/PlayModeView/PlayModeView.cs)
- [EditorGUIUtility 렌더 바인딩](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/EditorGUIUtility.bindings.cs)
- [EditorWindow 화면 크기 전달](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/EditorWindow.cs)

실패한 실험과 초기 정상 글자 출력은 `output/ulon-codex/offscreen-repair/`에 보존한다.

## 최종 실측 (2026-09-12)

`output/ulon-codex/offscreen-verified3/unity.log`: exit 0, OFFSCREEN_CHECK_PASS.
1440×900/1024×768 실제 Screen 일치, Repaint 72/121회, 글자 픽셀 각708.
글자만 숨긴 대조 717픽셀 차이·표식 유지, IMGUI 끈 대조 Repaint0·글자0.
두 양성 PNG 직접 확인. 초기 글자 미준비 실패도 offscreen-verified에 보존한다.
전체 ai-team 하네스는 기존 데몬 down·백업 정체·아트 분류 경고로 exit1이며 이 검사와 별개다.
