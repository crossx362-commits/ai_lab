import subprocess
import sys
import json

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

def check_services():
    ps_cmd = "Get-Service | Where-Object { $_.Name -like '*AI*' -or $_.Name -like '*Scheduler*' } | Select-Object Name, Status, DisplayName | ConvertTo-Json"
    r = subprocess.run(["powershell", "-Command", ps_cmd], capture_output=True, text=True, encoding="utf-8", errors="ignore")
    print(r.stdout)

if __name__ == "__main__":
    check_services()
