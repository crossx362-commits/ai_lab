import psutil
import time

def get_system_load():
    """현재 CPU 및 RAM 사용률 반환"""
    cpu = psutil.cpu_percent(interval=0.5)
    ram = psutil.virtual_memory().percent
    return cpu, ram

def check_resource_bottleneck(cpu_limit=90, ram_limit=90):
    """자원이 부족한지 확인 (True면 병목 상태)"""
    cpu, ram = get_system_load()
    return cpu > cpu_limit or ram > ram_limit

def wait_for_resources(task_name="Heavy Task", cpu_limit=85, ram_limit=85, check_interval=10):
    """자원이 확보될 때까지 대기"""
    while True:
        cpu, ram = get_system_load()
        if cpu < cpu_limit and ram < ram_limit:
            break
        print(f"  [리소스 대기] {task_name} 시작 보류 중... (CPU: {cpu}%, RAM: {ram}%)")
        time.sleep(check_interval)

def get_resource_report_html():
    """텔레그램 보고용 리소스 상태 요약"""
    cpu, ram = get_system_load()
    status = "🔴 위험" if cpu > 90 or ram > 90 else ("🟡 주의" if cpu > 70 or ram > 70 else "🟢 양호")
    return (
        f"🖥️ <b>시스템 리소스 현황: {status}</b>\n"
        f"- CPU 사용률: {cpu}%\n"
        f"- RAM 사용률: {ram}%"
    )

def get_heavy_processes_report(n=5):
    """부하가 높은 상위 N개 프로세스 정보 추출 (CPU/RAM 기준)"""
    proc_list = []
    for proc in psutil.process_iter(['pid', 'name', 'memory_percent']):
        try:
            proc.cpu_percent(interval=None) # CPU 측정을 위한 시작점 설정
            proc_list.append(proc)
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue

    time.sleep(0.1) # CPU 사용량 계산을 위한 최소 대기
    final_data = []
    for proc in proc_list:
        try:
            info = proc.info
            info['cpu_percent'] = proc.cpu_percent(interval=None)
            final_data.append(info)
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue

    top_cpu = sorted(final_data, key=lambda x: x.get('cpu_percent', 0), reverse=True)[:n]
    top_mem = sorted(final_data, key=lambda x: x.get('memory_percent', 0), reverse=True)[:n]

    lines = ["\n⚠️ <b>부하 유발 프로세스 목록:</b>"]
    lines.append("<i>[CPU 사용순]</i>")
    lines.extend([f"• {p['name']} (PID {p['pid']}): {p['cpu_percent']}%" for p in top_cpu if p.get('cpu_percent', 0) > 0.5])
    lines.append("<i>[메모리 사용순]</i>")
    lines.extend([f"• {p['name']} (PID {p['pid']}): {p['memory_percent']:.1f}%" for p in top_mem if p.get('memory_percent', 0) > 0.5])
    return "\n".join(lines)