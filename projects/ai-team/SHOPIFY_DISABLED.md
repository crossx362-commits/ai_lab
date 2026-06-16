# 🛑 Shopify 관련 업무 중단

> **중단 일시**: 2026-06-16  
> **사유**: 업무 우선순위 변경 - 암호화폐 트레이딩 집중

---

## 중단된 기능

### 1. 현빈 - Shopify 드랍쉽핑 관리
- ❌ `shopify_manager.py` (비활성화)
- ❌ `shopify_oauth.py` (비활성화)
- **기능**: 주문 감지, 매출 모니터링, 드랍쉽 이행 알림

### 2. 아린 - Shopify 제품 사진 업로드
- ❌ `shopify_products.py` (비활성화)
- **기능**: Instagram 콘텐츠 → Shopify 제품 이미지 업로드

### 3. 티모 - Shopify 디자인
- ❌ `shopify_design.py` (비활성화)
- **기능**: 스토어 디자인 관리

### 4. 드랍쉽핑 소싱 도구
- ⚠️ `aliexpress_fulfillment.py` (유지, 미사용)
- ⚠️ `zendrop_sourcing.py` (유지, 미사용)

---

## 현재 활성 미션

### 현빈 (전략가)
- ✅ **암호화폐 시장 정보 수집** (최우선)
  - 연준 일정, 공포탐욕지수, 김치프리미엄
  - 데이브, 레오에게 실시간 제공
- 📋 비즈니스 리서치 (선택적)

### 데이브 (보수적 트레이더)
- ✅ Upbit 자동 매매
- ✅ 현빈 정보 기반 리스크 관리

### 레오 (공격적 트레이더)
- ✅ 고변동성 알트코인 단타
- ✅ 현빈 정보 기반 진입 판단

---

## 재활성화 방법

필요 시 아래 파일명에서 `.disabled` 제거:

```bash
# 현빈
mv shopify_manager.py.disabled shopify_manager.py
mv shopify_oauth.py.disabled shopify_oauth.py

# 아린
mv shopify_products.py.disabled shopify_products.py

# 티모
mv shopify_design.py.disabled shopify_design.py
```

그리고 `SKILL.md` 파일에서 Shopify 미션 복원.

---

## 스토어 정보 (보관용)

- **URL**: `swiftcart-101711.myshopify.com`
- **상태**: 유지 (자동화 중단)
- **주문 처리**: 수동

---

**마지막 업데이트**: 2026-06-16
