-- ============================================================
-- Migration: health_logs에 원탭 컨디션 4개 컬럼 추가
-- 실행: Supabase Dashboard → SQL Editor에 붙여넣고 Run
-- 오너 승인 2026-07-28.
--
-- 배경: health_logs 테이블은 2026-07-10에 만들어졌고(add_health_logs.sql),
-- 그 뒤 daily-condition.js가 '원탭 컨디션'으로 배변 색·소변 색·식욕·활력을
-- 기록하기 시작했다. 테이블에 대응 컬럼이 없어 이 네 값은 서버로 올라간 적이 없다.
--
-- 그래서 2026-07-28에 두 가지 문제가 드러났다:
--   1) 동기화가 로컬 항목을 원격 행으로 '교체'하면서 이 네 필드를 지웠다 —
--      wellness-anomaly의 혈변·혈뇨·식욕저하 감지가 입력을 통째로 잃었다.
--      (커밋 260a70f8에서 교체 → 병합으로 고쳐 유실은 멈췄다.)
--   2) 유실은 멈췄지만 기기 간 동기화는 여전히 안 된다 — 폰에서 기록한 배변 색을
--      PC에서 볼 수 없다. 이 마이그레이션이 그 나머지 절반이다.
--
-- 어휘는 앱과 1:1로 맞춘다(daily-condition.js GROUPS · wellness-anomaly.js):
--   poop_color : normal(갈색) | black(검은색) | red(붉은색) | white(회백색)
--   urine      : normal(연노랑) | dark(진한색) | red(붉은색)
--   appetite   : good | normal | low
--   activity   : high | normal | low
-- 값 검증은 걸지 않는다 — 앱이 어휘를 늘릴 때 CHECK 제약이 조용한 업로드 실패를
-- 만드는 쪽이 더 위험하다(기존 poop·condition 컬럼도 같은 이유로 제약 없음).
--
-- RLS·인덱스는 손대지 않는다. 기존 정책이 행 단위(user_id)라 컬럼 추가만으로
-- 접근 범위가 넓어지지 않는다.
-- ============================================================

ALTER TABLE public.health_logs ADD COLUMN IF NOT EXISTS poop_color TEXT;
ALTER TABLE public.health_logs ADD COLUMN IF NOT EXISTS urine      TEXT;
ALTER TABLE public.health_logs ADD COLUMN IF NOT EXISTS appetite   TEXT;
ALTER TABLE public.health_logs ADD COLUMN IF NOT EXISTS activity   TEXT;

-- 확인용(선택): 아래를 실행하면 추가된 컬럼이 보인다.
-- SELECT column_name, data_type FROM information_schema.columns
--  WHERE table_schema = 'public' AND table_name = 'health_logs' ORDER BY ordinal_position;
