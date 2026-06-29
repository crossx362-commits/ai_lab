#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""과거 수급(외국인·기관 일별 순매매) 수집 — 네이버 금융 frgn 페이지 파싱.

KIS inquire-investor 는 최근 30일만 주므로, 백테스트용 장기 수급 히스토리는
네이버 금융(외국인·기관 일별 순매매)에서 페이지네이션으로 수집한다.
반환: {"YYYYMMDD": {"inst": 기관순매매주, "frgn": 외국인순매매주}, ...}
"""

from __future__ import annotations

import re
import time
import urllib.request

_HEADERS = {"User-Agent": "Mozilla/5.0"}


def _num(cell: str) -> int:
    """<td> 셀에서 부호 포함 정수 추출 (순매매는 텍스트에 -부호 포함)."""
    t = re.sub(r"<[^>]+>", "", cell)
    t = re.sub(r"[^\d\-]", "", t)
    return int(t) if t and t not in ("-", "") else 0


def _parse_page(html: str) -> list[dict]:
    rows = []
    for tr in re.findall(r"<tr[^>]*>.*?</tr>", html, re.S):
        dm = re.search(r"(\d{4})\.(\d{2})\.(\d{2})", tr)
        if not dm:
            continue
        nums = re.findall(r'<td[^>]*class="num"[^>]*>(.*?)</td>', tr, re.S)
        if len(nums) < 6:  # 종가·전일비·등락·거래량·기관·외국인(+보유)
            continue
        rows.append({"date": "".join(dm.groups()), "inst": _num(nums[4]), "frgn": _num(nums[5])})
    return rows


def fetch(code: str, pages: int = 13, pause: float = 0.2) -> dict[str, dict]:
    """code 종목의 외국인·기관 일별 순매매를 pages 페이지(≈20일/페이지)만큼 수집."""
    out: dict[str, dict] = {}
    for pg in range(1, pages + 1):
        url = f"https://finance.naver.com/item/frgn.naver?code={code}&page={pg}"
        try:
            req = urllib.request.Request(url, headers=_HEADERS)
            with urllib.request.urlopen(req, timeout=20) as r:
                html = r.read().decode("euc-kr", "replace")
        except Exception:
            break
        page_rows = _parse_page(html)
        if not page_rows:
            break
        for row in page_rows:
            out.setdefault(row["date"], {"inst": row["inst"], "frgn": row["frgn"]})
        time.sleep(pause)
    return out


if __name__ == "__main__":
    import sys
    code = sys.argv[1] if len(sys.argv) > 1 else "005930"
    pages = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    data = fetch(code, pages)
    print(f"{code}: {len(data)}일 수집 ({min(data)}~{max(data)})" if data else f"{code}: 수집 실패")
    for d in sorted(data)[-5:]:
        print(f"  {d}: 기관 {data[d]['inst']:+,} / 외국인 {data[d]['frgn']:+,}")
