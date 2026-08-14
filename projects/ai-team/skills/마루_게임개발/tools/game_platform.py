#!/usr/bin/env python3
"""
마루(게임 개발) — 플랫폼 의존 조각 한 곳 모음 (맥 우선, Windows 폴백)

왜 별도 파일인가:
  유니티 경로 탐색·에디터 락 판정·플레이어 실행파일 경로는
  game_build_verify.py와 game_regression.py가 **똑같이** 필요로 한다.
  같은 안전장치를 두 곳에 복붙하면 한쪽만 고쳐져 어긋난다(CLAUDE.md 반복 교훈).
  그래서 처음부터 공용 함수로 두고 호출부는 재사용만 한다.

핵심 규칙:
  - 유니티 버전은 **절대 하드코딩하지 않는다**. ProjectVersion.txt가 유일한 정답.
  - 실행 중인 에디터는 **죽이지 않는다**. 오너가 Hub로 연 에디터를 죽인 사고가 있었다
    (GAME_DEV_HANDOFF.md §5 「에디터 락 ≠ 죽여도 되는 프로세스」).
    맥에서는 무조건 보고하고 중단한다.
"""
import os
import platform
import re
import shutil
import subprocess

IS_MAC = platform.system() == "Darwin"
IS_WIN = platform.system() == "Windows"


# ---------------------------------------------------------------- 유니티 버전
def project_unity_version(unity_project):
    """ProjectVersion.txt에서 에디터 버전을 읽는다. 실패하면 None."""
    pv = os.path.join(unity_project, "ProjectSettings", "ProjectVersion.txt")
    try:
        with open(pv, encoding="utf-8", errors="replace") as f:
            for line in f:
                if line.startswith("m_EditorVersion:"):
                    return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return None


def _hub_roots():
    if IS_MAC:
        return ["/Applications/Unity/Hub/Editor",
                os.path.expanduser("~/Applications/Unity/Hub/Editor")]
    if IS_WIN:
        return [os.path.join(os.environ.get("PROGRAMFILES", r"C:\Program Files"),
                             "Unity", "Hub", "Editor")]
    return [os.path.expanduser("~/Unity/Hub/Editor")]


def _editor_binary(hub_root, version):
    """설치 루트 + 버전 → 실제 실행파일 경로 (플랫폼별 배치가 다르다)"""
    base = os.path.join(hub_root, version)
    if IS_MAC:
        return os.path.join(base, "Unity.app", "Contents", "MacOS", "Unity")
    if IS_WIN:
        return os.path.join(base, "Editor", "Unity.exe")
    return os.path.join(base, "Editor", "Unity")


def find_unity(unity_project):
    """
    프로젝트가 요구하는 버전의 유니티 에디터를 찾는다.

    반환: (경로, 메모). 경로가 None이면 메모가 실패 사유.
    """
    want = project_unity_version(unity_project)
    roots = [r for r in _hub_roots() if os.path.isdir(r)]

    if want:
        for r in roots:
            exe = _editor_binary(r, want)
            if os.path.exists(exe):
                return exe, f"Unity {want}"

    env = os.environ.get("UNITY_EDITOR_PATH")
    if env and os.path.exists(env):
        return env, f"UNITY_EDITOR_PATH={env}"

    # 요구 버전이 없으면 설치된 것 중 최신을 알려주되, 버전 불일치를 명시한다
    installed = []
    for r in roots:
        for v in os.listdir(r):
            exe = _editor_binary(r, v)
            if os.path.exists(exe):
                installed.append((v, exe))
    if not installed:
        return None, f"유니티 설치를 찾을 수 없다 (탐색: {', '.join(roots) or '없음'})"
    installed.sort(reverse=True)
    v, exe = installed[0]
    return None, (f"프로젝트가 요구하는 {want or '???'} 이 없다. "
                  f"설치된 것: {', '.join(x[0] for x in installed)} "
                  f"→ Unity Hub에서 {want}를 설치할 것 (버전을 바꿔 빌드하면 전체 재임포트가 터진다)")


# ------------------------------------------------------------------ 빌드 타겟
def build_target():
    if IS_MAC:
        return "StandaloneOSX"
    if IS_WIN:
        return "StandaloneWindows64"
    return "StandaloneLinux64"


def player_path(build_dir, stem):
    """
    빌드 산출물의 실행 경로. 맥은 .app 번들 안의 바이너리를 직접 실행한다
    (open(1)으로 띄우면 종료 코드·타임아웃을 못 잡는다).
    """
    if IS_MAC:
        app = os.path.join(build_dir, stem + ".app")
        inner = os.path.join(app, "Contents", "MacOS", stem)
        if os.path.exists(inner):
            return inner
        # 번들 안 바이너리 이름이 제품명과 다를 수 있다
        macos = os.path.join(app, "Contents", "MacOS")
        if os.path.isdir(macos):
            for f in sorted(os.listdir(macos)):
                p = os.path.join(macos, f)
                if os.access(p, os.X_OK) and os.path.isfile(p):
                    return p
        return inner
    if IS_WIN:
        return os.path.join(build_dir, stem + ".exe")
    return os.path.join(build_dir, stem)


