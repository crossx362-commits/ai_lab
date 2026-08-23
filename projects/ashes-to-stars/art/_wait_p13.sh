#!/bin/bash
set -e
ART="/Users/junholee/ai_lab/projects/ashes-to-stars/art"
cd "$ART"
PID=16122
while kill -0 $PID 2>/dev/null; do sleep 20; done
echo "spec_p13_adv_and_rest_bg.json $(date +%H:%M) pid=$$" > .generating
python3 aigen.py --spec spec_p13_adv_and_rest_bg.json --out-dir out_p13_adv
python3 import_p13.py
rm -f .generating
echo P13_DONE
