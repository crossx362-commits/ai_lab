-- ============================================================
-- Migration: 오류 로그 원격 수집 (error_logs)
-- 실사용자 클라이언트 오류를 개발팀이 확인할 수 있도록 원격 수집.
-- AppLogger.addErrorLog가 익명(anon)으로 INSERT만 수행한다.
-- 개인정보는 저장하지 않으며, anon은 SELECT 불가(개발팀은 service_role로 조회).
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- ============================================================

-- 1) 테이블 생성 (없으면)
CREATE TABLE IF NOT EXISTS public.error_logs (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    type        TEXT,
    message     TEXT,
    stack       TEXT,
    url         TEXT,
    user_agent  TEXT,
    session_id  TEXT
);

-- 2) RLS 활성화
ALTER TABLE public.error_logs ENABLE ROW LEVEL SECURITY;

-- 3) anon은 INSERT만 허용 (SELECT/UPDATE/DELETE 정책 없음 → 조회·수정·삭제 차단)
DROP POLICY IF EXISTS "error_logs_insert_anon" ON public.error_logs;
CREATE POLICY "error_logs_insert_anon" ON public.error_logs
    FOR INSERT TO anon
    WITH CHECK (true);
