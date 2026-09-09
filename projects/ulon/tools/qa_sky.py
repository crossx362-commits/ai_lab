#!/usr/bin/env python3
"""문 구멍으로 하늘이 보이는가 — **그 픽셀을 센다**(검수 지시 2026-09-09).

던전 입구 샷에서 「지하로 들어간다」를 깨는 것은 고도가 아니라 **뚫려 있는 것**이다.
문 너머로 하늘·마을이 그대로 보이면 문이 아니라 창틀이다. 그래서 **문 구멍 영역의
하늘색 픽셀 수**를 자로 쓴다 — 전 판은 크고, 막은 뒤에는 0에 가까워야 한다.

쓰는 법:
    python3 tools/qa_sky.py builds/qa/07_d1_entrance.png [--band 0.30 0.85]

**영역**: 화면 위쪽 띠는 진짜 하늘이라 뺀다(기본: 세로 30%~85% 구간만 본다).
그 안에서 파랑이 확실히 우세한 픽셀(B > R+18 and B > G+10)을 하늘로 센다.

**이 자가 못 보는 것**: 파란 소품(물·깃발)도 하늘로 셀 수 있고, 흐린 하늘은 못 본다.
그래서 **절대 수치가 아니라 전후 비교**로 쓴다.
"""
import sys
import struct
import zlib


def read_png(path):
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("PNG가 아닙니다: " + path)
    pos, w, h, idat = 8, 0, 0, b""
    depth = color = None
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, depth, color = struct.unpack(">IIBB", body[:10])
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break
        pos += 12 + ln
    if depth != 8 or color not in (2, 6):
        raise SystemExit("8비트 RGB/RGBA만 봅니다(이 파일: depth=%s color=%s)" % (depth, color))
    ch = 3 if color == 2 else 4
    raw = zlib.decompress(idat)
    rows, prev, at = [], bytearray(w * ch), 0
    for _ in range(h):
        f = raw[at]; at += 1
        line = bytearray(raw[at:at + w * ch]); at += w * ch
        for i in range(len(line)):
            a = line[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        rows.append(line)
        prev = line
    return w, h, ch, rows


def sky_pixels(path, top=0.30, bottom=0.85):
    w, h, ch, rows = read_png(path)
    y0, y1 = int(h * top), int(h * bottom)
    n = 0
    for y in range(y0, y1):
        row = rows[y]
        for x in range(w):
            r, g, b = row[x * ch], row[x * ch + 1], row[x * ch + 2]
            if b > r + 18 and b > g + 10:
                n += 1
    return n, w * (y1 - y0)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    top, bot = 0.30, 0.85
    if "--band" in sys.argv:
        i = sys.argv.index("--band")
        top, bot = float(sys.argv[i + 1]), float(sys.argv[i + 2])
    for p in args:
        n, total = sky_pixels(p, top, bot)
        print("%-42s 하늘 픽셀 %7d / 띠 %7d (%.2f%%)" % (p.split("/")[-1], n, total, 100.0 * n / total))
