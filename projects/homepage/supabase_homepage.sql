-- ═══════════════════════════════════════════════════════════
-- 백수장인 홈페이지 — 방명록 + 방문자 수
-- Supabase 콘솔 → SQL Editor에서 이 파일 전체를 1회 실행하면 끝.
-- (펫과나와 같은 프로젝트를 쓰지만 homepage_ 접두사로 완전 분리됨)
-- ═══════════════════════════════════════════════════════════

-- 방명록
create table if not exists public.homepage_guestbook (
  id bigint generated always as identity primary key,
  created_at timestamptz not null default now(),
  nickname text not null check (char_length(nickname) between 1 and 24),
  message text not null check (char_length(message) between 1 and 500)
);
alter table public.homepage_guestbook enable row level security;
drop policy if exists "gb anon read" on public.homepage_guestbook;
create policy "gb anon read" on public.homepage_guestbook for select using (true);
drop policy if exists "gb anon insert" on public.homepage_guestbook;
create policy "gb anon insert" on public.homepage_guestbook for insert with check (true);

-- 방문자 수 (단일 행 카운터)
create table if not exists public.homepage_page_views (
  id int primary key,
  views bigint not null default 0
);
insert into public.homepage_page_views (id, views) values (1, 0) on conflict do nothing;
alter table public.homepage_page_views enable row level security;
drop policy if exists "pv anon read" on public.homepage_page_views;
create policy "pv anon read" on public.homepage_page_views for select using (true);

-- 방문 1 증가 RPC (anon이 직접 update 못 하게 security definer로만 허용)
create or replace function public.homepage_hit() returns bigint
language sql security definer set search_path = public as $$
  update public.homepage_page_views set views = views + 1 where id = 1 returning views;
$$;
grant execute on function public.homepage_hit() to anon;
