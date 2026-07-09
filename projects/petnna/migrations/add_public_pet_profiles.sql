-- ============================================================
-- Migration: public_pet_profiles 테이블 (QR 미아방지 공개 프로필)
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- 목적: 반려동물별 읽기전용 공개 프로필(QR 스캔 → 습득자 열람). 미아방지.
-- 오너 승인 2026-07-10: "승인하되 최소 노출·철회 가능·연락처 마스킹".
--
-- 개인정보 보호 설계(스키마에 강제):
--  1) 추측 불가 URL — PK는 클라이언트가 만든 랜덤 토큰(순차 id 아님) → 열거 불가.
--  2) 최소 노출 — 공개 데이터는 public_fields JSONB에 '보호자가 고른 필드만' 저장.
--     고르지 않은 항목은 애초에 저장 안 됨 → 유출 불가(민감정보 미보관).
--  3) 철회 가능 — is_public=false로 즉시 비공개(공개 read 차단) + 행 삭제 가능.
--  4) 연락처 마스킹 — 원문 전화번호 저장 금지. 프론트가 마스킹한 contact_masked만 보관.
--  * email 등 보호자 식별정보는 이 테이블에 두지 않음(공개 read 노출 방지).
-- ============================================================

CREATE TABLE IF NOT EXISTS public.public_pet_profiles (
    token          TEXT PRIMARY KEY,                       -- 랜덤 토큰(공개 URL /p/<token>)
    user_id        UUID NOT NULL DEFAULT auth.uid(),        -- 소유자(UUID만, PII 아님)
    pet_id         TEXT,
    is_public      BOOLEAN NOT NULL DEFAULT true,           -- 철회 토글: false면 공개 read 차단
    public_fields  JSONB NOT NULL DEFAULT '{}'::jsonb,       -- 보호자가 고른 공개 항목만
                                                             -- 예: {name, photo_url, breed, traits, allergies, meds}
    contact_masked TEXT,                                     -- 마스킹된 연락 수단(원문 저장 금지)
    created_at     TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL,
    updated_at     TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

ALTER TABLE public.public_pet_profiles ENABLE ROW LEVEL SECURITY;

-- 공개 read: is_public=true 인 행만 익명 열람 허용(미아방지 핵심)
DO $$ BEGIN
  CREATE POLICY "ppp_select_public" ON public.public_pet_profiles FOR SELECT USING (is_public = true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
-- 소유자는 공개 여부와 무관하게 본인 행 열람
DO $$ BEGIN
  CREATE POLICY "ppp_select_own" ON public.public_pet_profiles FOR SELECT USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
-- 생성·수정·삭제는 소유자만(철회·필드변경 포함)
DO $$ BEGIN
  CREATE POLICY "ppp_insert_own" ON public.public_pet_profiles FOR INSERT WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "ppp_update_own" ON public.public_pet_profiles FOR UPDATE USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "ppp_delete_own" ON public.public_pet_profiles FOR DELETE USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
