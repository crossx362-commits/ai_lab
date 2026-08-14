#!/usr/bin/env python3
"""
마루(게임 개발) — 유니티 없이 C# 컴파일 검사

왜 필요한가:
  이 프로젝트에서 가장 자주 막힌 것이 **에디터 락**이다. 오너가 에디터를 열어두면
  배치 빌드가 exit 21로 죽고(GAME_DEV_HANDOFF.md §5), 그래서 지난 세션은
  **컴파일을 한 번도 못 돌린 채** 11개 작업을 커밋했다. "컴파일 오류부터 확인할 것"이
  인수인계의 첫 줄이 된 이유다.

  그런데 컴파일 검사만은 유니티 에디터가 없어도 된다. 에디터 설치본 안에
  Roslyn(csc.dll)과 유니티 참조 어셈블리가 그대로 들어 있으므로,
  그걸로 `Assets/**/*.cs`를 라이브러리로 컴파일해 보면 문법·타입 오류가 전부 나온다.
  **에디터 락과 무관하고, 빌드보다 수십 배 빠르다.**

무엇을 못 잡는가 (이 도구의 한계를 먼저 적는다):
  - 에셋 참조 깨짐(.meta GUID 고아), 씬·프리팹 연결, 셰이더, 런타임 NullReference
  - 패키지(com.unity.*)에서 오는 타입 — 참조 어셈블리에 없으면 CS0246이 뜬다.
    그 경우 --packages 로 Library/ScriptAssemblies 를 참조에 추가한다(임포트 1회 후 생김).
  즉 **통과 = 문법이 맞다**이지 **통과 = 게임이 돈다**가 아니다. 실동작은 여전히 플레이로 본다.

사용:
    python3 game_compile_check.py                # 프로젝트 전체 컴파일 검사
    python3 game_compile_check.py --self-test    # 네거티브 컨트롤(고의 오류가 잡히는지)

  --self-test 를 반드시 한 번은 돌려라. 참조가 하나라도 어긋나면 csc가
  "오류 0건"처럼 조용히 성공해 보일 수 있다 — 이 저장소가 실제로 겪은
  「테스트를 돌렸다 ≠ 테스트가 실행됐다」 사고와 같은 계열이다.
"""
import argparse
import os
import subprocess
import sys
import tempfile

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from game_platform import IS_MAC, IS_WIN, project_unity_version, _hub_roots  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))
UNITY_PROJECT = os.path.join(ROOT, "projects", "ashes-to-stars", "unity")

AGENT = "마루"


def log(msg):
    print(f"[{AGENT}] {msg}", flush=True)


def _editor_contents(hub_root, version):
    """에디터 설치본에서 Roslyn·참조 어셈블리가 사는 최상위 폴더."""
    base = os.path.join(hub_root, version)
    if IS_MAC:
        return os.path.join(base, "Unity.app", "Contents")
    return os.path.join(base, "Editor", "Data")


def find_toolchain(unity_project=UNITY_PROJECT):
    """
    컴파일에 쓸 (dotnet, csc.dll, 참조 dll 목록, 어느 에디터를 썼는지) 를 찾는다.

    프로젝트 버전을 우선하되, **없으면 설치된 다른 버전으로도 검사한다**.
    빌드와 달리 컴파일 검사는 버전이 조금 달라도 문법 오류를 그대로 잡아내고,
    "정확한 버전이 없어서 검사조차 못 했다"가 지난 세션의 실패였기 때문이다.
    """
    want = project_unity_version(unity_project)
    cands = []
    for root in _hub_roots():
        if not os.path.isdir(root):
            continue
        for v in sorted(os.listdir(root), reverse=True):
            c = _editor_contents(root, v)
            if os.path.isdir(c):
                cands.append((v, c))
    if not cands:
        return None, "유니티 설치를 찾을 수 없다"

    # 프로젝트 버전을 앞에 두되, **설치가 끝나지 않은 후보는 건너뛴다**.
    # 설치 중인 에디터 폴더는 이미 존재하지만 Roslyn이 아직 없다 — 첫 후보만 보고
    # 판정하면 "찾을 수 없다"로 조용히 SKIP된다(실측으로 밟았다).
    cands.sort(key=lambda vc: (vc[0] != want,))
    for version, contents in cands:
        tc = _toolchain_at(contents, version)
        if tc:
            return tc, None
    return None, ("컴파일 가능한 에디터 설치본이 없다 "
                  f"(후보: {', '.join(v for v, _ in cands)})")


