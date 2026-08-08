-- ============================================================
-- Migration: pets 테이블 익명 접근 차단
-- 오너 승인 2026-08-08 ("a,3" — 코드 가드 + pets·albums 익명 읽기 차단)
--
-- 무엇이 문제였나(실측 확인)
-- --------------------------------------------------------
-- pets에 걸린 정책 4개가 전부 `USING (true)` 였다:
--   Allow anon select pets / insert / update / delete
-- anon 키는 index.html에 평문으로 실려 배포되므로 사실상 공개 정보다. 즉 누구나
--   · 반려동물 이름·품종·나이·체중·사주 데이터를 읽고(실측: 6행 노출)
--   · 새 반려동물을 만들고, 남의 것을 고치거나 지울 수 있었다.
--
-- 게다가 앱의 syncPets()는 `select('*')`에 **필터가 없다** — RLS가 유일한 방어선인데
-- 그게 열려 있었으므로, 다중 사용자가 되는 순간 남의 반려동물이 내 앱에 섞여 나온다.
--
-- 왜 authenticated 전용인가(user_id 기반이 아니라)
-- --------------------------------------------------------
-- pets 테이블에는 **user_id 컬럼이 없다**(id·name·breed·type·imageUrl·age·weight·…).
-- 소유자를 식별할 수 없으므로 `auth.uid() = user_id` 정책을 쓸 수 없다. 지금은 오너
-- 1인 사용이라 "로그인한 사람만 접근"으로도 익명 노출은 완전히 막힌다.
-- **가입을 개방하기 전에 user_id 컬럼 추가 + 소유자 정책이 반드시 선행돼야 한다**
-- (그때까지는 로그인 사용자끼리는 서로의 pets가 보인다 — 알고 있는 잔여 리스크).
--
-- 되돌리기: 이 파일의 정책을 DROP 하고 예전 `USING (true)` 정책을 다시 만들면 된다.
-- ============================================================

ALTER TABLE public.pets ENABLE ROW LEVEL SECURITY;

-- 1) 전면 개방 정책 제거
DROP POLICY IF EXISTS "Allow anon select pets" ON public.pets;
DROP POLICY IF EXISTS "Allow anon insert pets" ON public.pets;
DROP POLICY IF EXISTS "Allow anon update pets" ON public.pets;
DROP POLICY IF EXISTS "Allow anon delete pets" ON public.pets;

-- 2) 로그인 사용자 전용으로 재생성
DO $$ BEGIN
  CREATE POLICY "pets_select_authenticated" ON public.pets
      FOR SELECT TO authenticated USING (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
  CREATE POLICY "pets_insert_authenticated" ON public.pets
      FOR INSERT TO authenticated WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
  CREATE POLICY "pets_update_authenticated" ON public.pets
      FOR UPDATE TO authenticated USING (true) WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
  CREATE POLICY "pets_delete_authenticated" ON public.pets
      FOR DELETE TO authenticated USING (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
