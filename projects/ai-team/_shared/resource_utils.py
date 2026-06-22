"""
resource_utils.py — 시스템 CPU/RAM/디스크 상태 유틸리티
"""
import psutil


def get_resource_report_html() -> str:
    cpu = psutil.cpu_percent(interval=1)
    mem = psutil.virtual_memory()
    disk = psutil.disk_usage("/")

    def _level(pct):
        if pct >= 90:
            return "🔴"
        if pct >= 70:
            return "🟡"
        return "🟢"

    cpu_icon = _level(cpu)
    mem_icon = _level(mem.percent)
    disk_icon = _level(disk.percent)

    return (
        f"💻 <b>시스템 자원</b>\n"
        f"{cpu_icon} CPU: {cpu:.1f}%\n"
        f"{mem_icon} RAM: {mem.percent:.1f}% ({mem.used // 1024**3}GB / {mem.total // 1024**3}GB)\n"
        f"{disk_icon} Disk: {disk.percent:.1f}% ({disk.used // 1024**3}GB / {disk.total // 1024**3}GB)"
    )


def get_heavy_processes_report(top_n: int = 5) -> str:
    procs = sorted(
        psutil.process_iter(["pid", "name", "cpu_percent", "memory_percent"]),
        key=lambda p: p.info.get("cpu_percent") or 0,
        reverse=True,
    )[:top_n]

    lines = ["⚠️ <b>상위 CPU 프로세스</b>"]
    for p in procs:
        name = p.info.get("name", "?")
        cpu = p.info.get("cpu_percent") or 0
        mem = p.info.get("memory_percent") or 0
        lines.append(f"  • {name} — CPU {cpu:.1f}%, RAM {mem:.1f}%")
    return "\n".join(lines)
