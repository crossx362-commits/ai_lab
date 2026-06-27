-- ============================================================
-- Migration: 앨범 프라이버시 수정
-- 기존 'albums_select_shared USING (true)'는 모든 앨범을 누구에게나 노출했음.
-- is_public 플래그를 추가하고, 명시적으로 공개한 앨범만 외부 조회 허용.
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- ============================================================

-- 1) 공개 여부 컬럼 추가 (없으면)
ALTER TABLE public.albums
    ADD COLUMN IF NOT EXISTS is_public BOOLEAN NOT NULL DEFAULT false;

-- 2) 전체공개 정책 제거 후 is_public 기반으로 재생성
DROP POLICY IF EXISTS "albums_select_shared" ON public.albums;
CREATE POLICY "albums_select_shared" ON public.albums FOR SELECT USING (is_public);

-- 참고: 본인 앨범은 별도 정책 "albums_select_own"(auth.uid() = user_id)으로 계속 조회 가능.
