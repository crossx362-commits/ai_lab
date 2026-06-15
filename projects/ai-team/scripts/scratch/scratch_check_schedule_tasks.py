import subprocess
import sys
import json

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

def get_tasks_details():
    ps_cmd = """
    Get-ScheduledTask | ForEach-Object {
        $task = $_
        $actions = $task.Actions | ForEach-Object { "$($_.Execute) $($_.Arguments)" }
        [PSCustomObject]@{
            TaskPath = $task.TaskPath
            TaskName = $task.TaskName
            Actions = ($actions -join " | ")
        }
    } | ConvertTo-Json
    """
    r = subprocess.run(["powershell", "-Command", ps_cmd], capture_output=True, text=True, encoding="utf-8", errors="ignore")
    try:
        tasks = json.loads(r.stdout)
        if isinstance(tasks, dict):
            tasks = [tasks]
        for task in tasks:
            actions = str(task.get("Actions", ""))
            if "schedule_manager" in actions.lower() or "schedule_manager" in task.get("TaskName", "").lower():
                print(f"Task: {task.get('TaskPath')}{task.get('TaskName')}")
                print(f"  Actions: {actions}")
    except Exception as e:
        print("Error parsing XML:", e)

if __name__ == "__main__":
    get_tasks_details()
