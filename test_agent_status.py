#!/usr/bin/env python3
import sys, os
sys.path.insert(0, 'projects/ai-team/skills/영숙_비서/tools')

try:
    import agent_controller
    status = agent_controller.get_agent_status()
    print("=== 에이전트 현황 ===")
    print(status)
except Exception as e:
    print(f"❌ 오류: {e}")
    import traceback
    traceback.print_exc()
