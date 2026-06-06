
## 2026-06-04 10:05 UTC — 배포 검증

- **HTTP 상태**: 403 (host_not_allowed) | Latency: 0.061s
- **핵심 자산** (`/`, `/css/style.css`, `/js/app.js`, `/js/supabase.js`, `/manifest.json`, `/sw.js`): 전체 403
- **Vercel 응답 헤더**: `x-deny-reason: host_not_allowed`
- **로컬 파일 상태**:
  - Tailwind config: OK
  - Font Awesome: OK
  - Supabase URL: OK
  - Brand color (brand-500): OK (11건)
  - Tailwind JS: OK
- **최신 로컬 커밋**: `d8488e1` Fix: Vercel rewrites 추가로 ?v=130 쿼리 파라미터 JS 404 해결 (2026-06-03)
- **일치 여부**: 로컬 코드는 정상 — 배포 자체에 접근 불가
- **근본 원인**: `host_not_allowed`는 Vercel **Deployment Protection** 활성화를 의미
  - Vercel 대시보드 → petnna 프로젝트 → Settings → Deployment Protection 확인 필요
  - 또는 프로젝트가 Team 플랜으로 이동되어 인증 없이 `.vercel.app` URL 접근 차단됨
- **조치 필요**: Vercel CLI/대시보드에서 직접 확인 필요 (이 환경에서 Vercel 대시보드 접근 불가)
  - `vercel --prod` 재배포보다 **Deployment Protection 설정 확인**이 우선


## 2026-06-04 12:06 UTC — 배포 검증

### HTTP 상태
- **루트 `/`**: 403 | Latency: ~0.09s
- **핵심 자산 전체** (`/css/style.css`, `/js/app.js`, `/js/supabase.js`, `/manifest.json`, `/sw.js`): 모두 403

### 오류 원인
- `x-deny-reason: host_not_allowed` — Vercel이 `petnna.vercel.app` 도메인을 프로젝트 allowlist에서 거부
- 코드 문제 아님 — **Vercel 대시보드 설정 문제**

### 로컬 버전 핵심 요소 (정상)
- Tailwind config: OK
- Font Awesome: OK
- Supabase ref: OK
- Brand color (brand-500): OK
- Tailwind ref: OK

### 로컬 vs 배포 일치 여부
- 로컬 코드: 정상 (모든 요소 존재)
- 배포 접근: 불가 (403 — `host_not_allowed`)

### 필요 조치 (코드로 해결 불가)
1. **Vercel 대시보드** → 해당 프로젝트 → Settings → Domains 에서 `petnna.vercel.app` 도메인 연결 확인
2. **Deployment Protection** 설정 확인 — Vercel Pro 기능으로 allowlist 외 접근 차단 시 발생
3. `vercel --prod` 재배포 후에도 동일 오류 예상 (도메인 미연결이 원인)

### 조치
- 코드 수정 불필요 — Vercel 계정/프로젝트 설정에서 해결 필요
- 로그 저장 완료

## 2026-06-05 UTC — 배포 검증

- HTTP 상태: 403 Forbidden | Latency: 0.053s
- 코어 자산: 전체 403 (/, /css/style.css, /js/app.js, /js/supabase.js, /manifest.json, /sw.js)
- 로컬 파일: tailwind.config OK | font-awesome OK
- 최신 로컬 커밋: feat(petnna): 모바일 UI 개선 및 Supabase 미디어 스토리지 통합
- 조치: 이상 감지 — Vercel 배포 접근 차단(403). Vercel 대시보드에서 Password Protection 또는 도메인 설정 확인 필요

## 2026-06-06 10:03 UTC — 배포 검증

- **HTTP 상태**: 403 | Latency: 0.054s
- **x-deny-reason**: `host_not_allowed` (Vercel Deployment Protection 차단)
- **코어 자산 응답**: 전체 403 (/, /css/style.css, /js/app.js, /js/supabase.js, /manifest.json, /sw.js)
- **배포 응답 본문**: `Host not in allowlist`
- **로컬 파일 상태**: ✅ 정상 (tailwind.config OK, font-awesome OK)
- **최신 로컬 커밋**: `76068cf fix(petnna): localStorage 봇 알림 초기화 — NPC 알림 데이터 자동 제거`
- **판정**: 코드/빌드 이상 없음. Vercel 대시보드의 Deployment Protection 설정에서 IP/도메인 allowlist 문제. 외부 접근 차단 상태.
- **조치**: Vercel 대시보드 → Project Settings → Deployment Protection 에서 허용 범위 확인 및 수정 필요.
