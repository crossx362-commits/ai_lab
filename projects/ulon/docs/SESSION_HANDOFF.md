# SESSION HANDOFF — Ulon 자율 개발루프

## 이 세션의 자리
- **작업 트리는 `/Users/junholee/ai_lab-loop`**(브랜치 `loop-claude`, 푸시는 `git push origin HEAD:master`).
  공유 트리 `/Users/junholee/ai_lab`은 **남의 것**이다(Grok·Codex `autodev`). 산출물 경로를 보고할 때는
  **어느 트리인지**를 같이 적는다.
- 랩 모양: 읽기 → 하나 만들기 → **내 눈으로 검증**(셀프체크 EXIT=0 **두 번** + QA PNG를 실제로 열어 보기)
  → 커밋·푸시 → 검수(`local_7b28e464-020e-4be4-905a-270a96c891d7`)에 보고 → 이 파일 갱신.
- 안건 판정은 대장(`local_07be93d8-1ac7-44a4-8d67-c82f238176a1`). 오너 직행은 셋뿐 —
  다운로드·되돌릴 수 없는 삭제·돈. 보고 끝에는 항상 **「구조 한 줄」**.

## 도구
```bash
cd /Users/junholee/ai_lab-loop/projects/ulon
pgrep -f "Unity.*-batchmode"                      # 먼저 확인(유니티는 한 프로젝트 한 인스턴스)
SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log ./tools/slice_selfcheck.sh   # 스크립트 고쳤으면 두 번
./tools/qa_shots.sh                                # builds/qa/*.png
./tools/rebuild_client.sh && ./tools/two_client_check.sh
```
- 2클라 하네스 전에 **영속 서비스가 살아 있어야 한다**: `http://127.0.0.1:8777/character/<key>`,
  cwd `/Users/junholee/ai_lab/projects/ulon`, 데이터 `.../projects/ulon/data`. 빈 응답이면 죽여서 다시 띄운다.
- **`git add -A` 금지**(경로 명시). add와 commit은 한 호흡. `git add`에 없는 경로를 섞으면 **명령 전체가
  실패**하고 아무것도 안 올라간다 — 커밋 뒤 `git show --stat`으로 **무엇이 들어갔는지 확인**할 것
  (2026-09-08 실제로 문서만 올라갔다).

## 커밋 꼬리
`179632d8`(도구 상대경로) → `a824278d`(경보에 머티리얼 목록) → `ea8d2ba3`(계측기 Selected 삭제) →
`6a1f4ba2`(PERF_BASELINE 문서) → **`0aefd409`(그 본체 코드)**.

## 방금 닫은 것 (2026-09-08, 검수 판정 셋)
1. **재현성 결함(무텍스처 618)** — 원인은 소스가 아니라 **도포 시점**. 풀·흙 도포가 마을을 짓는
   도중에만 돌아, 뒤에 세워지는 던전 돌길 타일이 그 판에서 안 칠해졌다(씬이 매 실행 저장되므로
   다음 판에서야 초록). `EnsureWorldPropMaterials`로 옮겨 고쳤고, 씬을 커밋 상태로 되돌린 판에서 EXIT=0.
2. **성능 머티리얼 축** — 자를 「그리는 그림의 가짓수」(셰이더+메인 텍스처+색)로 교체, 양방향 NC
   (사본 36→36 / 다른 그림 36→37·경보). 렌더러는 별도 축, 마을 기준선 730→852.
3. **보스 칼끝 매몰** — 그립 점을 축으로 각도만 고친다. BoneWarden −0.28→+0.17m,
   IronTyrant −1.10→+0.33m, Hexarch −0.76→+0.23m.

## 다음 큐
1. **③ 배우 명단 14개 → 전수 스윕**: 먼저
   `git log --oneline -- unity/Assets/Game/Scripts/Editor/SliceSelfCheck.ActorRoster.cs`와 origin/master를 확인.
   Codex(`autodev`)가 이미 올렸으면 **새로 만들지 말고 그 결과를 검수 대상으로 올린다**. 아니면 대장이 준 패치
   `/private/tmp/claude-501/-Users-junholee-ai-lab/6420896d-962c-4642-a090-8a13538cd049/scratchpad/autodev-patches/actor-roster-gate.patch`
   (loop-claude에서 `git apply --check` 통과 확인) 적용 여부를 내 판단으로 결정.
2. **①② 장비 이름표·손/모자/방패 뼈 고르기**.
3. MegaKit(대장간 모루·화덕 / 절구통)은 오너가 `_ThirdParty/Quaternius/`에 파일을 넣어 줘야 열린다(다운로드는 오너 직행).

## 손대지 말 것
- `OfflineWorld*`, `NetAvatar.cs`(A 차선), 루트 `docs/SESSION_HANDOFF.md`(검수 것),
  `.worktrees/herdr-*`·`autodev-*`, `OfflineWorld.Player`(89곳·미배정 — 잡으려면 DEV_INBOX에 한 줄 먼저).
- 남의 변경은 **되돌리지 않는다**. 충돌 시 상대를 보존, 자동 병합 실패면 `merge --abort`. 강제 푸시 금지.

## 원장 문장(이번 랩)
- **같은 소스에서 트리마다 결과가 다르면 환경 차이가 아니라 재현성 결함이다** — 그리고 그 원인은
  대개 「무엇을 하느냐」가 아니라 **언제 하느냐**다.
- **숫자가 상한에 걸리면 상한이 아니라 내역을 먼저 열어라** — 84 중 34개가 같은 그림이었다.
- **명령이 성공했는지와 내용이 들어갔는지는 다른 질문이다**(`git add` 실패 → 문서만 커밋).
