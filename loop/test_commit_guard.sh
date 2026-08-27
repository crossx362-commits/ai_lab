#!/bin/bash
# commit_guard 샌드박스 자동검사 — 실제 임시 git 저장소를 만들어 통과·차단을 실측한다.
# 사용법: bash loop/test_commit_guard.sh   (종료 코드 0 = 전부 통과)
set -u

GUARD="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/commit_guard.sh"
PASS=0
FAIL=0

run_in() { # run_in <저장소> <명령...> — 해당 저장소 루트에서 명령 실행
  local repo="$1"; shift
  (cd "$repo" && "$@")
}

guard_in() { # guard_in <저장소> <허용경로...>
  local repo="$1"; shift
  (cd "$repo" && bash "$GUARD" "$@")
}

expect() { # expect <기대코드> <설명> <명령...>
  local want="$1" desc="$2"; shift 2
  local got=0
  "$@" >/dev/null 2>&1 || got=$?
  if [ "$got" -eq "$want" ]; then
    echo "ok   - $desc"
    PASS=$((PASS + 1))
  else
    echo "FAIL - $desc (기대 $want, 실제 $got)"
    FAIL=$((FAIL + 1))
  fi
}

output_has() { # output_has <설명> <패턴> <명령...>
  local desc="$1" pat="$2"; shift 2
  local out
  out="$("$@" 2>&1 || true)"
  if printf '%s' "$out" | grep -q "$pat"; then
    echo "ok   - $desc"
    PASS=$((PASS + 1))
  else
    echo "FAIL - $desc (출력에 '$pat' 없음)"
    FAIL=$((FAIL + 1))
  fi
}

TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/commit_guard_test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

mk_repo() { # $1 = 디렉터리
  git init -q "$1"
  git -C "$1" config user.email t@t
  git -C "$1" config user.name t
  mkdir -p "$1/loop" "$1/docs"
  echo base > "$1/base.txt"
  git -C "$1" add base.txt
  git -C "$1" commit -qm init
}

# 1) 포지티브: 내 파일만 스테이지 → 통과(0)
R="$TMPROOT/r1"; mk_repo "$R"
echo mine > "$R/docs/mine.md"
git -C "$R" add docs/mine.md
expect 0 "내 파일만 스테이지면 통과" guard_in "$R" docs/mine.md
echo x > "$R/loop/x.md"
git -C "$R" add loop/x.md
expect 0 "허용 경로를 여럿 선언해도 전부 일치하면 통과" guard_in "$R" docs/mine.md loop/x.md
expect 2 "인수 없으면 사용법 오류(2)" guard_in "$R"

# 2) 네거티브: 타 세션 스테이징 혼입 → 차단(1) + 경로 지목
R="$TMPROOT/r2"; mk_repo "$R"
echo foreign > "$R/docs/foreign.md"
echo mine > "$R/docs/mine.md"
git -C "$R" add docs/foreign.md docs/mine.md
expect 1 "남의 스테이징이 섞이면 차단" guard_in "$R" docs/mine.md
output_has "차단 시 혼입 경로를 지목" "docs/foreign.md" guard_in "$R" docs/mine.md

# 3) 네거티브: 선언했지만 스테이지 안 한 경로 → 차단(1)
R="$TMPROOT/r3"; mk_repo "$R"
echo mine > "$R/docs/mine.md"
git -C "$R" add docs/mine.md
expect 1 "선언 미스매치도 차단" guard_in "$R" docs/mine.md docs/forgot.md
output_has "미스매치 시 누락 경로를 지목" "docs/forgot.md" guard_in "$R" docs/mine.md docs/forgot.md

# 4) 네거티브: 빈 인덱스 → 차단(1, 조용한 pathspec 실패 방지)
R="$TMPROOT/r4"; mk_repo "$R"
expect 1 "빈 인덱스는 커밋할 게 없어 차단" guard_in "$R" docs/whatever.md

