import subprocess
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

def get_tasks_details():
    ps_cmd = "Get-ScheduledTask | Where-Object { $_.TaskName -like '*AI*' } | Get-ScheduledTaskInfo | Select-Object TaskName, LastRunTime, LastTaskResult | ConvertTo-Json"
    r = subprocess.run(["powershell", "-Command", ps_cmd], capture_output=True, text=True, encoding="utf-8", errors="ignore")
    print(r.stdout)

if __name__ == "__main__":
    get_tasks_details()
