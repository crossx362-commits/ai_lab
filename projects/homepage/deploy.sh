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

vercel deploy --prod --yes
echo "배포 완료: https://crossx362.vercel.app"
