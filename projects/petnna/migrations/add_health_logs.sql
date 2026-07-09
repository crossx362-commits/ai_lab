-- ============================================================
-- Migration: health_logs 테이블 추가 (예측 웰니스 이상감지)
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- 목적: 지금까지 localStorage 전용이던 일일 건강 기록(체중 대신 음수·식사·배변·컨디션)을
--       서버에 저장해 기기 간 이력 동기화 + z-score 이상감지의 데이터 소스로 사용.
-- 모델: 앱의 healthLogs.history와 1:1 — 펫·날짜당 한 행(일 단위 집계).
-- 접근: 본인 데이터만(공개 없음). 오너 승인 2026-07-10.
-- ============================================================

CREATE TABLE IF NOT EXISTS public.health_logs (
    id          BIGINT PRIMARY KEY,                       -- 클라이언트 생성 id
    user_id     UUID NOT NULL DEFAULT auth.uid(),
    email       TEXT NOT NULL,
    pet_id      TEXT,                                      -- 다견 구분(없으면 기본 펫)
    log_date    DATE NOT NULL,                             -- YYYY-MM-DD (정렬·시계열 기준)
    water       NUMERIC,                                   -- ml
    food        NUMERIC,                                   -- g
    poop        TEXT,                                      -- normal | hard | liquid | null
    condition   TEXT,                                      -- happy | tired | sick
    created_at  TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL,
    UNIQUE (user_id, pet_id, log_date)                     -- 하루 한 행(upsert 대상)
);

CREATE INDEX IF NOT EXISTS health_logs_user_date
    ON public.health_logs (user_id, pet_id, log_date);

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
