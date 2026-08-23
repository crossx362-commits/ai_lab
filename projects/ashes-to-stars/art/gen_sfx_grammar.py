#!/usr/bin/env python3
"""§16-10 전투 오디오 문법 — 위험 신호 4종 + 징글 3종 WAV 직접 합성(외부 생성기 미사용).

위험 신호는 서로 음색이 명확히 달라야 한다(§16-1: 눈 감고 구분).
  danger_zone     위험 장판 발동 — 어두운 하강 스윕+트레몰로
  boss_enrage     보스 격노   — 상승 그로울(사각파+노이즈)
  last_life_enter 마지막 목숨 진입 — 경고 2비프(밝은 사각파)
  escape_cast     긴급 탈출 캐스팅 — 부드러운 상승 아르페지오(사인)
징글:
  level_up        레벨업 — 짧은 상승 3음(§16-10 3번째 불릿)
  death_low       일반 사망 — 저음 1회
  last_life_gone  마지막 목숨 소멸 — 낮춘 음악 하강 후 별도 고음 신호
"""
from pathlib import Path
import math
import struct
import wave

SR = 22050
HERE = Path(__file__).resolve().parent
DST = HERE.parent / "unity" / "Assets" / "Resources" / "sfx"


def env(t, dur, attack=0.005, release=0.08):
    if t < attack:
        return t / attack
    if t > dur - release:
        return max(0.0, (dur - t) / release)
    return 1.0


def saw(ph):
    return 2.0 * (ph - math.floor(ph)) - 1.0


def sqr(ph, duty=0.5):
    return 1.0 if (ph % 1.0) < duty else -1.0


def tri(ph):
    p = ph % 1.0
    return 4.0 * abs(p - 0.5) - 1.0


def render(name, dur, fn, gain=0.8):
    n = int(SR * dur)
    frames = bytearray()
    for i in range(n):
        t = i / SR
        v = max(-1.0, min(1.0, fn(t) * gain))
        frames += struct.pack("<h", int(v * 32767))
    out = DST / f"{name}.wav"
    with wave.open(str(out), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(frames))
    print(f"{out.name}  {dur:.2f}s  {len(frames)}B")


def danger_zone():
    dur = 0.95

    def f(t):
        freq = 220.0 * math.pow(70.0 / 220.0, t / dur)          # 하강 스윕
        trem = 0.65 + 0.35 * math.sin(2 * math.pi * 9.0 * t)     # 트레몰로
        ph = t * freq
        v = 0.6 * math.sin(2 * math.pi * ph) + 0.4 * saw(ph)
        return v * trem * env(t, dur, 0.01, 0.25)

    return dur, f


def boss_enrage():
    dur = 1.15

    def f(t):
        prog = t / dur
        freq = 80.0 + 80.0 * prog                                 # 상승
        growl = sqr(t * freq, 0.42) * 0.7 + saw(t * freq * 0.5) * 0.3
        noise = ((t * 7919.0) % 1.0) * 2.0 - 1.0                  # 유사 난수 노이즈
        amp = 0.25 + 0.75 * prog                                  # 점점 세게
        v = growl * 0.85 + noise * 0.15 * prog
        return v * amp * env(t, dur, 0.02, 0.18)

    return dur, f


def last_life_enter():
    dur = 0.72
    beep = 0.22

    def f(t):
        on = t < beep or (beep + 0.12 <= t < beep * 2 + 0.12)
        v = sqr(t * 988.0, 0.35) * 0.8 + math.sin(2 * math.pi * 1976.0 * t) * 0.2
        local = t if t < beep else t - (beep + 0.12)
        return v * on * env(local if on else 0, min(beep, dur), 0.004, 0.05)

    return dur, f


def escape_cast():
    notes = [(0.00, 440.0), (0.16, 554.37), (0.32, 659.25)]
    dur = 0.85

    def f(t):
        v = 0.0
        for start, freq in notes:
            if t >= start:
                lt = t - start
                decay = math.exp(-lt * 5.0)
                v += math.sin(2 * math.pi * freq * lt) * decay
        return v * env(t, dur, 0.004, 0.30)

    return dur, f


def level_up():
    notes = [(0.00, 523.25), (0.13, 659.25), (0.26, 783.99)]
    dur = 0.62

    def f(t):
        v = 0.0
        for start, freq in notes:
            if t >= start:
                lt = t - start
                decay = math.exp(-lt * 6.0)
                v += tri(t * freq) * decay
        return v * env(t, dur, 0.004, 0.20)

    return dur, f


def death_low():
    dur = 0.55

    def f(t):
        freq = 110.0 * math.pow(55.0 / 110.0, t / dur)
        return math.sin(2 * math.pi * freq * t) * env(t, dur, 0.002, 0.28)

    return dur, f


def last_life_gone():
    # §16-10: 음악을 0.5초 낮춘 뒤 별도 신호음 → 하강 소프라구 후 고음 핑
    drop = [(0.00, 329.63), (0.28, 261.63), (0.56, 220.0)]
    ping_at = 0.92
    dur = 1.45

    def f(t):
        v = 0.0
        for start, freq in drop:
            if t >= start:
                lt = t - start
                v += tri(t * freq) * math.exp(-lt * 4.0) * 0.8
        if t >= ping_at:
            lt = t - ping_at
            v += math.sin(2 * math.pi * 1568.0 * lt) * math.exp(-lt * 6.0) * 0.9
            v += sqr(lt * 1568.0, 0.3) * math.exp(-lt * 7.0) * 0.25
        return v * env(t, dur, 0.005, 0.25)

    return dur, f


def main():
    DST.mkdir(parents=True, exist_ok=True)
    for name, gen in [
        ("danger_zone", danger_zone),
        ("boss_enrage", boss_enrage),
        ("last_life_enter", last_life_enter),
        ("escape_cast", escape_cast),
        ("level_up", level_up),
        ("death_low", death_low),
        ("last_life_gone", last_life_gone),
    ]:
        dur, fn = gen()
        render(name, dur, fn)


if __name__ == "__main__":
    main()
