"""
youtube_audit.py — 루나 YouTube 채널 감사 진입점

⚠️  중복 감지·수정 로직은 가희(content_inspector.py)로 이관됨 (2026-05-28).
    이 파일은 가희 run_full_audit()을 호출하는 얇은 래퍼로만 사용.

실행:
  python youtube_audit.py   # 가희 전체 감사 즉시 실행
"""
import os
import sys

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)


def run_audit():
    """가희 전체 감사를 트리거한다."""
    inspector_path = os.path.join(
        _root, "assets", "tool-seeds", "가희_검수관", "content_inspector.py"
    )
    import importlib.util
    spec = importlib.util.spec_from_file_location("content_inspector", inspector_path)
    mod  = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    mod.run_full_audit()


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    run_audit()
