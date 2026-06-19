#!/usr/bin/env python3
"""슈퍼트렌드 감시 목록 관리"""
import os
import sys
import json

# _shared 모듈 경로 추가
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, "..", "..", ".."))

from _shared.env import load_env
from _shared.llm import gpt

load_env()

WATCH_FILE = os.path.join(os.path.dirname(__file__), ".supertrend_watch.json")

def load_watch_list():
    """감시 목록 로드"""
    try:
        if os.path.exists(WATCH_FILE):
            with open(WATCH_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
    except:
        pass
    return {"stocks": []}

def save_watch_list(data):
    """감시 목록 저장"""
    with open(WATCH_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def get_stock_code(name):
    """종목명 → 종목코드 변환 (GPT 사용)"""
    name_clean = name.replace(" ", "").replace("-", "").strip()

    # 직접 코드인 경우 (6자리 숫자)
    if name_clean.isdigit() and len(name_clean) == 6:
        return name_clean

    # GPT로 종목 코드 찾기
    try:
        prompt = f"""한국 주식 종목명을 종목코드로 변환해주세요.

종목명: {name}

규칙:
- 정확한 6자리 종목코드만 반환
- 존재하지 않는 종목이면 "UNKNOWN" 반환
- 설명 없이 코드만 반환

예시:
원익IPS → 240810
SK하이닉스 → 000660
삼성전자 → 005930
"""
        result = gpt(prompt, max_tokens=20, temperature=0)
        code = result.strip()

        # 유효성 검사
        if code.isdigit() and len(code) == 6:
            return code
        elif code == "UNKNOWN":
            return None

    except Exception as e:
        print(f"GPT 종목코드 검색 실패: {e}")

    return None

def add_stock(name):
    """종목 추가"""
    code = get_stock_code(name)
    if not code:
        return False, f"❌ 종목 코드를 찾을 수 없습니다: {name}\n\n한국 주식 종목명을 정확히 입력해주세요."

    data = load_watch_list()

    # 이미 존재하는지 확인
    for stock in data["stocks"]:
        if stock["code"] == code:
            if stock.get("enabled"):
                return False, f"ℹ️ {name} ({code})는 이미 감시 중입니다."
            else:
                stock["enabled"] = True
                save_watch_list(data)
                return True, f"✅ {name} ({code}) 감시 재개"

    # 새로 추가
    data["stocks"].append({
        "code": code,
        "name": name,
        "enabled": True
    })
    save_watch_list(data)
    return True, f"✅ {name} ({code}) 감시 시작\n\n슈퍼트렌드 추세 변환 시 실시간 알림을 보내드립니다."

def remove_stock(name):
    """종목 제거"""
    code = get_stock_code(name)
    if not code:
        return False, f"❌ 알 수 없는 종목: {name}"

    data = load_watch_list()

    # 종목 찾기
    for stock in data["stocks"]:
        if stock["code"] == code:
            stock["enabled"] = False
            save_watch_list(data)
            return True, f"✅ {name} ({code}) 감시 중단"

    return False, f"ℹ️ {name} ({code})는 감시 목록에 없습니다."

def list_stocks():
    """감시 목록 조회"""
    data = load_watch_list()
    enabled = [s for s in data["stocks"] if s.get("enabled")]

    if not enabled:
        return "ℹ️ 현재 감시 중인 종목이 없습니다."

    lines = ["📊 슈퍼트렌드 감시 목록\n"]
    for stock in enabled:
        lines.append(f"✅ {stock['name']} ({stock['code']})")

    return "\n".join(lines)

if __name__ == "__main__":
    import sys
    if len(sys.argv) < 2:
        print("Usage: python supertrend_manager.py [add|remove|list] <종목명>")
        sys.exit(1)

    cmd = sys.argv[1]
    if cmd == "list":
        print(list_stocks())
    elif cmd == "add" and len(sys.argv) > 2:
        success, msg = add_stock(sys.argv[2])
        print(msg)
    elif cmd == "remove" and len(sys.argv) > 2:
        success, msg = remove_stock(sys.argv[2])
        print(msg)
