#!/usr/bin/env bash
# 홈페이지 배포 — 캐시버스팅을 파일 해시로 자동 갱신한 뒤 Vercel 프로덕션 배포.
# 수동 sed로 ?v=N을 올리다 페이지끼리 버전이 어긋나던 문제의 근본 수정:
# 버전 = 파일 내용 해시라서 사람이 숫자를 기억하거나 맞출 필요가 없다.
set -euo pipefail
cd "$(dirname "$0")"

for asset in style.css site.js posts.js works.js; do
  [ -f "$asset" ] || continue
  h=$(git hash-object "$asset" | cut -c1-8)
  if sed --version >/dev/null 2>&1; then
    sed -i "s|$asset?v=[A-Za-z0-9]*|$asset?v=$h|g" ./*.html
  else
    sed -i '' "s|$asset?v=[A-Za-z0-9]*|$asset?v=$h|g" ./*.html
  fi
done

# 검증: 참조가 방금 계산한 해시와 다르면(패턴 불일치로 sed가 놓친 페이지) 배포 중단
for asset in style.css site.js posts.js works.js; do
  [ -f "$asset" ] || continue
  h=$(git hash-object "$asset" | cut -c1-8)
  stale=$(grep -l "$asset?v=" ./*.html | xargs grep -L "$asset?v=$h" || true)
  if [ -n "$stale" ]; then
    echo "!! $asset 버전 갱신 누락: $stale" >&2
    exit 1
  fi
done

# 프로덕션 배포. --prod가 자동으로 갱신하는 건 «프로덕션 도메인»뿐이고,
# 수동으로 붙인 별칭(homepage-two-opal 등)은 옛 배포에 그대로 남는다.
# 2026-09-20 사고: 그 별칭 하나만 3주 묵은 배포를 가리켜 오너가 옛 사이트를 봤다.
# 그래서 별칭을 전부 명시해 다시 붙이고, 마지막에 «실제로» 새 내용이 뜨는지 확인한다.
OUT=$(vercel deploy --prod --yes 2>&1)
echo "$OUT" | tail -3
DEPLOY_URL=$(echo "$OUT" | grep -oE 'homepage-[a-z0-9]+-crossx362-s-projects\.vercel\.app' | head -1)
if [ -z "$DEPLOY_URL" ]; then
  echo "!! 배포 URL을 못 읽었다 — 별칭 갱신 불가. vercel 출력 형식 변경 의심." >&2
  exit 1
fi

ALIASES="crossx362.vercel.app homepage-two-opal.vercel.app homepage-drab-eta-68.vercel.app homepage-crossx362-s-projects.vercel.app"
for a in $ALIASES; do
  vercel alias set "$DEPLOY_URL" "$a" >/dev/null 2>&1 || echo "!! 별칭 실패: $a" >&2
done

# 검증: 방금 올린 «내용»이 모든 주소에서 실제로 보이는지.
# 200/404로 보면 안 된다 — 옛 배포에도 있던 파일이면 옛 사이트가 그대로 초록불이 된다
# (2026-09-20 사고가 정확히 그 모양이었다). 로컬 파일 해시와 대조해야 «새 배포»를 판별한다.
LOCAL=$(shasum -a1 index.html | cut -d' ' -f1)
fail=0
for a in $ALIASES; do
  for try in 1 2 3 4 5 6 7 8; do
    REMOTE=$(curl -s "https://$a/index.html?cb=$RANDOM" | shasum -a1 | cut -d' ' -f1)
    [ "$REMOTE" = "$LOCAL" ] && break
  done
  if [ "$REMOTE" = "$LOCAL" ]; then echo "  ok  https://$a"; else echo "  !! https://$a — 내용이 로컬과 다르다(옛 배포): ${REMOTE:0:8} != ${LOCAL:0:8}" >&2; fail=1; fi
done
[ "$fail" = "0" ] || { echo "!! 일부 주소가 새 배포를 안 가리킨다." >&2; exit 1; }

echo "배포 완료: https://crossx362.vercel.app ($DEPLOY_URL)"
