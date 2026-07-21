-- ============================================================
-- Migration: 가입 허용 이메일 화이트리스트 (auth.users BEFORE INSERT 트리거)
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run (오너 승인 필요)
-- 목적: 오너 지시(2026-07-21 "나 말고 다른사람 로그인 못하게") — 허용 목록 밖
--       이메일의 신규 가입(이메일/카카오/구글 전부 auth.users INSERT를 거침)을
--       서버에서 거부한다. 클라이언트 게이트(app.js ALLOWED_LOGIN_EMAILS)는
--       anon key로 API를 직접 부르면 우회되므로, 이 트리거가 진짜 방어선이다.
--
-- 허용 이메일 추가/변경: 아래 allowed 배열만 고쳐 재실행(CREATE OR REPLACE라 멱등).
--
-- 참고: 이미 가입된 비허용 계정의 "로그인"은 트리거로 막을 수 없다(INSERT가 아님).
--       기존 계정 정리는 맨 아래 조회로 확인 후 Dashboard → Authentication → Users에서
--       수동 삭제. 또한 Dashboard → Authentication → Sign In / Up 에서
--       "Allow new users to sign up"을 꺼두면 이 트리거와 이중 방어가 된다.
-- ============================================================

CREATE OR REPLACE FUNCTION public.enforce_signup_allowlist()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    allowed text[] := ARRAY[
        'crossx362@gmail.com'
    ];
BEGIN
    IF lower(NEW.email) <> ALL (SELECT lower(a) FROM unnest(allowed) AS a) THEN
        RAISE EXCEPTION 'signup not allowed for %', NEW.email
            USING ERRCODE = 'P0001';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS enforce_signup_allowlist ON auth.users;
CREATE TRIGGER enforce_signup_allowlist
    BEFORE INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.enforce_signup_allowlist();

-- ── 기존 비허용 계정 확인(수동 정리용 조회 — 삭제는 Dashboard에서) ──
-- SELECT id, email, created_at FROM auth.users
--  WHERE lower(email) NOT IN ('crossx362@gmail.com')
--  ORDER BY created_at;
