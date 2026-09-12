#!/usr/bin/env python3
"""Grok loop.sh와 같은 새 세션 → 결과 기록 → 45초 대기 방식의 Codex 루프.

예약 실행 아님. 실행한 프로세스가 다음 codex exec를 직접 시작한다.
rc=0은 세션 종료만 의미하며 게임 완료 판정은 보드의 검증 근거로 한다.
"""
import argparse
from datetime import datetime
import fcntl
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]


def card_signature(card):
    return json.dumps([card.get('status'), card.get('resume_token')], ensure_ascii=False)


def select_card(cards, deferred):
    for status in ('진행 중', '대기', '검증 중'):
        for card in cards:
            owner = card.get('owner')
            mine = owner == 'codex' or (not owner and card.get('model') in ('gpt', 'codex'))
            if mine and card.get('status') == status and deferred.get(card['id']) != card_signature(card):
                return card
    return None


def content_fingerprint(worktree):
    """문서/바퀴 수 변경은 개발 진척이 아니다. 게임 소스·에셋의 실제 변경만 구별한다."""
    paths = ['projects/ulon/unity/Assets', 'projects/ulon/assets', 'projects/ulon/art']
    def git(*args):
        return subprocess.run(['git', *args], cwd=worktree, stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE, check=True).stdout
    h = hashlib.sha256()
    h.update(git('log', '-1', '--format=%H', '--', *paths))
    h.update(git('diff', 'HEAD', '--binary', '--', *paths))
    for raw in git('ls-files', '--others', '--exclude-standard', '-z', '--', *paths).split(b'\0'):
        if raw:
            path = worktree / os.fsdecode(raw)
            if path.is_file():
                h.update(raw); h.update(path.read_bytes())
    return h.hexdigest()


def now():
    return datetime.now().astimezone().isoformat()


def atomic_json(path, data):
    temp = path.with_name(path.name + '.tmp')
    temp.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    os.replace(temp, path)


def child_env():
    # 구독 로그인은 Codex auth 저장소가 담당. API 키/토큰은 자식 환경에 전달하지 않는다.
    keep = {'PATH', 'HOME', 'USER', 'LOGNAME', 'SHELL', 'TMPDIR', 'LANG', 'LC_ALL',
            'CODEX_HOME', 'XDG_CONFIG_HOME', 'XDG_CACHE_HOME', 'SSL_CERT_FILE', 'SSL_CERT_DIR'}
    env = {k: v for k, v in os.environ.items() if k in keep}
    env['PYTHONUTF8'] = '1'
    env['PYTHONUNBUFFERED'] = '1'
    return env


def terminate(child):
    # 이 루프가 만든 자식 그룹만 종료. Grok/기존 Unity는 대상이 아니다.
    try:
        os.killpg(child.pid, signal.SIGTERM)
    except ProcessLookupError:
        return
    try:
        child.wait(timeout=8)
    except subprocess.TimeoutExpired:
        os.killpg(child.pid, signal.SIGKILL)
        child.wait()


