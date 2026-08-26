#!/bin/bash
# safe_commit 샌드박스 자동검사 — 실제 임시 저장소로 통과·차단·인덱스 정리를 실측한다.
# 사용법: bash loop/test_safe_commit.sh   (종료 코드 0 = 전부 통과)
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SAFE="$HERE/safe_commit.sh"
PASS=0
FAIL=0

ok()   { echo "ok   - $1"; PASS=$((PASS + 1)); }
bad()  { echo "FAIL - $1"; FAIL=$((FAIL + 1)); }
check() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 (기대 $2, 실제 $3)"; fi; }

TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/safe_commit_test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

mk_repo() {
  git init -q "$1"
  git -C "$1" config user.email t@t
  git -C "$1" config user.name t
  mkdir -p "$1/docs" "$1/loop"
  cp "$HERE/commit_guard.sh" "$1/loop/commit_guard.sh"
  cp "$SAFE" "$1/loop/safe_commit.sh"
  echo base > "$1/base.txt"
  git -C "$1" add base.txt
  git -C "$1" commit -qm init
}

run() { # run <repo> <메시지> <경로...>
  local repo="$1" msg="$2"; shift 2
  (cd "$repo" && printf '%s\n' "$msg" | bash loop/safe_commit.sh "$@" >/dev/null 2>&1)
}

# 1) 포지티브 — 내 파일만 커밋되고, 공유 인덱스에 낡은 스냅이 남지 않는다
R="$TMPROOT/r1"; mk_repo "$R"
echo mine > "$R/docs/mine.md"
run "$R" "docs: 내 파일" docs/mine.md; check "정상 커밋은 0" 0 "$?"
check "커밋에 파일이 들어갔다" "docs/mine.md" "$(git -C "$R" show --name-only --format= HEAD | tr -d '\n')"
check "공유 인덱스에 잔재 없음" "" "$(cd "$R" && git diff --cached --name-only)"

# 2) 핵심 회귀 — 커밋 뒤 맨몸 커밋이 방금 것을 되돌리지 않는다(2026-08-26 사고)
echo v2 > "$R/docs/mine.md"
run "$R" "docs: 2판" docs/mine.md
(cd "$R" && git commit -qam "맨몸 커밋" 2>/dev/null)
check "맨몸 커밋 뒤에도 내용 유지" "v2" "$(cat "$R/docs/mine.md")"
check "HEAD에도 v2가 남는다" "v2" "$(git -C "$R" show HEAD:docs/mine.md 2>/dev/null | tr -d '\n')"

# 3) 격리 — 남의 스테이징이 있어도 내 커밋에 섞이지 않고, 남의 것은 그대로 남는다
R="$TMPROOT/r2"; mk_repo "$R"
echo foreign > "$R/docs/foreign.md"
git -C "$R" add docs/foreign.md                 # 다른 세션이 스테이지해 둠
echo mine > "$R/docs/mine.md"
run "$R" "docs: 내 것만" docs/mine.md; check "남의 스테이징이 있어도 통과" 0 "$?"
check "커밋에는 내 파일만" "docs/mine.md" "$(git -C "$R" show --name-only --format= HEAD | tr -d '\n')"
check "남의 스테이징은 보존" "docs/foreign.md" "$(cd "$R" && git diff --cached --name-only | grep foreign)"

# 4) 네거티브 — 담길 변경이 없으면 커밋하지 않는다(조용한 pathspec 실패 방지)
R="$TMPROOT/r3"; mk_repo "$R"
before="$(git -C "$R" rev-parse HEAD)"
run "$R" "빈 커밋 시도" docs/none.md; check "변경 없으면 1로 중단" 1 "$?"
check "HEAD가 그대로" "$before" "$(git -C "$R" rev-parse HEAD)"

# 5) 네거티브 — 메시지 없이 부르면 사용법 오류
R="$TMPROOT/r4"; mk_repo "$R"
echo x > "$R/docs/x.md"
(cd "$R" && printf '' | bash loop/safe_commit.sh docs/x.md >/dev/null 2>&1)
check "빈 메시지는 2로 거부" 2 "$?"
(cd "$R" && printf 'msg\n' | bash loop/safe_commit.sh >/dev/null 2>&1)
check "경로 없으면 2로 거부" 2 "$?"

# 6) 삭제도 정상 처리 — 실제로 지운 파일은 삭제 커밋이 된다
R="$TMPROOT/r5"; mk_repo "$R"
echo gone > "$R/docs/gone.md"
run "$R" "docs: 추가" docs/gone.md
rm "$R/docs/gone.md"
run "$R" "docs: 삭제" docs/gone.md; check "삭제 커밋도 0" 0 "$?"
check "HEAD에서 사라졌다" "" "$(git -C "$R" ls-files docs/gone.md)"

echo "----------------------------------------"
echo "통과 ${PASS} · 실패 ${FAIL}"
[ "$FAIL" -eq 0 ]
