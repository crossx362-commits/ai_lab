-- ============================================================
-- Migration: lost_pets (실종 신고 · 목격 제보 · 재회)
-- 오너 승인 2026-08-04 ("막힌거 8개 진행해").
-- 대응 백로그 3건을 하나로 통합 — 같은 기능을 나무가 세 번 제안했다:
--   나무_20260713111056_4 실종 모드(Lost Mode) + 이웃 제보
--   나무_20260716110717_2 실종 반려동물 제보·목격 보드
--   나무_20260722110259_2 실종·발견 알림 지도 게시판
--
-- 목적: 실종 신고를 남기면 QR 공개 프로필(public_pet_profiles)이 실종 배너로 전환되고,
--       petlife 지도에 핀으로 뜬다. 목격 제보는 같은 테이블에 kind='sighting'으로 쌓는다.
--
-- 공개 범위(중요): 지금은 가입이 오너 1인으로 잠겨 있어(restrict_signups_allowlist)
--   '이웃 제보'는 아직 성립하지 않는다. 그래서 **공개 read 정책을 넣지 않는다** —
--   본인만 읽고 쓴다. 다중 사용자를 열 때 아래 주석의 정책을 추가하면 된다.
--   지금 공개 정책을 미리 넣으면 연락처·위치가 anon key로 전부 읽히는 상태가 된다.
--
--   -- 공개 시 추가할 정책(지금은 넣지 않는다):
--   -- CREATE POLICY "lost_select_active" ON public.lost_pets
--   --   FOR SELECT USING (status = 'lost');
--
-- 개인정보: 연락처는 원문 대신 마스킹본(contact_masked)만 저장한다 —
--   public_pet_profiles와 같은 원칙(2026-07-10 오너 지시 "최소 노출·연락처 마스킹").
-- ============================================================

CREATE TABLE IF NOT EXISTS public.lost_pets (
    id              BIGINT PRIMARY KEY,                    -- 클라이언트 생성 id
    user_id         UUID NOT NULL DEFAULT auth.uid(),
    email           TEXT NOT NULL,
    pet_id          TEXT,                                  -- 내 펫 실종 신고일 때만
    kind            TEXT NOT NULL DEFAULT 'lost',          -- lost(신고) | sighting(목격)
    status          TEXT NOT NULL DEFAULT 'lost',          -- lost | resolved
    pet_name        TEXT,                                  -- 목격 제보는 펫 id가 없으므로 표시용
    breed           TEXT,
    lat             NUMERIC,                               -- 마지막 위치 / 목격 위치
    lng             NUMERIC,
    place_label     TEXT,                                  -- "○○공원 근처" 등 사람이 읽는 표기
    note            TEXT,
    contact_masked  TEXT,                                  -- 원문 전화번호 저장 금지(마스킹본만)
    happened_at     TIMESTAMP WITH TIME ZONE,              -- 실종/목격 시각
    resolved_at     TIMESTAMP WITH TIME ZONE,              -- 재회 시각
    created_at      TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL
);

CREATE INDEX IF NOT EXISTS lost_pets_user_created
    ON public.lost_pets (user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS lost_pets_status
    ON public.lost_pets (status, created_at DESC);

ALTER TABLE public.lost_pets ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
  CREATE POLICY "lost_select_own" ON public.lost_pets FOR SELECT USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "lost_insert_own" ON public.lost_pets FOR INSERT WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "lost_update_own" ON public.lost_pets FOR UPDATE USING (auth.uid() = user_id) WITH CHECK (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY "lost_delete_own" ON public.lost_pets FOR DELETE USING (auth.uid() = user_id);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