def _toolchain_at(contents, version):
    """이 설치본으로 컴파일이 가능하면 도구 정보를, 아니면 None."""
    scripting = os.path.join(contents, "Resources", "Scripting") if IS_MAC else \
        os.path.join(contents, "Tools", "Roslyn")

    # Windows 배치는 Roslyn을 Tools/Roslyn 아래 별도로 둔다
    alt = os.path.join(contents, "Tools", "Roslyn", "csc.exe")
    if os.path.exists(alt):
        return {"csc_exe": alt, "refs": _ref_dlls(contents), "version": version}

    # 에디터 버전마다 배치가 다르다 — 실측:
    #   6000.3.x : Scripting/DotNetSdkRoslyn/csc.dll  + Scripting/NetCoreRuntime/dotnet
    #   6000.5.x : Scripting/DotNetSdk/sdk/<ver>/Roslyn/bincore/csc.dll + DotNetSdk/dotnet
    # 한쪽만 알고 있으면 "설치돼 있는데 못 찾는다"가 된다.
    exe = ".exe" if IS_WIN else ""
    dotnets = [os.path.join(scripting, "NetCoreRuntime", "dotnet" + exe),
               os.path.join(scripting, "DotNetSdk", "dotnet" + exe)]
    cscs = [os.path.join(scripting, "DotNetSdkRoslyn", "csc.dll")]
    sdk_root = os.path.join(scripting, "DotNetSdk", "sdk")
    if os.path.isdir(sdk_root):
        for sdk in sorted(os.listdir(sdk_root), reverse=True):
            cscs.append(os.path.join(sdk_root, sdk, "Roslyn", "bincore", "csc.dll"))

    dotnet = next((d for d in dotnets if os.path.exists(d)), None)
    csc = next((c for c in cscs if os.path.exists(c)), None)
    if not dotnet or not csc:
        return None
    return {"dotnet": dotnet, "csc": csc, "refs": _ref_dlls(contents),
            "version": version}


def _ref_dlls(contents):
    """유니티 참조 어셈블리 — UnityEngine 모듈 + UnityEditor + .NET 표준."""
    scripting = os.path.join(contents, "Resources", "Scripting") if IS_MAC else \
        os.path.join(contents, "Managed")
    dirs = []
    if IS_MAC:
        dirs = [os.path.join(scripting, "Managed", "UnityEngine"),
                os.path.join(scripting, "Managed"),
                os.path.join(scripting, "UnityReferenceAssemblies", "unity-4.8-api"),
                os.path.join(scripting, "UnityReferenceAssemblies", "unity-4.8-api", "Facades")]
    else:
        dirs = [os.path.join(contents, "Managed", "UnityEngine"),
                os.path.join(contents, "Managed"),
                os.path.join(contents, "MonoBleedingEdge", "lib", "mono", "unityaot-win32-api")]

    refs, seen = [], set()
    for d in dirs:
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if not f.endswith(".dll"):
                continue
            # Managed/ 루트에는 빌드 파이프라인용 내부 dll이 많다 — 필요한 것만 집는다
            if os.path.basename(d) == "Managed" and not (
                    f.startswith("UnityEngine") or f.startswith("UnityEditor")):
                continue
            if f in seen:
                continue
            seen.add(f)
            refs.append(os.path.join(d, f))
    return refs


