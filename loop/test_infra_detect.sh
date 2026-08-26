#!/bin/bash
# 공급자 장애 판정 자동검사 — 「인프라 실패 ≠ 이슈 실패」 규칙이 실제로 갈라내는지 실측한다.
# 사용: bash loop/test_infra_detect.sh   (종료 0 = 전부 통과)
#
# 왜 필요한가: 2026-08-26 22:23·22:38 두 바퀴가 opencode 서버 오류(Endpoint is unavailable ·
# Unexpected server error)로 죽었는데 「STATUS 미갱신 3회」에 걸려 루프 전체가 정상 종료했다.
# 상대 서버가 죽은 것을 우리가 일을 안 한 것으로 세면 안 된다. 네거티브 컨트롤 없는 통과를
# 만들지 않기 위해 「우리 사정으로 죽은 로그」도 반드시 함께 검사한다.
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOOP="$HERE/loop.sh"
PASS=0; FAIL=0
TMP="$(mktemp -d "${TMPDIR:-/tmp}/infra_detect.XXXXXX")"
trap 'rm -rf "$TMP"' EXIT

judge() { bash "$LOOP" --self-test-infra "$1" >/dev/null 2>&1; }   # 0=장애 1=아님
want() { # want <기대 0|1> <설명> <파일>
  judge "$3"; local rc=$?
  if [ "$rc" = "$1" ]; then echo "ok   - $2"; PASS=$((PASS+1));
  else echo "FAIL - $2 (기대 $1, 실제 $rc)"; FAIL=$((FAIL+1)); fi
}

filler() { for i in $(seq "${1:-50}"); do echo "작업 줄 $i"; done; }

# 1) 포지티브 — 세션 끝에서 터진 공급자 오류들
{ filler; echo 'Error: Error from provider (Console): Upstream request failed: Endpoint is unavailable.'; } > "$TMP/p1.log"
want 0 "Endpoint is unavailable → 장애" "$TMP/p1.log"
{ filler; echo 'Error: {"name": "UnknownError", "data": {"message": "Unexpected server error."}}'; } > "$TMP/p2.log"
want 0 "UnknownError/Unexpected server error → 장애" "$TMP/p2.log"
{ filler; echo 'HTTP 503 Service Unavailable'; } > "$TMP/p3.log"
want 0 "503 → 장애" "$TMP/p3.log"

# 2) 네거티브 — 우리 사정으로 죽은 바퀴는 계속 미갱신으로 세야 한다
{ filler; echo 'Error: max turns reached'; echo '바퀴 종료: code=1'; } > "$TMP/n1.log"
want 1 "max turns reached → 우리 사정" "$TMP/n1.log"
{ filler; echo 'error CS0103: The name does not exist'; } > "$TMP/n2.log"
want 1 "컴파일 오류 → 우리 사정" "$TMP/n2.log"
{ filler; echo '바퀴 종료: code=0'; } > "$TMP/n3.log"
want 1 "정상 종료 → 장애 아님" "$TMP/n3.log"

# 3) 네거티브 — 본문 중간의 인용을 장애로 오독하지 않는다(끝 40줄만 본다)
{ echo 'Endpoint is unavailable'; filler 60; echo '바퀴 종료: code=0'; } > "$TMP/n4.log"
want 1 "중간에만 나온 문구 → 장애 아님" "$TMP/n4.log"

# 4) 네거티브 — 없는 파일
want 1 "로그 파일 부재 → 장애 아님" "$TMP/nope.log"

echo "----------------------------------------"
echo "통과 ${PASS} · 실패 ${FAIL}"
[ "$FAIL" -eq 0 ]
