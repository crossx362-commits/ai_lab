
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