def run(args):
    args.run_dir.mkdir(parents=True, exist_ok=True)
    with (args.run_dir / 'loop.lock').open('a+') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print('이미 Codex 루프가 실행 중입니다.', flush=True)
            return 0
        lock.seek(0); lock.truncate(); lock.write(str(os.getpid())); lock.flush()
        state_path = args.run_dir / 'runtime_state.json'
        previous = json.loads(state_path.read_text()) if state_path.exists() else {}
        deferred = previous.get('deferred_cards', {})
        iteration = int(previous.get('loop', previous.get('iteration', 1)))
        state = {'loop': iteration, 'pid': os.getpid(), 'pgid': os.getpgrp(),
                 'child_pid': None, 'consec_fail': 0, 'status': 'starting',
                 'started_at': now(), 'worktree': str(args.worktree), 'mode': 'headless_process_loop',
                 'deferred_cards': deferred}
        interrupted = False

        def stop_signal(signum, frame):
            nonlocal interrupted
            interrupted = True

        signal.signal(signal.SIGTERM, stop_signal)
        signal.signal(signal.SIGINT, stop_signal)

        def save(**fields):
            state.update(fields, heartbeat=now())
            atomic_json(state_path, state)

        completed = 0
        save()
        try:
            while True:
                if interrupted or args.stop_file.exists():
                    save(status='owner_stopped', child_pid=None, ended_at=now())
                    return 0
                if args.max_loops and completed >= args.max_loops:
                    save(status='max_loops', child_pid=None, ended_at=now())
                    return 0
                try:
                    cards = json.loads(args.board.read_text(encoding='utf-8'))['cards']
                except (OSError, ValueError, KeyError):
                    # 보드가 깨지거나 쓰는 중이면 이전 내용으로 되돌리거나 CLI를 호출하지 않는다.
                    save(status='waiting_board', child_pid=None)
                    time.sleep(1)
                    continue
                selected = select_card(cards, deferred)
                if selected is None:
                    save(status='waiting_work', child_pid=None, current_task='실행 가능한 담당 카드 없음')
                    time.sleep(1)
                    continue
                before = content_fingerprint(args.worktree)
                assigned_signature = card_signature(selected)
                iteration += 1
                log_path = args.run_dir / f'loop_{iteration:04d}.jsonl'
                last_path = args.run_dir / f'loop_{iteration:04d}.result.md'
                prompt = args.prompt.read_text(encoding='utf-8')
                prompt += ('\n\n부모가 선택한 이번 작업 카드: ' + json.dumps(selected, ensure_ascii=False) +
                           '\n이번 바퀴는 이 카드의 실제 구현 또는 검증을 수행한다. 다른 막힌 카드의 상태 점검으로 '
                           '대체하지 마라. 이전 검증의 미완료는 이 카드의 독립 구현을 막지 않는다. '
                           '진행 불가능하면 정확한 재개 조건을 해당 카드에 기록하고 막힘으로 전환하라.\n')
                prompt += ('\n\n이번 Codex 바퀴는 #' + str(iteration) +
                           '이다. 이 세션에서 한 바퀴만 수행하고 종료하라. 다음 바퀴는 부모 프로세스가 시작한다. '
                           '예약 작업·자동 작업을 만들거나 다른 codex exec를 실행하지 마라. '
                           '결과는 공유 보드에만 보고하고 최종 응답은 내부 로그용 한 줄로 남겨라.\n')
                command = [args.cli, 'exec', '-C', str(args.worktree), '-s', 'danger-full-access',
                           '-c', 'approval_policy="never"', '--json', '--color', 'never',
                           '-o', str(last_path), '-']
                started = time.monotonic()
                save(loop=iteration, status='running', started_at=now(), ended_at='',
                     log_path=str(log_path), result_path=str(last_path), wait_remaining_sec=0,
                     current_task=selected['id'], result='running', rc=None)
                with log_path.open('w', encoding='utf-8') as output:
                    child = subprocess.Popen(command, cwd=args.worktree, stdin=subprocess.PIPE,
                                             stdout=output, stderr=subprocess.STDOUT, text=True,
                                             encoding='utf-8', env=child_env(), start_new_session=True)
                    save(child_pid=child.pid)
                    rc = None
                    try:
                        child.stdin.write(prompt)
                        child.stdin.close()
                        while child.poll() is None:
                            if interrupted:
                                terminate(child); rc = 130; break
                            if time.monotonic() - started >= args.timeout:
                                terminate(child); rc = 124; break
                            save()
                            time.sleep(.1 if args.timeout < 5 else 1)
                        if rc is None:
                            rc = child.wait()
                    finally:
                        if child.poll() is None:
                            terminate(child)
                result = 'session_finished' if rc == 0 else ('timeout' if rc == 124 else 'session_failed')
                changed = content_fingerprint(args.worktree) != before
                if rc == 0 and not changed:
                    result = 'no_progress'
                    # 동일 조건에서 다음 세션도 같은 읽기만 반복하는 것을 막는다.
                    try:
                        latest = json.loads(args.board.read_text())['cards']
                        selected = next((c for c in latest if c['id'] == selected['id']), selected)
                    except (OSError, ValueError, KeyError):
                        pass
                    if card_signature(selected) == assigned_signature:
                        deferred[selected['id']] = assigned_signature
                failures = 0 if rc == 0 else state['consec_fail'] + 1
                row = {'loop': iteration, 'started_at': state['started_at'], 'ended_at': now(),
                       'result': result, 'rc': rc, 'child_pid': child.pid,
                       'card_id': selected['id'], 'content_changed': changed,
                       'elapsed_sec': round(time.monotonic() - started, 2),
                       'log_path': str(log_path), 'result_path': str(last_path)}
                with (args.run_dir/'history.jsonl').open('a', encoding='utf-8') as history:
                    history.write(json.dumps(row, ensure_ascii=False) + '\n')
                save(status='waiting', child_pid=None, result=result, rc=rc,
                     ended_at=row['ended_at'], consec_fail=failures)
                completed += 1
                if failures >= args.max_failures:
                    save(status='stopped_fail')
                    return 1
                deadline = time.monotonic() + args.pause
                while time.monotonic() < deadline and not interrupted and not args.stop_file.exists():
                    save(wait_remaining_sec=max(0, int(deadline-time.monotonic())))
                    time.sleep(min(1, max(0, deadline-time.monotonic())))
        except Exception as exc:
            save(status='runner_error', child_pid=None, error=str(exc), ended_at=now())
            raise


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--cli', default='/opt/homebrew/bin/codex')
    p.add_argument('--worktree', type=Path, default=REPO/'.worktrees/ulon-codex-art')
    p.add_argument('--prompt', type=Path, default=ROOT/'loop/CODEX_PROMPT.md')
    p.add_argument('--board', type=Path, default=ROOT/'docs/board.json')
    p.add_argument('--run-dir', type=Path, default=REPO/'output/ulon-codex')
    p.add_argument('--stop-file', type=Path, default=ROOT/'loop/CODEX_STOP')
    p.add_argument('--pause', type=float, default=45)
    p.add_argument('--timeout', type=float, default=90*60)
    p.add_argument('--max-failures', type=int, default=3)
    p.add_argument('--max-loops', type=int, default=0)
    args = p.parse_args()
    if args.pause < 0 or args.timeout <= 0 or args.max_failures < 1 or args.max_loops < 0:
        p.error('대기/제한 설정이 올바르지 않습니다.')
    return run(args)


if __name__ == '__main__':
    raise SystemExit(main())
