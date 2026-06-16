"""
history_recorder.py — 에이전트 업로드 히스토리 기록 공통 유틸
"""
import os
import json

def _find_root(start: str) -> str:
    root = start
    for _ in range(8):
        if os.path.isdir(os.path.join(root, "reports")):
            hist = os.path.join(root, "reports", "history", "upload_history.json")
            # reports/history/upload_history.json 이 실제로 존재하는 루트만 반환
            if os.path.exists(hist) or os.path.isdir(os.path.join(root, "reports", "history")):
                return root
        root = os.path.dirname(root)
    return start

def record_to_history(record: dict, caller_file: str = __file__):
    """reports/history/upload_history.json에 레코드 추가."""
    root = _find_root(os.path.dirname(os.path.abspath(caller_file)))
    mem_path = os.path.join(root, "reports", "history", "upload_history.json")
    try:
        history = json.load(open(mem_path, encoding="utf-8")) if os.path.exists(mem_path) else []
        history.append(record)
        with open(mem_path, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
    except Exception as e:
        print(f"  [Warning] 히스토리 기록 실패: {e}")
