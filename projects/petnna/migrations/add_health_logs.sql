-- ============================================================
-- Migration: health_logs 테이블 추가 (예측 웰니스 이상감지)
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- 목적: 지금까지 localStorage 전용이던 체중·음수·식사 로그를 서버에 저장해
--       기기 간 이력 동기화 + z-score 이상감지의 데이터 소스로 사용.
-- 접근: 본인 데이터만(공개 없음). 오너 승인 2026-07-10.
-- ============================================================

CREATE TABLE IF NOT EXISTS public.health_logs (
    id          BIGINT PRIMARY KEY,                       -- 클라이언트 생성 id(Date.now 기반)
    user_id     UUID NOT NULL DEFAULT auth.uid(),
    email       TEXT NOT NULL,
    pet_id      TEXT,                                      -- 다견 구분(없으면 기본 펫)
    type        TEXT NOT NULL,                             -- weight | water | food | ...
    value       NUMERIC NOT NULL,
    unit        TEXT,                                      -- kg | ml | g ...
    logged_at   TIMESTAMP WITH TIME ZONE NOT NULL,         -- 실제 측정 시각(정렬·시계열 기준)
    created_at  TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

CREATE INDEX IF NOT EXISTS health_logs_user_type_time
    ON public.health_logs (user_id, type, logged_at);

ALTER TABLE public.health_logs ENABLE ROW LEVEL SECURITY;

-- 본인 데이터만 CRUD (공개 정책 없음)
DO $$ BEGIN
  CREATE POLICY "health_logs_select_own" ON public.health_logs FOR SELECT USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "health_logs_insert_own" ON public.health_logs FOR INSERT WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "health_logs_update_own" ON public.health_logs FOR UPDATE USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "health_logs_delete_own" ON public.health_logs FOR DELETE USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
