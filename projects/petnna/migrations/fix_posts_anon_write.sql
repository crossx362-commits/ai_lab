-- ============================================================
-- Migration: posts 익명 쓰기 범위 축소 (오너 지시 2026-08-08 "알아서 정리좀 해")
--
-- 무엇이 문제였나(익명 키로 실측)
-- --------------------------------------------------------
-- anon 키(index.html에 평문으로 실려 배포되므로 사실상 공개)로
--   · INSERT 성공(201) — 아무나 피드에 글 도배 가능
--   · UPDATE 성공(200) — 아무나 남의 글 **내용을 변조** 가능
--   · DELETE 성공      — 아무나 남의 글 **삭제** 가능
-- 51개 글 전체가 이 상태에 노출돼 있었다.
--
-- 왜 전부 막지 않는가
-- --------------------------------------------------------
-- AI 이웃 에이전트(petnna_social_agent.py)가 **anon 키로** 피드를 운영한다:
--   · POST posts                      — 페르소나 글 발행 (petnna_social_agent.py:209)
--   · PATCH posts?id=eq.N {likes, comments} — 실 유저 글에 좋아요·댓글 (같은 파일:241)
-- service_role 키는 이 저장소 env에 없다. INSERT/UPDATE를 통째로 막으면 이웃 활동이
-- 죽는다(오너가 2026-07-11에 "정체된 피드 활성화"로 도입한 기능).
--
-- 해법: RLS는 행 단위라 컬럼을 못 가린다 → **컬럼 권한(GRANT)**으로 좁힌다.
--   · UPDATE는 likes·comments 두 컬럼에만 허용 → 이웃의 좋아요·댓글은 그대로 동작하고
--     content·image·pet_name 변조는 권한 부족으로 거부된다.
--   · DELETE는 정책 자체를 제거 → 가장 파괴적인 경로를 닫는다.
--   · INSERT는 유지 → 이웃 글 발행. 도배 위험은 남지만 되돌릴 수 있는 종류다.
--
-- 남은 리스크(의도적으로 남김): 익명 INSERT로 도배 가능. 막으려면 이웃 에이전트를
-- service_role 키로 전환해야 하고, 그건 최고 권한 자격증명을 새로 들이는 일이라
-- 오너 판단 사항이다.
--
-- 되돌리기: GRANT UPDATE ON public.posts TO anon; 로 전체 컬럼 권한 복원 +
--           DELETE 정책 재생성.
-- ============================================================

-- 1) 삭제 경로 제거 — 익명이 남의 글을 지울 이유가 없다.
DO $$
DECLARE p record;
BEGIN
  FOR p IN SELECT policyname FROM pg_policies
           WHERE schemaname='public' AND tablename='posts' AND cmd='DELETE'
  LOOP
    EXECUTE format('DROP POLICY IF EXISTS %I ON public.posts', p.policyname);
  END LOOP;
END $$;

-- 로그인 사용자는 자기 글을 지울 수 있어야 하므로 authenticated 경로는 남긴다.
DO $$ BEGIN
  CREATE POLICY "posts_delete_authenticated" ON public.posts
      FOR DELETE TO authenticated USING (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- 2) 익명 UPDATE를 likes·comments 컬럼으로만 좁힌다(이웃 좋아요·댓글용).
REVOKE UPDATE ON public.posts FROM anon;
GRANT  UPDATE (likes, comments) ON public.posts TO anon;

-- 3) 로그인 사용자는 전체 컬럼 수정 유지(자기 글 편집).
GRANT UPDATE ON public.posts TO authenticated;
