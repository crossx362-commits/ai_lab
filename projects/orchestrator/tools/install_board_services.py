#!/usr/bin/env python3
"""맥 사용자 launchd에 보드와 영속 명령 소비자를 등록한다. 기존 작업 실행기는 보존한다."""
import os
from pathlib import Path
import plistlib
import signal
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]

def main():
    sys.stdout.reconfigure(encoding='utf-8')
    domain = f'gui/{os.getuid()}'
    folder = Path.home() / 'Library' / 'LaunchAgents'
    folder.mkdir(parents=True, exist_ok=True)
    (ROOT / 'logs').mkdir(exist_ok=True)
    for suffix, args in [('board', [str(ROOT / 'tools/board.py')]),
                         ('commands', ['-m', 'orch_core.board_commands'])]:
        label = f'com.ailab.orchestrator.{suffix}'
        if subprocess.run(['launchctl', 'print', f'{domain}/{label}'], capture_output=True).returncode == 0:
            print(f'{label}: 이미 등록됨 (실행 중 작업 보존)')
            continue
        if suffix == 'board':
            listeners = subprocess.run(['/usr/sbin/lsof', '-t', '-iTCP:8767', '-sTCP:LISTEN'], capture_output=True, text=True)
            for pid_text in listeners.stdout.split():
                pid = int(pid_text)
                command = subprocess.check_output(['ps', '-p', str(pid), '-o', 'command='], text=True).strip()
                if not (command.endswith('projects/orchestrator/tools/board.py') or command.endswith(str(ROOT / 'tools/board.py'))):
                    raise RuntimeError(f'8767 포트의 다른 서비스는 종료하지 않습니다: PID {pid}')
                os.kill(pid, signal.SIGTERM)
                deadline = time.monotonic() + 10
                while subprocess.run(['ps', '-p', str(pid)], capture_output=True).returncode == 0:
                    if time.monotonic() >= deadline:
                        raise RuntimeError(f'보드 PID {pid} 종료 미확인')
                    time.sleep(0.1)
        path = folder / f'{label}.plist'
        data = {'Label': label, 'ProgramArguments': [sys.executable, *args],
                'WorkingDirectory': str(ROOT), 'RunAtLoad': True, 'KeepAlive': True,
                'ThrottleInterval': 10,
                'EnvironmentVariables': {'PATH': os.environ.get('PATH', '/opt/homebrew/bin:/usr/bin:/bin'), 'PYTHONUNBUFFERED': '1'},
                'StandardOutPath': str(ROOT / 'logs' / f'{suffix}-service.stdout.log'),
                'StandardErrorPath': str(ROOT / 'logs' / f'{suffix}-service.stderr.log')}
        if path.exists():
            path.with_suffix(f'.plist.backup-{time.time_ns()}').write_bytes(path.read_bytes())
        path.write_bytes(plistlib.dumps(data))
        subprocess.run(['launchctl', 'bootstrap', domain, str(path)], check=True)
        print(f'{label}: 등록됨')

if __name__ == '__main__':
    main()
