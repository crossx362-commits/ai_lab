"""보드용 진행률과 AI 표시. 읽은 근거만 집계하며 작업/설정을 바꾸지 않는다."""
from __future__ import annotations
import json
import re
import subprocess
import time
from pathlib import Path
from .providers import TTL_SEC

NAMES = {'codex':'Codex', 'astra':'Astra', 'claude':'Claude', 'grok':'Grok', 'ollama':'Ollama'}
REASONS = {'AUTH_REQUIRED':'로그인 필요', 'UNAVAILABLE':'연결 안 됨', 'DISABLED':'사용 중지',
           'RATE_LIMITED':'사용량 한도', 'ERROR':'상태 확인 실패'}


def progress(tasks):
    total = len(tasks)
    done = sum(t.get('status') == 'DONE' and t.get('verdict') == 'PASS' for t in tasks)
    running = sum(t.get('status') in ('RUNNING','TESTING','REVIEW') for t in tasks)
    blocked = sum(t.get('status') in ('BLOCKED','FAILED','STOPPED','INTERRUPTED','BLOCKED_CLOUD_REQUIRED') for t in tasks)
    return {'done':done, 'total':total, 'running':running, 'blocked':blocked,
            'waiting':total-done-running-blocked, 'percent':round(done*100/total,1) if total else None}


def actor(proc, attempts):
    sp = str(proc.get('stdout_path') or '')
    if '.review-' in sp:
        return sp.split('.review-',1)[1].split('.',1)[0], '리뷰'
    if proc.get('kind') == 'agent':
        who = attempts.get(proc.get('attempt_id'))
        if not who:
            try:
                who = Path(json.loads(proc['cmd'])[0]).name
            except (ValueError, TypeError, KeyError, IndexError):
                who = '?'
        return who, '구현'
    if proc.get('kind') == 'command':
        return 'codex', '명령 처리'
    return None, {'unity':'컴파일', 'unity-test':'테스트', 'unity-gate':'검증', 'blender':'Blender 검증'}.get(proc.get('kind'),'실행')


def matches_process(proc, observed, now):
    """PID, 실행 파일, 시작시각을 대조한다. PID만 맞는 옛 기록은 실행 중으로 표시하지 않는다."""
    try:
        elapsed, command = observed.strip().split(None,1)
        argv = json.loads(proc['cmd'])
        exe = str(argv[0])
        name = Path(exe).name
        executable_matches = command.startswith(exe+' ') or command == exe or bool(re.match(r'^(?:\S*/)?'+re.escape(name)+r'(?:\s|$)',command))
        executable_matches = executable_matches or bool(re.match(r'^(?:\S*/)?(?:node|nodejs|python(?:\d(?:\.\d+)?)?)\s+(?:\S*/)?'+re.escape(name)+r'(?:\s|$)',command))
        day, clock = elapsed.split('-',1) if '-' in elapsed else ('0',elapsed)
        parts = [int(n) for n in clock.split(':')]
        seconds = int(day)*86400 + sum(n * (60**i) for i,n in enumerate(reversed(parts)))
        age = now - float(proc['started_at'])
        return executable_matches and abs(seconds-age)<10
    except (ValueError, TypeError, KeyError, IndexError):
        return False


def verified_processes(processes):
    pids = sorted({int(p['pid']) for p in processes if p.get('pid')})
    if not pids:
        return []
    try:
        r = subprocess.run(['ps','-p',','.join(map(str,pids)),'-o','pid=,etime=,command='],
                           capture_output=True,text=True,timeout=3)
    except (OSError, subprocess.SubprocessError):
        return []
    observed = {}
    for line in r.stdout.splitlines():
        bits = line.strip().split(None,1)
        if len(bits)==2 and bits[0].isdigit():
            observed[int(bits[0])] = bits[1]
    now = time.time()
    return [p for p in processes if matches_process(p,observed.get(p.get('pid'),''),now)]


def ai_state(configured, cached, activities, *, now=None):
    now = time.time() if now is None else now
    out = []
    for name, cfg in configured.items():
        if name.startswith('nc') or cfg.get('type')=='script':
            continue
        jobs = [a['label'] for a in activities if a.get('ai')==name]
        row = cached.get(name,{})
        checked = row.get('checked_at') or 0
        age = max(0, now-checked) if checked else None
        fresh = age is not None and age < TTL_SEC
        cooldown = max(0,int((row.get('cooldown_until') or 0)-now))
        enabled = cfg.get('enabled',True)
        state = row.get('state','UNKNOWN')
        if jobs:
            power,activity,reason = 'ON','작업 중','실행 프로세스 확인'
        elif not enabled:
            power,activity,reason = 'OFF','사용 중지','설정에서 사용 중지'
        elif cooldown:
            power,activity,reason = 'OFF','한도 대기',f'{(cooldown+59)//60}분 후 재확인'
        elif not fresh:
            power,activity,reason = 'UNKNOWN','확인 필요','상태 새로 확인 필요'
        elif state in ('ERROR','UNKNOWN'):
            power,activity,reason = 'UNKNOWN','확인 필요',REASONS.get(state,'상태 확인 필요')
        elif state in ('AVAILABLE','LIMITED'):
            power,activity,reason = 'ON','대기','사용 가능' if state=='AVAILABLE' else '제한적 사용 가능'
        else:
            power,activity,reason = 'OFF','연결 확인',REASONS.get(state,'상태 확인 필요')
        out.append({'name':name,'display_name':NAMES.get(name,name),'power':power,'activity':activity,
                    'reason':reason,'jobs':jobs,'checked_at':checked,'age_sec':age,
                    'enabled':enabled,'provider_state':state,'fresh':fresh,
                    'model':cfg.get('model')})
    return out
