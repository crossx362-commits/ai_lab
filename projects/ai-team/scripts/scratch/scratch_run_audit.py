import sys
import os

# Reconfigure stdout to utf-8
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

sys.path.insert(0, r"d:\ai-lab\projects\ai-team")
sys.path.insert(0, r"d:\ai-lab\projects\ai-team\skills\예원_CEO\tools")

import skill_auditor

if __name__ == "__main__":
    sys.argv = ["skill_auditor.py", "--check"]
    skill_auditor.run_audit()
