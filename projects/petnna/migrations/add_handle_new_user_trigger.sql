-- ============================================================
-- Migration: handle_new_user 트리거 — 가입 시 profiles 행 자동 생성
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run (오너 승인 필요)
-- 목적: 회원가입(auth.users INSERT) 시 public.profiles 행을 자동 생성한다.
--
-- 배경(백호 조사 2026-07-11, 과제 회의_202607082053_5):
--   app.js signUp은 auth.users만 만들고 profiles insert가 없다. profiles 행은
--   사용자가 설정을 바꿔 updateProfile(upsert)을 호출할 때만 lazy 생성되므로,
--   설정을 한 번도 건드리지 않은 계정은 영구 0행이 된다(butler 계정 재현).
--   또 그 upsert는 user_id를 안 넣고 DEFAULT auth.uid()에 의존해, fallback
--   로그인 등 세션이 실인증이 아니면 auth.uid()=NULL로 RLS insert가 막히고
--   에러가 조용히 삼켜진다(supabase.js).
--
-- 해법: SECURITY DEFINER 트리거로 서버 측에서 행을 만든다. 테이블 소유자
--   권한으로 실행되므로 RLS·클라이언트 세션 상태와 무관하게 항상 성공한다.
--   신규 테이블 불필요 — profiles는 이미 존재한다.
--
-- signUp이 넘기는 메타데이터: options.data.nickname → raw_user_meta_data->>'nickname'
--
-- 멱등: 재실행 안전(트리거·함수 CREATE OR REPLACE, 기존 0행 계정 백필 ON CONFLICT).
-- ============================================================

-- 1) 가입 시 profiles 행을 만드는 함수 (RLS 우회 위해 SECURITY DEFINER)
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO public.profiles (user_id, email, nickname)
    VALUES (
        NEW.id,
        NEW.email,
        COALESCE(NEW.raw_user_meta_data->>'nickname', split_part(NEW.email, '@', 1))
    )
    ON CONFLICT (user_id) DO NOTHING;
    RETURN NEW;
END;
$$;

-- 2) auth.users INSERT 후 위 함수 호출
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- 3) 트리거 도입 전 가입한 기존 0행 계정 백필 (butler 등)
INSERT INTO public.profiles (user_id, email, nickname)
SELECT u.id, u.email,
       COALESCE(u.raw_user_meta_data->>'nickname', split_part(u.email, '@', 1))
FROM auth.users u
LEFT JOIN public.profiles p ON p.user_id = u.id
WHERE p.user_id IS NULL
  AND u.email IS NOT NULL
ON CONFLICT (user_id) DO NOTHING;

-- ============================================================
-- 적용 후 프론트 후속(백호 권고 — DB 선적용 없이 병합 금지):
--   supabase.js updateProfile upsert에 user_id를 명시(auth.uid() 의존 제거),
--   insert 실패를 조용히 삼키지 말고 사용자에게 노출. 이 트리거 적용 뒤 별도 과제.
-- ============================================================
