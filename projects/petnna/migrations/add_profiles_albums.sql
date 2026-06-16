-- ============================================================
-- Migration: profiles + albums 테이블 추가
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- ============================================================

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

-- profiles RLS 정책
DO $$ BEGIN
  CREATE POLICY "profiles_select_own" ON public.profiles FOR SELECT  USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "profiles_insert_own" ON public.profiles FOR INSERT  WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "profiles_update_own" ON public.profiles FOR UPDATE  USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "profiles_delete_own" ON public.profiles FOR DELETE  USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- albums RLS 정책
DO $$ BEGIN
  CREATE POLICY "albums_select_own"    ON public.albums FOR SELECT  USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "albums_select_shared" ON public.albums FOR SELECT  USING (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "albums_insert_own"    ON public.albums FOR INSERT  WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "albums_update_own"    ON public.albums FOR UPDATE  USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "albums_delete_own"    ON public.albums FOR DELETE  USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_albums_email ON public.albums (email);
