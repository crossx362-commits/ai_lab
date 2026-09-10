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
