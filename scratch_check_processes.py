import subprocess
import sys
import json

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

def get_all_python_processes():
    ps_cmd = "Get-CimInstance Win32_Process | Select-Object ProcessId, Name, CommandLine | ConvertTo-Json"
    r = subprocess.run(["powershell", "-Command", ps_cmd], capture_output=True, text=True, encoding="utf-8", errors="ignore")
    try:
        processes = json.loads(r.stdout)
        # If it's a single dict, wrap in a list
        if isinstance(processes, dict):
            processes = [processes]
        for p in processes:
            cmd = str(p.get("CommandLine", ""))
            if "python" in cmd.lower() or "ai-lab" in cmd.lower() or "yeongsuk" in cmd.lower():
                print(f"PID: {p.get('ProcessId')} | Name: {p.get('Name')} | Cmd: {cmd}")
    except Exception as e:
        print("Error parsing processes:", e)

if __name__ == "__main__":
    get_all_python_processes()
