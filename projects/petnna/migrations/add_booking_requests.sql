-- ============================================================
-- Migration: booking_requests (원격상담 예약 요청 브릿지)
-- 오너 승인 2026-08-04 ("막힌거 8개 진행해").
-- 대응 백로그: 나무_20260723111049_3 원격상담 예약 요청 브릿지
--
-- 목적: 인근 병원에 상담 요청을 남기고 이력을 보관한다. 실제 전송(전화·메일)은
--       앱이 하지 않는다 — **요청 폼과 이력만** 보관하고 연락은 보호자가 직접 한다.
--       외부로 자동 발송하는 순간 오배송·개인정보 전달 책임이 생기므로 범위를 좁혔다.
--
-- 병원 정보는 nearby-places(카카오/로컬 검색)에서 고른 값을 문자열로 복사해 둔다 —
-- 병원 마스터 테이블을 만들면 갱신 책임이 생기고, 이 기능엔 불필요하다.
-- ============================================================

CREATE TABLE IF NOT EXISTS public.booking_requests (
    id            BIGINT PRIMARY KEY,                      -- 클라이언트 생성 id
    user_id       UUID NOT NULL DEFAULT auth.uid(),
    email         TEXT NOT NULL,
    pet_id        TEXT,
    clinic_name   TEXT NOT NULL,
    clinic_phone  TEXT,                                    -- 병원 공개 번호(보호자 개인정보 아님)
    preferred_at  TIMESTAMP WITH TIME ZONE,                -- 희망 일시
    symptom       TEXT,                                    -- 상담 요청 내용
    status        TEXT NOT NULL DEFAULT 'draft',           -- draft | sent | done | canceled
    created_at    TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

CREATE INDEX IF NOT EXISTS booking_requests_user_created
    ON public.booking_requests (user_id, created_at DESC);

ALTER TABLE public.booking_requests ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
  CREATE POLICY "booking_select_own" ON public.booking_requests FOR SELECT USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "booking_insert_own" ON public.booking_requests FOR INSERT WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "booking_update_own" ON public.booking_requests FOR UPDATE USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "booking_delete_own" ON public.booking_requests FOR DELETE USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
