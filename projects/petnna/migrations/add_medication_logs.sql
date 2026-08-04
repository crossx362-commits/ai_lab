-- ============================================================
-- Migration: medication_logs (투약 이행 로그)
-- 오너 승인 2026-08-04 ("막힌거 8개 진행해").
-- 대응 백로그: 나무_20260712200231_0 투약 이행 로그 + '오늘의 케어' 홈 넛지
--
-- 목적: care_scheduler가 "오늘 09:00 심장사상충"을 띄우기는 하는데, 실제로 먹였는지는
--       lastCompleted 한 필드(localStorage)에만 남아 연속일·이행률을 계산할 수 없다.
--       날짜별 1행으로 쌓아 복약 연속일과 월 이행률을 낸다.
--
-- 설계:
--  · 하루 한 일정당 한 행 — UNIQUE로 중복 체크 방지(연타·기기 두 대 동시 체크).
--  · schedule_id는 care_schedules(로컬)의 id를 문자열로 받는다. 로컬 스케줄러를
--    서버로 옮기는 건 별개 과제라, 여기선 '이행 기록'만 서버에 둔다.
--  · taken=false도 기록한다(건너뜀). 이행률 분모를 프론트 추정에 맡기지 않기 위함.
-- ============================================================

CREATE TABLE IF NOT EXISTS public.medication_logs (
    id          BIGINT PRIMARY KEY,                        -- 클라이언트 생성 id
    user_id     UUID NOT NULL DEFAULT auth.uid(),
    email       TEXT NOT NULL,
    pet_id      TEXT,
    schedule_id TEXT NOT NULL,                             -- care_schedules 항목 id
    kind        TEXT,                                      -- medicine | vaccine | feed | walk
    title       TEXT,                                      -- 표시용(스케줄 삭제 후에도 이력 보존)
    log_date    DATE NOT NULL,                             -- YYYY-MM-DD (연속일·이행률 기준)
    taken       BOOLEAN NOT NULL DEFAULT true,             -- false = 건너뜀(이행률 분모 유지)
    taken_at    TIMESTAMP WITH TIME ZONE,
    created_at  TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL,
    UNIQUE (user_id, pet_id, schedule_id, log_date)
);

CREATE INDEX IF NOT EXISTS medication_logs_user_date
    ON public.medication_logs (user_id, pet_id, log_date DESC);

ALTER TABLE public.medication_logs ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
  CREATE POLICY "medlog_select_own" ON public.medication_logs FOR SELECT USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "medlog_insert_own" ON public.medication_logs FOR INSERT WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "medlog_update_own" ON public.medication_logs FOR UPDATE USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "medlog_delete_own" ON public.medication_logs FOR DELETE USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
