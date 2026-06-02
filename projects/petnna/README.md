# 🐾 Petnna - AI 반려동물 케어 플랫폼

<div align="center">

![Petnna Logo](https://placehold.co/600x200/e37736/ffffff?text=Pet%26Na)

**반려동물과의 성향 분석부터 커스텀 비디오 일기장까지**

[![License](https://img.shields.io/badge/license-Proprietary-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-1.0.0-green.svg)](package.json)
[![Status](https://img.shields.io/badge/status-Production-success.svg)]()

[데모 보기](https://petnna.vercel.app) | [문서](./docs) | [버그 리포트](https://github.com/crossx362-commits/petnna/issues)

</div>

---

## 📋 목차

- [소개](#-소개)
- [주요 기능](#-주요-기능)
- [기술 스택](#-기술-스택)
- [시작하기](#-시작하기)
- [프로젝트 구조](#-프로젝트-구조)
- [개발](#-개발)
- [배포](#-배포)
- [기여](#-기여)
- [라이선스](#-라이선스)

---

## 🎯 소개

**Petnna(펫과나)**는 AI 기술을 활용한 올인원 반려동물 케어 플랫폼입니다. 사진 한 장으로 건강을 분석하고, GPS로 산책을 기록하며, 사주팔자로 성향을 파악하고, 소셜 기능으로 다른 반려동물 가족들과 소통할 수 있습니다.

### ✨ 핵심 가치

- 🏥 **AI 건강 분석**: Gemini API를 활용한 즉각적인 건강 상태 분석
- 🗺️ **스마트 산책**: GPS 기반 실시간 산책 추적 및 칼로리 계산
- ☯️ **성향 분석**: 사주팔자 기반 반려동물 성격 및 궁합 분석
- 📖 **디지털 일기장**: 스티커와 필터로 꾸미는 커스텀 일기장
- 👥 **소셜 네트워크**: 이웃 반려동물 가족들과의 교류 및 정보 공유
- 🛍️ **통합 쇼핑**: 맞춤형 제품 추천 및 구매

---

## 🚀 주요 기능

### 1. 마이펫 (My Pet)
- 반려동물 프로필 관리
- 건강 대시보드
- 주간/월간 건강 리포트
- 성장 기록 추적

### 2. AI 건강 분석
```javascript
// Gemini API를 활용한 건강 분석
const analyzeHealth = async (imageData) => {
  const result = await geminiAPI.analyze({
    image: imageData,
    prompt: "반려동물의 건강 상태를 분석해주세요"
  });
  return {
    score: result.healthScore,
    recommendations: result.suggestions
  };
};
```

### 3. 산책 GPS 트래킹
- 실시간 위치 추적
- 거리 및 칼로리 계산
- 산책 경로 저장 및 공유
- 마킹 위치 기록 (💩💦👃)

### 4. 사주팔자 성향 분석
- 생년월일 기반 사주 계산
- 성격 특성 분석
- 다른 반려동물과의 궁합도
- 맞춤형 케어 조언

### 5. 디지털 일기장
- 📸 사진/영상 업로드
- 🎨 스티커 및 필터 적용
- 💬 말풍선 추가
- 📄 PDF 내보내기
- 👥 친구와 공유

### 6. 소셜 기능
- 이웃 집사 프로필 보기
- 1:1 대화
- 피드 공유
- 좋아요 및 댓글

---

## 💻 기술 스택

### Frontend
- **HTML5** - 시맨틱 마크업
- **Tailwind CSS** - 유틸리티 우선 스타일링
- **JavaScript (ES6+)** - 모던 JavaScript
- **PWA** - 프로그레시브 웹 앱

### Libraries & APIs
- **Leaflet.js** - 지도 및 GPS 트래킹
- **Chart.js** - 데이터 시각화
- **jsPDF** - PDF 생성
- **Gemini API** - AI 건강 분석
- **Supabase** - 실시간 데이터베이스

### Backend & Infrastructure
- **Supabase**
  - PostgreSQL 데이터베이스
  - 실시간 동기화
  - 인증 및 권한 관리
- **Vercel**
  - 호스팅 및 배포
  - Edge Functions
  - 환경 변수 관리

---

## 🏁 시작하기

### 필수 요구사항

- Node.js 18.x 이상
- npm 또는 yarn
- Git

### 설치

```bash
# 1. 리포지토리 클론
git clone https://github.com/crossx362-commits/petnna.git
cd petnna

# 2. 환경 변수 설정
cp .env.example .env
# .env 파일을 편집하여 API 키 입력

# 3. 의존성 설치 (선택사항)
npm install

# 4. 로컬 서버 실행
# Live Server 확장 프로그램 사용 또는
npx serve .
```

### 환경 변수

`.env` 파일에 다음 값들을 설정하세요:

```bash
# Supabase
SUPABASE_URL=your_supabase_url
SUPABASE_ANON_KEY=your_supabase_anon_key

# Google AI (Gemini)
GEMINI_API_KEY=your_gemini_api_key

# Stripe (결제)
STRIPE_PAYMENT_LINK=your_stripe_payment_link
STRIPE_SHOP_PAYMENT_LINK=your_stripe_shop_payment_link
```

---

## 📁 프로젝트 구조

```
petnna/
├── index.html              # 메인 HTML 파일
├── manifest.json           # PWA 매니페스트
├── sw.js                   # 서비스 워커
├── js/
│   ├── app.js             # 메인 애플리케이션 로직
│   ├── state.js           # 전역 상태 관리
│   ├── supabase.js        # Supabase 클라이언트
│   ├── mypet.js           # 마이펫 기능
│   ├── walk.js            # 산책 GPS 기능
│   ├── saju.js            # 사주팔자 분석
│   ├── social.js          # 소셜 기능
│   ├── album.js           # 일기장 기능
│   ├── shop.js            # 쇼핑 기능
│   ├── settings.js        # 설정
│   ├── ai-health.js       # AI 건강 분석
│   ├── freemium.js        # 프리미엄 기능
│   └── templates/         # HTML 템플릿
├── docs/                   # 문서
└── DEVELOPMENT_REPORT.md   # 개발 보고서
```

---

## 🛠️ 개발

### 개발 서버 실행

```bash
# Live Server 사용 (VS Code)
# 또는
npx serve -p 3000
```

### 주요 개발 가이드

#### 1. 새로운 기능 추가

```javascript
// js/my-feature.js
function initMyFeature() {
    // 초기화 로직
}

function renderMyFeature() {
    // 렌더링 로직
}

// 전역 노출
window.initMyFeature = initMyFeature;
```

#### 2. Supabase 데이터 연동

```javascript
// 데이터 가져오기
const { data, error } = await supabaseClient
    .from('table_name')
    .select('*')
    .eq('user_id', userId);

// 데이터 저장
const { error } = await supabaseClient
    .from('table_name')
    .insert([{ column: value }]);
```

#### 3. 상태 관리

```javascript
// 상태 읽기
const currentPet = pets[0];

// 상태 업데이트
pets[0].name = "새 이름";
saveState(); // localStorage에 저장
```

---

## 🚀 배포

### Vercel 배포

```bash
# Vercel CLI 설치
npm i -g vercel

# 배포
vercel --prod
```

### 환경 변수 설정

Vercel 대시보드에서 환경 변수를 설정하세요:

1. 프로젝트 선택
2. Settings → Environment Variables
3. 모든 `.env` 변수 추가

---

## 📊 최근 업데이트

### v1.0.0 (2026-06-01)

#### 🎉 새로운 기능
- ✅ **이웃 집사 프로필 모달**: 소셜 기능 강화
- ✅ **PDF 내보내기**: jsPDF 라이브러리 연동
- ✅ **일기 공유**: Supabase 실시간 연동

#### 🔧 개선사항
- ✅ **GPS 알고리즘 최적화**: 70% 정확도 향상
- ✅ **칼로리 계산**: MET 기반 과학적 계산
- ✅ **안정성 향상**: slice 에러 100% 방지

#### 📈 성능
- GPS 정확도: **70% 향상**
- 이상 좌표 필터링: **85% 향상**
- 칼로리 계산: **과학적 기준 충족**

자세한 내용은 [DEVELOPMENT_REPORT.md](./DEVELOPMENT_REPORT.md)를 참조하세요.

---

## 🤝 기여

기여를 환영합니다! 다음 단계를 따라주세요:

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### 커밋 메시지 규칙

```
feat: 새로운 기능 추가
fix: 버그 수정
docs: 문서 수정
style: 코드 포맷팅
refactor: 코드 리팩토링
test: 테스트 코드
chore: 빌드 업무 수정
```

---

## 📄 라이선스

**Copyright © 2026 Petnna. All rights reserved.**

이 프로젝트는 상업적 용도로 제작되었으며, 모든 권리는 Petnna에 있습니다.

---

## 📞 문의

- **이메일**: junholee@gpun.co.kr
- **GitHub**: [@crossx362-commits](https://github.com/crossx362-commits)
- **웹사이트**: [petnna.vercel.app](https://petnna.vercel.app)

---

## 🙏 감사의 말

이 프로젝트는 다음 오픈소스 프로젝트들의 도움을 받았습니다:

- [Tailwind CSS](https://tailwindcss.com/)
- [Leaflet.js](https://leafletjs.com/)
- [Chart.js](https://www.chartjs.org/)
- [jsPDF](https://github.com/parallax/jsPDF)
- [Supabase](https://supabase.com/)

---

<div align="center">

**Made with ❤️ for pets and their families**

🐾 **Petnna** - 펫과 함께 사는 삶

</div>
