#!/usr/bin/env python3
"""슈퍼트렌드 감시 목록 관리"""
import os
import json

WATCH_FILE = os.path.join(os.path.dirname(__file__), ".supertrend_watch.json")

# 주요 종목 코드 매핑
STOCK_CODES = {
    "원익ips": "240810",
    "원익아이피에스": "240810",
    "sk하이닉스": "000660",
    "하이닉스": "000660",
    "삼성전자": "005930",
    "삼성": "005930",
    "카카오": "035720",
    "네이버": "035420",
    "naver": "035420",
}

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
    """종목명 → 종목코드 변환"""
    name_clean = name.replace(" ", "").replace("-", "").lower()

    # 직접 코드인 경우
    if name_clean.isdigit() and len(name_clean) == 6:
        return name_clean

    # 매핑된 이름
    return STOCK_CODES.get(name_clean)

def add_stock(name):
    """종목 추가"""
    code = get_stock_code(name)
    if not code:
        return False, f"❌ 알 수 없는 종목: {name}\n\n지원 종목:\n- 원익IPS (240810)\n- SK하이닉스 (000660)\n- 삼성전자 (005930)\n- 카카오 (035720)\n- 네이버 (035420)"

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
