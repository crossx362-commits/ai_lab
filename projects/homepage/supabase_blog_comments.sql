-- Blog comments table for homepage.
-- ASCII only on purpose: Korean comments and box-drawing characters get
-- mangled when pasted into the Supabase SQL editor, so keep this file plain.
-- Run once in Supabase console -> SQL Editor.

create table if not exists public.homepage_blog_comments (
  id bigint generated always as identity primary key,
  created_at timestamptz not null default now(),
  post_id text not null check (char_length(post_id) between 1 and 60),
  nickname text not null check (char_length(nickname) between 1 and 24),
  message text not null check (char_length(message) between 1 and 500)
);

create index if not exists homepage_blog_comments_post_id_idx
  on public.homepage_blog_comments (post_id);

alter table public.homepage_blog_comments enable row level security;

drop policy if exists "bc anon read" on public.homepage_blog_comments;
create policy "bc anon read" on public.homepage_blog_comments
  for select using (true);

drop policy if exists "bc anon insert" on public.homepage_blog_comments;
create policy "bc anon insert" on public.homepage_blog_comments
  for insert with check (true);
