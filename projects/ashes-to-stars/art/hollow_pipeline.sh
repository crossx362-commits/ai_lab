#!/bin/bash
# 할로우 나이트 화풍 전면 교체. p12(초상)가 끝난 뒤 이어서 돈다.
# 클로드가 만지는 직업 5×13 PNG는 다시 그리지 않는다.
set -u
ART="/Users/junholee/ai_lab/projects/ashes-to-stars/art"
cd "$ART"
log() { echo "$(date +%H:%M:%S) $*" | tee -a /tmp/hollow_pipeline.log; }
wait_pid() {
  local p=$1
  while kill -0 "$p" 2>/dev/null; do sleep 20; done
}
run_spec() {
  local spec=$1 out=$2
  echo "$spec $(date +%H:%M) pid=$$" > .generating
  log "시작 $spec → $out"
  python3 aigen.py --spec "$spec" --out-dir "$out"
  log "끝 $spec"
}

wait_pid 16122 || true
run_spec spec_p13_adv_and_rest_bg.json out_p13_adv
python3 import_p13.py || true
python3 import_hollow.py portraits || true
run_spec spec_p14_hollow_mobs.json out_p14_mobs
python3 import_hollow.py mobs || true
run_spec spec_p15_hollow_boss.json out_p15_boss
python3 import_hollow.py boss || true
run_spec spec_p16_hollow_props.json out_p16_props
python3 import_hollow.py props || true
run_spec spec_p17_hollow_fx.json out_p17_fx
python3 import_hollow.py fx || true
run_spec spec_p18_hollow_chrome.json out_p18_chrome
python3 import_hollow.py chrome || true
run_spec spec_p19_hollow_ground.json out_p19_ground
python3 import_hollow.py ground || true
rm -f .generating
log P_HOLLOW_DONE
