import subprocess
import sys
import json

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

def get_all_non_system_processes():
    ps_cmd = "Get-CimInstance Win32_Process | Select-Object Name, CommandLine | ConvertTo-Json"
    r = subprocess.run(["powershell", "-Command", ps_cmd], capture_output=True, text=True, encoding="utf-8", errors="ignore")
    try:
        processes = json.loads(r.stdout)
        if isinstance(processes, dict):
            processes = [processes]
        for p in processes:
            name = p.get("Name", "")
            cmd = str(p.get("CommandLine", ""))
            # Filter out some common system/windows noise
            if not any(x in cmd.lower() for x in ["c:\\windows\\system32", "c:\\windows\\syswow64", "svchost", "conhost", "antivirus", "defender"]):
                if cmd.strip():
                    print(f"Name: {name} | Cmd: {cmd}")
    except Exception as e:
        print("Error parsing processes:", e)

if __name__ == "__main__":
    get_all_non_system_processes()
