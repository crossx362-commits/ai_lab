# 🔧 Supabase 테이블 설정 가이드

## ⚠️ 현재 문제
```
❌ profiles 테이블 없음
❌ albums 테이블 없음  
❌ 피드 타임라인 타임아웃
```

## ✅ 해결 방법

### 1. Supabase 대시보드 접속
https://supabase.com/dashboard → **nlgjsdffgkygaylbjooc** 프로젝트

### 2. SQL Editor 열기
Dashboard → SQL Editor → New Query

### 3. 스키마 실행
아래 SQL을 복사해서 실행:

```sql
-- 3. profiles 테이블
CREATE TABLE IF NOT EXISTS public.profiles (
    user_id UUID PRIMARY KEY DEFAULT auth.uid(),
    email TEXT UNIQUE NOT NULL,
    nickname TEXT,
    avatar TEXT,
    photo_url TEXT,
    theme TEXT,
    unit TEXT,
    notifications_enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

-- 4. albums 테이블
CREATE TABLE IF NOT EXISTS public.albums (
    id BIGINT PRIMARY KEY,
    user_id UUID NOT NULL DEFAULT auth.uid(),
    email TEXT NOT NULL,
    data JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

-- RLS 활성화
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.albums   ENABLE ROW LEVEL SECURITY;

-- profiles 정책
CREATE POLICY "profiles_select_own" ON public.profiles FOR SELECT  USING (auth.uid() = user_id);
CREATE POLICY "profiles_insert_own" ON public.profiles FOR INSERT  WITH CHECK (auth.uid() = user_id);
CREATE POLICY "profiles_update_own" ON public.profiles FOR UPDATE  USING (auth.uid() = user_id);
CREATE POLICY "profiles_delete_own" ON public.profiles FOR DELETE  USING (auth.uid() = user_id);

-- albums 정책
CREATE POLICY "albums_select_own"    ON public.albums FOR SELECT  USING (auth.uid() = user_id);
CREATE POLICY "albums_select_shared" ON public.albums FOR SELECT  USING (true);
CREATE POLICY "albums_insert_own"    ON public.albums FOR INSERT  WITH CHECK (auth.uid() = user_id);
CREATE POLICY "albums_update_own"    ON public.albums FOR UPDATE  USING (auth.uid() = user_id);
CREATE POLICY "albums_delete_own"    ON public.albums FOR DELETE  USING (auth.uid() = user_id);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_albums_email ON public.albums (email);
```

### 4. 실행 후 확인
Table Editor에서 `profiles`와 `albums` 테이블이 보이는지 확인

---

## 📋 전체 스키마 (처음 설정 시)

전체 테이블을 한 번에 생성하려면: `supabase_schema.sql` 파일 내용 전체 실행

---

## 🚨 타임아웃 문제 해결

피드 타임라인 쿼리가 느릴 경우:

1. **인덱스 확인**
```sql
CREATE INDEX IF NOT EXISTS idx_posts_created ON public.posts (created_at DESC);
```

2. **쿼리 타임아웃 조정** (Supabase Dashboard → Settings → API)
- Statement timeout: 8000ms → 15000ms

---

## ✅ 완료 체크리스트
- [ ] profiles 테이블 생성
- [ ] albums 테이블 생성
- [ ] RLS 정책 활성화
- [ ] 인덱스 생성
- [ ] 타임아웃 설정 확인
