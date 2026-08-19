# ChinaGuard

중국 IP와의 통신(인바운드/아웃바운드)을 Windows 방화벽 커널 레벨에서 차단하고,
차단 시도를 실시간 알림으로 띄워주는 트레이 상주 프로그램.

## 동작 방식

1. **차단** — APNIC 위임 데이터 기반 중국 CIDR 목록(ipverse/rir-ip)을 받아
   Windows 방화벽에 인바운드+아웃바운드 차단 규칙으로 등록.
   커널(WFP)이 차단하므로 프로그램이 꺼져 있어도 차단은 유지됨.
2. **감지/알림** — WFP 차단 감사 이벤트(보안 로그 5157)를 구독해
   중국 IP 관련 차단 발생 시 어떤 프로세스가 어디로 접속하려 했는지 풍선 알림.
   같은 (프로세스, IP) 조합은 5분에 1회만 알림 (`Throttler.cs`).
3. **모니터** — 트레이 아이콘 더블클릭 → 현재 TCP 연결 목록 + 국가 표시
   (DB-IP Country Lite, CC BY 4.0). 중국 연결은 빨간색 강조.

## 빌드

SDK 불필요. Windows 내장 csc(.NET Framework 4.8)로 빌드:

```
build.cmd
```

## 실행

`ChinaGuard.exe` 실행 (관리자 권한 필요, UAC 승인).
첫 실행 시 자동으로:
- 중국 CIDR 목록 다운로드 (raw.githubusercontent.com/ipverse/rir-ip)
- GeoIP DB 다운로드 (download.db-ip.com, 약 30MB)
- 방화벽 차단 규칙 등록
- WFP 차단 감사 정책 활성화 (`auditpol`)

## 제거

트레이 메뉴 → "차단 규칙 모두 제거" 후 종료.
감사 정책 원복은 필요 시:

```
auditpol /set /subcategory:"{0CCE9226-69AE-11D9-BED3-505054503030}" /failure:disable
```

## 파일

- `china-ipv4.txt` / `china-ipv6.txt` — 중국 CIDR 캐시
- `dbip-country-lite.csv` — GeoIP DB 캐시
- `chinaguard.log` — 이벤트/차단 로그
