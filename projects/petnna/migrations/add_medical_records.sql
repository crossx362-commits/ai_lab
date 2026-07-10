-- ============================================================
-- Migration: medical_records 테이블 + 사진 storage (건강수첩 정식판)
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
--       (사진 버킷은 아래 STORAGE 안내대로 별도 생성)
-- 목적: care-scheduler(일정)와 분리된 지속형 의료기록 — 방문일·진단·처방·
--       진료비·영수증/검사지 사진을 시간순 아카이브. 월간 리포트 병원비 요약.
-- 접근: 본인 데이터만(공개 없음). 오너 승인 2026-07-10.
-- 참고: 스키마-무변경 MVP(medical-records.js, localStorage)는 이미 출시됨 →
--       정식판 병합 후 localStorage 기록을 이 테이블로 1회 이관.
-- ============================================================

CREATE TABLE IF NOT EXISTS public.medical_records (
    id          BIGINT PRIMARY KEY,                       -- 클라이언트 생성 id
    user_id     UUID NOT NULL DEFAULT auth.uid(),
    email       TEXT NOT NULL,
    pet_id      TEXT,
    category    TEXT NOT NULL,                             -- visit|vaccine|checkup|surgery|medication|other
    visit_date  DATE NOT NULL,
    title       TEXT NOT NULL,
    detail      TEXT,
    hospital    TEXT,
    cost        NUMERIC,                                   -- 진료비(원). 월간 리포트 합산
    photos      JSONB NOT NULL DEFAULT '[]'::jsonb,        -- storage 경로 배열(영수증·검사지)
    created_at  TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

CREATE INDEX IF NOT EXISTS medical_records_user_date
    ON public.medical_records (user_id, visit_date DESC);

ALTER TABLE public.medical_records ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
  CREATE POLICY "medical_records_select_own" ON public.medical_records FOR SELECT USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "medical_records_insert_own" ON public.medical_records FOR INSERT WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "medical_records_update_own" ON public.medical_records FOR UPDATE USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "medical_records_delete_own" ON public.medical_records FOR DELETE USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- ============================================================
-- STORAGE (사진 아카이브) — Dashboard → Storage에서 수행
-- 1) 새 버킷 'medical' 생성, Public: OFF (비공개)
-- 2) 아래 정책으로 "본인 폴더(<user_id>/...)만" 접근 허용:
--
-- CREATE POLICY "medical_photos_rw_own" ON storage.objects
--   FOR ALL TO authenticated
--   USING (bucket_id = 'medical' AND (storage.foldername(name))[1] = auth.uid()::text)
--   WITH CHECK (bucket_id = 'medical' AND (storage.foldername(name))[1] = auth.uid()::text);
--
-- 프론트는 `${user_id}/${record_id}/${filename}` 경로로 업로드, 조회는 signed URL 사용.
-- ============================================================
