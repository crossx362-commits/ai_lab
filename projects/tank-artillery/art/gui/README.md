# 캐주얼 탱크 / 이미지 GUI

## 디자인 기준

오너 레퍼런스: 포트리스2. 탱크의 짧고 과장된 실루엣, 캐릭터 성격, 그림으로 만든 각도계·무장 아이콘·하단 ENERGY/POWER/MOVE 계기판을 참고한다.

확인한 레퍼런스:
- https://www.gamemeca.com/view.php?gid=93146 (포트리스2 업데이트 기사와 게임 화면)
- https://store.steampowered.com/app/3152590/ (Fortress 2 BLUE 제품 페이지)

## 원본과 소비 경로

- `../blender/build_models.py`, `../blender/tankfall_roster.blend`: 둥근 차체·큰 눈·볼·입·무장을 포함한 13종 Blender 원본.
- `../../unity/Assets/_Project/Resources/Gui/casual-atlas.png`: 직접 생성한 16칸 RGBA GUI 아틀라스. 패널/선택 버튼/인벤토리/초상화 테두리/각도계/게이지/아이콘.
- `../../unity/Assets/_Project/Resources/Gui/title-logo.png`: 투명 이미지 타이틀 로고. 프롬프트는 `logo-prompt.txt`.
- `../../unity/Assets/_Project/Resources/Gui/title-garden.png`: 직접 생성한 타이틀 일러스트.
- `../../unity/Assets/_Project/Resources/Gui/Portraits/`: Unity에서 실제 13종 탱크를 렌더한 투명 초상화.
- `GuiArt.cs`: 이미지 영역 및 9-slice 렌더링. 글자와 게임 수치는 이미지에 굽지 않고 실시간 렌더링한다.
- `CasualHud.cs`: 하단 통합 계기판, 팀 초상화, 무장·아이템 표시.
- `CasualGuiImporter.cs`: 이미지 알파, 크기, 필터, 무압축 설정.

이미지는 built-in image_gen으로 제작했다. 최종 프롬프트는 `prompts.json`에 보관한다. 레퍼런스 스크린샷은 게임 에셋으로 사용하지 않는다.

## 재생성 / 확인

Blender 재생성 후 Unity의 `Tankfall.EditorTools.BlenderModelVerify.RenderReview`를 실행하면 모델 검사와 초상화 생성, GUI 리소스 검사를 수행한다. 새 아틀라스가 다른 배치를 가지면 `GuiArt.Regions`도 함께 맞춰야 한다.

## 최종 검증 — 2026-09-19

- Blender 재생성 완료, 39개 모델(13종 탱크·26종 발사체) / 657,674 삼각형 / 발사 원점·회전축·주행 부품 검사 통과.
- 이미지 아틀라스·타이틀·13종 초상화 리소스 검사 통과. 투명 로고도 실제 타이틀 화면에서 확인.
- Mac 빌드 `Succeeded`, 오류 0.
- 1440×900, 1280×720 실제 창: 각각 UI 11/11, 캡처 22/22 통과. UI 없는 두 장의 차이 0.00%.
- UI 비교 모드에서만 시간을 고정해 구름·나무 애니메이션으로 생기던 비결정적 대조 실패를 제거했다. 실패 시 비정상 종료 코드를 반환한다.
- 실제 `-autoshot -forcespecial`: 다연장 9발·위성탄 발사, 직격·착탄·지형 파괴 및 캡처 13/13 통과. 다연장 비행과 착탄 캡처 직접 확인.

최종 화면과 로그: `../../output/casual-ui/`.