# 5) temp-index 흐름: 공유 인덱스는 오염됐고, 검사 대상 인덱스는 GIT_INDEX_FILE로 분리
R="$TMPROOT/r5"; mk_repo "$R"
echo foreign > "$R/docs/foreign.md"
git -C "$R" add docs/foreign.md                        # 공유 인덱스 오염(사고 상황 재현)
cp "$R/.git/index" "$R/dirty-index"                    # 오염된 공유 인덱스 스냅샷
expect 0 "오염 인덱스를 GIT_INDEX_FILE로 찍어 검사 가능" \
  run_in "$R" env GIT_INDEX_FILE="$R/dirty-index" bash "$GUARD" docs/foreign.md
expect 1 "같은 오염 인덱스에 남의 경로 선언하면 차단" \
  run_in "$R" env GIT_INDEX_FILE="$R/dirty-index" bash "$GUARD" docs/mine.md
echo mine > "$R/docs/mine.md"
BLOB=$(git -C "$R" hash-object -w "$R/docs/mine.md")
export GIT_INDEX_FILE="$R/.git/tmpidx"
git -C "$R" read-tree HEAD
git -C "$R" update-index --add --cacheinfo "100644,$BLOB,docs/mine.md"
expect 0 "temp-index에는 내 분만 있으면 통과" guard_in "$R" docs/mine.md
unset GIT_INDEX_FILE
expect 1 "env 없이 공유 인덱스로 돌리면 오염이 드러나 차단" guard_in "$R" docs/mine.md

# 6) 낡은 스냅 스테이징 (2026-08-26 사고 재현) — 경로는 맞는데 내용이 되돌아간다
R="$TMPROOT/r6"; mk_repo "$R"
echo v1 > "$R/docs/mine.md"
git -C "$R" add docs/mine.md
git -C "$R" commit -qm v1                              # HEAD = v1
echo v2 > "$R/docs/mine.md"
git -C "$R" add docs/mine.md
git -C "$R" commit -qm v2                              # HEAD = v2 (남이 진전시킴)
git -C "$R" update-index --cacheinfo "100644,$(git -C "$R" hash-object -w <(echo v1)),docs/mine.md"
expect 1 "낡은 블롭이 스테이지되면 차단(되돌림 방지)" guard_in "$R" docs/mine.md
output_has "차단 시 낡은 경로를 지목" "작업 트리와 다름" guard_in "$R" docs/mine.md
expect 0 "COMMIT_GUARD_ALLOW_PARTIAL=1이면 의도적 부분 스테이징 허용" \
  run_in "$R" env COMMIT_GUARD_ALLOW_PARTIAL=1 bash "$GUARD" docs/mine.md

# 7) 파일이 살아 있는데 삭제로 스테이지 (영지 아트 png 8종 사고 재현)
R="$TMPROOT/r7"; mk_repo "$R"
echo keep > "$R/docs/art.png"
git -C "$R" add docs/art.png
git -C "$R" commit -qm art
git -C "$R" rm --cached -q docs/art.png                # 파일은 디스크에 그대로 남는다
expect 1 "파일이 있는데 삭제만 스테이지되면 차단" guard_in "$R" docs/art.png
output_has "차단 시 삭제 예약을 지목" "삭제로 스테이지됨" guard_in "$R" docs/art.png
rm "$R/docs/art.png"
expect 0 "실제로 지운 파일의 삭제 커밋은 통과" guard_in "$R" docs/art.png

# 8) png 실로드 훅이 가드에 물려 있는지 (회의 20260827-081437 #1)
if grep -q -- '--png-load' "$GUARD" && grep -q 'game_asset_names.py' "$GUARD"; then
  echo "ok   - png 실로드 훅이 commit_guard 에 연결됨"
  PASS=$((PASS + 1))
else
  echo "FAIL - png 실로드 훅이 commit_guard 에 없다"
  FAIL=$((FAIL + 1))
fi

echo "----------------------------------------"
echo "통과 ${PASS} · 실패 ${FAIL}"
[ "$FAIL" -eq 0 ]
