#!/bin/bash
# 측정·빌드 전용 유니티 사본 동기화 — 소스만 옮기고 Library는 사본 것을 그대로 둔다.
#
# 왜 사본을 쓰나: 유니티는 같은 프로젝트 폴더를 두 프로세스가 열면 Library(에셋 DB)를
# 동시에 써서 깨진다 — 그래서 배치 빌드가 exit 21로 죽는다. 사본은 자기 Library를 가지므로
# 에디터를 켠 채로 측정을 병렬로 돌릴 수 있다(2026-08-15 실측: 에디터가 unity/를 잡은
# 상태에서 unity_meas/ 배치가 exit 0).
#
# 왜 Library는 안 옮기나: 같이 덮으면 매번 전체 재임포트가 걸려 몇 분씩 날아간다.
# Assets·ProjectSettings·Packages(합쳐 12MB)만 맞추면 유니티가 바뀐 것만 증분 임포트한다.
#
# 사용: ./sync_meas.sh   → 그 뒤 unity_meas/ 로 배치 빌드·측정을 건다
set -euo pipefail
cd "$(dirname "$0")"
for d in Assets ProjectSettings Packages; do
  rsync -a --delete "unity/$d/" "unity_meas/$d/"
done
echo "동기화 완료 — unity/ → unity_meas/ (Assets·ProjectSettings·Packages)"