# -------------------------------------------------------------- 프로세스 조회
def list_processes():
    """(pid, 커맨드라인) 목록. psutil이 있으면 그걸, 없으면 ps/wmic."""
    try:
        import psutil
        out = []
        for p in psutil.process_iter(["pid", "cmdline", "name"]):
            info = p.info
            cmd = " ".join(info.get("cmdline") or []) or (info.get("name") or "")
            out.append((info["pid"], cmd))
        return out
    except Exception:
        pass

    if IS_WIN:
        try:
            r = subprocess.run(
                ["wmic", "process", "get", "ProcessId,CommandLine", "/format:csv"],
                capture_output=True, text=True, encoding="utf-8", errors="replace",
                timeout=30)
        except Exception:
            return []
        out = []
        for line in r.stdout.splitlines():
            parts = line.split(",")
            if len(parts) < 3 or not parts[-1].strip().isdigit():
                continue
            out.append((int(parts[-1].strip()), ",".join(parts[1:-1]).strip()))
        return out

    try:
        r = subprocess.run(["ps", "-Ao", "pid,command"],
                           capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=30)
    except Exception:
        return []
    out = []
    for line in r.stdout.splitlines()[1:]:
        line = line.strip()
        m = re.match(r"(\d+)\s+(.*)", line)
        if m:
            out.append((int(m.group(1)), m.group(2)))
    return out


# 에디터 실행파일만 잡는다. Unity Hub("/Applications/Unity Hub.app/…")는 에디터가 아니며
# 락과도 무관하다 — 처음 짠 느슨한 매칭이 Hub 헬퍼를 에디터로 오인해 빌드를 막았다(실측 확인).
_EDITOR_RE = (re.compile(r"[\\/]unity\.exe(\s|$)", re.I) if IS_WIN
              else re.compile(r"/unity\.app/contents/macos/unity(\s|$)|/editor/unity(\s|$)", re.I))


def _is_unity_editor(cmd):
    if "unity hub" in cmd.lower():
        return False
    return bool(_EDITOR_RE.search(cmd.replace("\\", "/")))


def running_unity_editors(unity_project):
    """
    이 프로젝트를 붙잡고 있는 유니티 에디터 프로세스 목록.

    다른 프로젝트를 연 에디터는 우리 배치 빌드를 막지 않으므로 제외한다
    (오너가 다른 프로젝트를 열어둔 것만으로 게임 빌드가 중단되면 안 된다).
    """
    target = os.path.normpath(os.path.abspath(unity_project)).replace("\\", "/").lower()
    found = []
    for pid, cmd in list_processes():
        if not _is_unity_editor(cmd):
            continue
        low = cmd.replace("\\", "/").lower()
        if target in low:
            found.append((pid, cmd))
        elif "-projectpath" not in low:
            # 어느 프로젝트인지 알 수 없다 — 죽이지 않고 사람에게 보고한다
            found.append((pid, cmd))
    return found


def ensure_no_editor_lock(unity_project):
    """
    배치 빌드 전 락 점검.

    반환: (진행해도 되나, 메시지)

    **에디터를 죽이지 않는다.** 오너가 열어둔 에디터를 taskkill로 날린 사고가 있어
    (핸드오프 §5), 맥에서는 발견 즉시 보고하고 중단한다.
    Windows에서도 사람이 연 에디터는 건드리지 않고, 명백히 우리가 남긴
    `-batchmode` 잔재만 정리한다.
    """
    editors = running_unity_editors(unity_project)
    if not editors:
        return True, "에디터 락 없음"

    human = [(p, c) for p, c in editors if "-batchmode" not in c.lower()]
    stale = [(p, c) for p, c in editors if "-batchmode" in c.lower()]

    if human:
        lines = [f"  pid={p} {c[:160]}" for p, c in human[:5]]
        return False, ("사람이 연 것으로 보이는 유니티 에디터가 이 프로젝트를 잡고 있다 — "
                       "**죽이지 않고 중단한다**. 에디터를 직접 닫은 뒤 다시 실행할 것.\n"
                       + "\n".join(lines))

    if stale and IS_WIN:
        for pid, _ in stale:
            subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True)
        return True, f"배치 잔재 {len(stale)}건 정리(Windows)"

    lines = [f"  pid={p} {c[:160]}" for p, c in stale[:5]]
    return False, ("배치모드 유니티가 이미 돌고 있다 — 중복 실행을 피해 중단한다.\n"
                   + "\n".join(lines))


def terminate_player(exe_path):
    """타임아웃된 **우리 플레이어**만 종료 (에디터는 절대 대상 아님)"""
    stem = os.path.basename(exe_path)
    if IS_WIN:
        subprocess.run(["taskkill", "/IM", stem, "/F"], capture_output=True)
        return
    subprocess.run(["pkill", "-f", exe_path], capture_output=True)


# ------------------------------------------------------------------- 블렌더
def find_blender():
    if IS_MAC:
        cands = ["/Applications/Blender.app/Contents/MacOS/Blender",
                 os.path.expanduser("~/Applications/Blender.app/Contents/MacOS/Blender")]
    elif IS_WIN:
        pf = os.environ.get("PROGRAMFILES", r"C:\Program Files")
        cands = []
        root = os.path.join(pf, "Blender Foundation")
        if os.path.isdir(root):
            for v in sorted(os.listdir(root), reverse=True):
                cands.append(os.path.join(root, v, "blender.exe"))
    else:
        cands = ["/usr/bin/blender", "/usr/local/bin/blender"]
    for c in cands:
        if os.path.exists(c):
            return c
    return shutil.which("blender")
