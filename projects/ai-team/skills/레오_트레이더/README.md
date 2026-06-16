# 레오 트레이더 운영 메모

레오는 고변동성 알트코인 단타 전용 에이전트입니다. 데이브보다 공격적인 전략을 쓰므로 기본 실행은 항상 시뮬레이션으로 둡니다.

## 실행 모드

```powershell
# 상태 확인
python tools/leo_aggressive_trader.py --status

# 1회 시뮬레이션 스캔
python tools/leo_aggressive_trader.py --once --sim

# 시뮬레이션 데몬
python tools/leo_aggressive_trader.py --daemon --sim

# 실거래 1회 실행
python tools/leo_aggressive_trader.py --once --live

# 실거래 데몬
python tools/leo_aggressive_trader.py --daemon --live
```

인자 없이 실행하면 상태만 보여주고 매매 루프를 시작하지 않습니다.

## 의존성

레오의 스캔/매매 루프는 Upbit 시세 조회를 위해 `pyupbit`가 필요합니다.

```powershell
python -m pip install pyupbit
```

현재 Codex 번들 파이썬에는 `pyupbit`가 없을 수 있습니다. 이 경우 `--status`는 동작하지만 `--once`와 `--daemon`은 의존성 누락을 보고하고 종료합니다.

## 디스패처 연결

예원 디스패처에서 다음 표현은 레오로 라우팅됩니다.

- `레오`
- `leo`
- `단타`
- `공격적`
- `고변동성`
- `알트코인`

실거래는 명령에 `실거래`, `실매매`, `라이브`, `live`가 명시된 경우에만 켜집니다.
