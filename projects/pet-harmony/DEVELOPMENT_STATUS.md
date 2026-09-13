# 펫과나 개발 현황

작성일: 2026-09-13
라이브 주소: https://petnna-app.vercel.app/

## 현재 한줄 상태

핵심 화면과 검사 흐름은 라이브 배포되어 있으며, 정적 기능 스모크 테스트를 통과했습니다. 다만 Vercel 환경변수가 아직 등록되지 않아 PayPal 실결제·Supabase 저장·웹훅은 운영 활성화 전입니다.

## 완료된 기능

### 제품 흐름

- 펫 오디션 콘셉트와 입양 전 중심 카피 적용
- 첫 화면에서 검사 종류·결과 예시·가격을 한눈에 확인
- 종합 결과, 입양 전, 강아지·고양이 비교, 세 식구, 보호자 스타일 검사
- 보호자 스타일은 3문항 무료 검사로 분리
- 종합 결과에서 평생·오늘·월간·년간 흐름 확인
- 점수 게이지, 삼각 시각화, 쉬운 결과 설명 제공
- 결과 화면에서 다시 입력·홈 이동 지원
- 생일을 모르는 펫은 모드별 기본 성향으로 추정

### 결제·결과 접근

- $0.99 상세 케어팁 상품 구조
- $3.99 사진 포함 최종 리포트 상품 구조
- PayPal 주문 생성 → 서버 캡처 → unlock 기록 흐름 구현
- 결제 전 상세 데이터는 화면에 렌더링하지 않음
- 보호자 스타일 검사에는 페이월을 표시하지 않음
- PDF 결제 후 Print / Save as PDF 버튼 표시
- PDF 결제 후 선택 이메일 링크 UI 제공
- PayPal SDK는 서버의 `PAYPAL_CLIENT_ID`를 받아 동적으로 로드

### 배포·품질

- Vercel 배포 완료
- WebP 이미지 사용
- 원본 PNG는 `.vercelignore`로 배포 제외
- OG 이미지 절대 URL 적용
- 모바일 레이아웃 및 이메일 입력 영역 반응형 처리
- API 입력·결제 미설정 상태에 대한 오류 응답 처리

## 검증 결과

최근 라이브 스모크 테스트:

```text
PASS home 200
PASS css 200
PASS app 200
PASS manifest 200
PASS hero-webp 200
PASS health 200
PASS payment-guard 503
PASS result-recovery-guard 503
```

의미:

- 정적 웹페이지와 주요 에셋은 정상 응답
- 결제 환경변수가 없을 때 주문 API가 안전하게 차단됨
- 실결제가 성공했다는 뜻은 아님

## 현재 미완료

### 필수 운영 설정

현재 Vercel에 환경변수가 등록되어 있지 않습니다.

필요한 값:

```text
PAYPAL_ENV=live
PAYPAL_CLIENT_ID
PAYPAL_CLIENT_SECRET
PAYPAL_WEBHOOK_ID
SUPABASE_URL
SUPABASE_SERVICE_ROLE_KEY
```

등록 후 확인할 조건:

```json
{
  "paymentConfigured": true,
  "webhookConfigured": true
}
```

### 아직 실제 검증하지 못한 항목

- PayPal Live에서 $0.99 주문·캡처
- PayPal Live에서 $3.99 주문·캡처
- 결제 후 Supabase unlock 기록
- PayPal `PAYMENT.CAPTURE.COMPLETED` 웹훅 실이벤트
- 실제 이메일 발송 서비스 연결
- 모바일 실기기에서 결제부터 최종 리포트까지의 E2E 흐름

## 다음 작업 순서

1. PayPal Secret을 새로 발급하고 Vercel 환경변수 등록
2. Supabase 스키마 실행 및 서비스 키 등록
3. `/api/health`에서 결제·웹훅 활성 상태 확인
4. Sandbox 또는 Live 결제 E2E 테스트
5. 웹훅 실이벤트 1회 확인
6. 실패·중복 결제·재진입 테스트
7. 필요할 때 Resend 등 이메일 발송 서비스 연결

## 보안 메모

- PayPal Secret은 브라우저 코드에 넣지 않습니다.
- 이전 대화에 노출된 PayPal Secret은 폐기하고 새 Secret을 사용해야 합니다.
- PayPal Client ID는 공개 가능하지만, 서버 환경변수의 Client ID와 반드시 같은 앱의 값이어야 합니다.
- 결제 성공 여부는 localStorage가 아니라 서버 캡처·Supabase unlock 상태를 기준으로 합니다.

## 주요 파일

- `index.html`: 화면 구조와 메타 정보
- `js/app.js`: 검사·결과·결제 UI 흐름
- `js/harmony-core.js`: 점수 계산 엔진
- `js/saju-core.js`: 재미용 사주 해석 엔진
- `api/orders.js`: PayPal 주문 생성
- `api/capture.js`: PayPal 캡처 및 unlock
- `api/unlock-status.js`: 결제 상태 확인
- `api/webhook.js`: PayPal 웹훅 처리
- `api/results.js`: 게스트 결과 저장·복구
- `api/report-link.js`: 선택 이메일 링크 발송
- `supabase/schema.sql`: 결과·주문·unlock 테이블
- `DEPLOY_CHECKLIST.md`: 배포 설정 절차
