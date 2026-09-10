#!/usr/bin/env bash
# 임시 저장소 + 가짜 원격 + 가짜 디스패치(CLI 한도 안 태움). 실 BOARD.md는 건드리지 않는다.
set -euo pipefail
H="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$H/../../.." && pwd)"
T="${HARNESS_DIR:-$H/.tmp}/tmprepo"; R="${HARNESS_DIR:-$H/.tmp}/remote.git"
mkdir -p "$(dirname "$T")"
rm -rf "$T" "$R"
git init -q --bare "$R"
mkdir -p "$T/loop/opinions" "$T/projects/petnna"
cp "$REPO/loop/BOARD.md" "$T/loop/BOARD.md"
# **미답 결정대기 카드를 픽스처로 심는다**(오너 지시 2026-09-10). 실 BOARD.md를 그대로 쓰면
# 그날 미답 카드가 0건일 때 「결정대기 카드 있음」과 「커밋 수 증가(≥7, 판정 하나가 빠진다)」가
# 코드가 아니라 **데이터 때문에** 실패한다. 하네스는 실데이터에 기대면 안 된다.
awk '
  /^## 결정대기[[:space:]]*$/ { print; inpend = 1; blanks = 0; next }
  inpend && /^[[:space:]]*$/ { blanks++; next }
  inpend && /^## / {
    print "- 하네스 픽스처 — 미답 결정대기 카드(실데이터와 무관)";
    for (i = 0; i < blanks; i++) print "";
    inpend = 0; print; next
  }
  inpend { for (i = 0; i < blanks; i++) print ""; blanks = 0; print; next }
  { print }
  END { if (inpend) print "- 하네스 픽스처 — 미답 결정대기 카드(실데이터와 무관)" }
' "$T/loop/BOARD.md" > "$T/loop/BOARD.md.tmp" && mv "$T/loop/BOARD.md.tmp" "$T/loop/BOARD.md"
grep -q '^- 하네스 픽스처 — 미답 결정대기 카드' "$T/loop/BOARD.md" || { echo "픽스처 심기 실패 — 실 BOARD.md에 '## 결정대기' 절이 없다" >&2; exit 1; }
cp "$REPO/loop/opinion_normalize.py" "$T/loop/"
printf 'loop/opinions/*\n!loop/opinions/*.md\nloop/opinions/_*\nloop/opinions/archive/\n' > "$T/.gitignore"
echo "# petnna" > "$T/projects/petnna/README.md"
# 가짜 디스패치: 10초 running → 의견 4개(하나는 실패) 기록 → idle. 실물과 같은 파일 규약.
cat > "$T/loop/dispatch-board.sh" <<'EOF'
#!/usr/bin/env bash
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; OUT="$ROOT/loop/opinions"
if [ -n "${DISPATCH_LOG:-}" ]; then exec >>"$DISPATCH_LOG" 2>&1; fi
ts(){ date '+%Y-%m-%d %H:%M:%S'; }
echo "running $(ts)" > "$OUT/_dispatch.status"; echo "[stub] start $(ts)"
for w in Grok GPT 제미니 Claude; do echo "running $(ts)" > "$OUT/$w.status"; done
sleep 10
printf '%s\n' "그록 제목 — 로그 회전" "loop/opinions/_dispatch.log가 무한히 자란다." "60줄만 남기고 잘라라." > "$OUT/Grok.md"; echo "ok $(ts)" > "$OUT/Grok.status"
printf '%s\n' "클로드 제목 — 상태 파일 잠금" "동시 쓰기 경합 가능." "flock을 써라." > "$OUT/Claude.md"; echo "ok $(ts)" > "$OUT/Claude.status"
printf '%s\n' "제미니 제목 — 빈 명령 방어" "공백 명령이 들어오면 CLI가 빈 프롬프트로 돈다." "trim 후 거절." > "$OUT/제미니.md"; echo "ok $(ts)" > "$OUT/제미니.status"
printf '%s\n' "GPT 실패" > "$OUT/GPT.md"; echo "fail $(ts)" > "$OUT/GPT.status"; echo "ERROR: You've hit your usage limit." > "$OUT/GPT.err"
cd "$ROOT" && git add -A -- loop/opinions && git commit -q -m "opinions: stub" -- loop/opinions && git push -q origin master || true
echo "idle $(ts)" > "$OUT/_dispatch.status"; echo "[stub] done $(ts)"
EOF
cd "$T"
git init -q -b master
git config user.email harness@local; git config user.name harness
git add -A && git commit -q -m "harness: seed"
git remote add origin "$R" && git push -q -u origin master
echo "seed $(git rev-parse --short HEAD) at $T"