def source_files(unity_project=UNITY_PROJECT):
    out = []
    for base, _dirs, files in os.walk(os.path.join(unity_project, "Assets")):
        for f in files:
            if f.endswith(".cs"):
                out.append(os.path.join(base, f))
    return sorted(out)


def compile_check(unity_project=UNITY_PROJECT, extra_sources=(), extra_refs=()):
    """(오류줄 목록, 메모). 오류가 없으면 빈 목록."""
    tc, err = find_toolchain(unity_project)
    if err:
        return None, err

    srcs = source_files(unity_project) + list(extra_sources)
    if not srcs:
        return None, "컴파일할 .cs 파일이 없다"

    tmp = tempfile.mkdtemp(prefix="ats_compile_")
    rsp = os.path.join(tmp, "csc.rsp")
    with open(rsp, "w", encoding="utf-8") as f:
        f.write("-target:library\n-nostdlib+\n-langversion:9\n")
        f.write(f'-out:"{os.path.join(tmp, "check.dll")}"\n')
        # 유니티 자체 규칙과 같은 수준으로 소음을 끈다(미사용 필드 등은 오류가 아니다)
        f.write("-nowarn:0169,0414,0649,0067,0108,0436\n")
        for r in list(tc["refs"]) + list(extra_refs):
            f.write(f'-r:"{r}"\n')
        for s in srcs:
            f.write(f'"{s}"\n')

    cmd = ([tc["csc_exe"]] if "csc_exe" in tc else [tc["dotnet"], tc["csc"]]) + ["-noconfig", "@" + rsp]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8",
                       errors="replace", timeout=600)
    errors = [ln for ln in (r.stdout + r.stderr).splitlines() if ": error CS" in ln]
    return errors, f"Unity {tc['version']} 참조 · 소스 {len(srcs)}개"


def self_test(unity_project=UNITY_PROJECT):
    """
    네거티브 컨트롤 — 고의로 깨진 파일을 하나 끼워 넣고 **오류가 잡히는지** 본다.
    이게 통과해야 "오류 0건"이라는 말에 의미가 생긴다.
    """
    tmp = tempfile.mkdtemp(prefix="ats_negctrl_")
    bad = os.path.join(tmp, "NegativeControl.cs")
    with open(bad, "w", encoding="utf-8") as f:
        f.write("class __AtsNegativeControl { void F() { int x = \"이건 컴파일되면 안 된다\"; } }\n")
    errors, note = compile_check(unity_project, extra_sources=[bad])
    if errors is None:
        return False, note
    caught = any("NegativeControl.cs" in e for e in errors)
    return caught, (f"{note} · 주입 오류 탐지={'예' if caught else '아니오'} "
                    f"(총 {len(errors)}건)")


def main():
    ap = argparse.ArgumentParser(description="유니티 없이 C# 컴파일 검사")
    ap.add_argument("--project", default=UNITY_PROJECT)
    ap.add_argument("--self-test", action="store_true",
                    help="네거티브 컨트롤 — 고의 오류가 실제로 잡히는지 확인")
    a = ap.parse_args()

    if a.self_test:
        ok, note = self_test(a.project)
        log(f"네거티브 컨트롤: {note}")
        if not ok:
            log("판정: FAIL — 고의 오류를 못 잡았다. 참조 구성이 틀렸으니 '오류 0건'을 믿지 마라")
            return 1
        log("판정: PASS — 검사 장치가 실제로 오류를 잡는다")
        return 0

    errors, note = compile_check(a.project)
    if errors is None:
        log(f"판정: SKIP — {note}")
        return 2
    log(note)
    if errors:
        for e in errors[:40]:
            print("  " + e)
        if len(errors) > 40:
            log(f"... 외 {len(errors) - 40}건")
        log(f"판정: FAIL — 컴파일 오류 {len(errors)}건")
        return 1
    log("판정: PASS — 컴파일 오류 0건 (실동작은 플레이로 따로 확인할 것)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
