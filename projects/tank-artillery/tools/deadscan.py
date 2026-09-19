#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""죽은 멤버 **후보** 생성기 — 선언만 있고 아무도 안 읽는 것을 찾는다.

🚨 이것은 **후보 생성기이지 판정기가 아니다.** 출력은 반드시 사람이 전수 grep 으로 확인하고,
   처분(지운다/배선한다/드러내고 둔다)은 따로 판단한다. 판정 기준은 2026-09-19 에 이렇게 정해졌다:
       «안 불린다»는 죄가 아니다. **«있다고 믿게 만든다»가 죄다.**

⚠️ 이 도구는 하루에 **세 번 틀렸다.** 고친 내역을 남긴다 — 같은 함정을 다시 파지 않게:
   ① 수식 호출을 못 봤다 — 수식 호출을 빼는 정규식을 써서 `Ui.Bar(...)` · `ImpactRules.ApplyToHit(...)` 같은
      **이 프로젝트의 주된 호출 형태**를 전부 0 으로 셌다(방금 배선한 함수까지 "죽었다"고 뱉었다).
   ② 문자열 안 호출을 못 봤다 — 주석·문자열을 지우는 전처리가 `$"...{MoveWhy(x)}..."` 안의 호출까지
      지웠다. 이 프로젝트는 로그 문자열에서 부르는 게 많아 피해가 컸다.
   ③ **프레임워크가 부르는 것**을 몰랐다 — `[RuntimeInitializeOnLoadMethod]` 인 `BattleDemo.Boot()` 이
      "호출부 0"으로 떴다. 지웠으면 «씬 없이 Play 만 누르면 돈다»(§3-0)가 죽고 빈 화면이 떴다.
      **프레임워크 호출은 정의상 호출부가 0 이라 어떤 참조 카운터로도 못 가른다.**
      → 아래 화이트리스트로 제외하되 **제외했다고 출력에 찍는다.** 조용히 빼면 도구가 무엇을
        안 봤는지 아무도 모른다.
"""
import re, os, sys, collections

ROOTS = ['unity/Assets/_Project/Scripts', 'tools']          # Editor/ 도 포함된다(MenuItem 이 거기 산다)
EXT = ('.cs',)

# ── 프레임워크가 부르는 것 — 호출부가 0 인 게 정상이다 ──
FRAMEWORK_ATTRS = ('RuntimeInitializeOnLoadMethod', 'InitializeOnLoad', 'InitializeOnLoadMethod',
                   'MenuItem', 'ContextMenu', 'SerializeField', 'Preserve')
UNITY_LIFECYCLE = {'Awake','Start','Update','LateUpdate','FixedUpdate','OnGUI','OnEnable','OnDisable',
                   'OnDestroy','OnApplicationQuit','OnApplicationFocus','OnApplicationPause',
                   'OnDrawGizmos','OnDrawGizmosSelected','OnValidate','Reset','OnPreRender','OnPostRender',
                   'OnRenderObject','OnBecameVisible','OnBecameInvisible'}
INTERFACE_IMPL = {'Dispose','MoveNext','GetEnumerator','CompareTo','Equals','GetHashCode','ToString'}
ENTRY = {'Main'}

def sources():
    out = []
    for r in ROOTS:
        for dp, _, fn in os.walk(r):
            for f in fn:
                if f.endswith(EXT): out.append(os.path.join(dp, f))
    return out

def blank_literals(t):
    """주석과 문자열을 지우되 **보간 문자열 안의 `{식}` 은 남긴다**(거기서 함수를 부른다)."""
    t = re.sub(r'//[^\n]*', '', t)
    t = re.sub(r'/\*.*?\*/', '', t, flags=re.S)
    def keep_braces(m):
        lit = m.group(0)
        return ' '.join(re.findall(r'\{([^{}]*)\}', lit)) or '""'
    return re.sub(r'\$?@?"(?:\\.|[^"\\])*"', keep_braces, t)

MOD = r'(?:public|private|internal|protected|static|readonly|const|sealed|override|virtual|partial|abstract|extern|async|\s)*'
DECL = re.compile(r'^([ \t]*)((?:\[[^\]]*\][ \t]*\r?\n[ \t]*)*)' + MOD +
                  r'[A-Za-z_][\w<>,\[\]\.\?]*\s+([A-Z]\w*)\s*\(', re.M)

def main():
    files = sources()
    raw = {f: open(f, encoding='utf-8').read() for f in files}
    code = {f: blank_literals(t) for f, t in raw.items()}
    allcode = '\n'.join(code.values())

    decls, skipped = collections.defaultdict(list), []
    for f, t in raw.items():                      # 선언은 원문에서(속성 줄을 봐야 한다)
        for m in DECL.finditer(t):
            attrs, name = m.group(2) or '', m.group(3)
            why = None
            if any(a in attrs for a in FRAMEWORK_ATTRS): why = '속성 ' + attrs.strip().replace('\n', ' ')[:40]
            elif name in UNITY_LIFECYCLE: why = 'MonoBehaviour 생명주기'
            elif name in INTERFACE_IMPL: why = '인터페이스 구현 가능성'
            elif name in ENTRY: why = '진입점'
            if why: skipped.append((name, f, why))
            else: decls[name].append(f)

    def uses(n):    # 점 앞 호출을 **포함**한다. 그게 주된 호출 형태다.
        return len(re.findall(r'(?<!\w)' + re.escape(n) + r'(?!\w)', allcode))

    dead = [(n, fs[0]) for n, fs in sorted(decls.items()) if len(fs) == 1 and uses(n) <= 1]

    print("=== 죽은 멤버 **후보** (판정 아님 — 전수 grep 으로 확인할 것) ===")
    print("파일 %d개 · 선언 %d개 · 프레임워크 호출로 제외 %d개\n" % (len(files), len(decls), len(skipped)))
    print("[제외한 것 — 호출부가 0 인 게 **정상**이라 안 봤다]")
    for n, f, why in sorted(skipped):
        print("  · %-28s %-44s %s" % (n, f.replace('unity/Assets/_Project/Scripts/', ''), why))
    print("\n[후보]")
    for n, f in dead:
        print("  ? %-28s %s" % (n, f.replace('unity/Assets/_Project/Scripts/', '')))
    print("  (없음)" if not dead else "  → %d건" % len(dead))
    return 0

if __name__ == '__main__':
    sys.exit(main())
