-- Download counter for homepage project files.
-- ASCII only on purpose: Korean comments and box-drawing characters get
-- mangled when pasted into the Supabase SQL editor, so keep this file plain.
-- Run once in Supabase console -> SQL Editor.

create table if not exists public.homepage_downloads (
  slug text primary key check (char_length(slug) between 1 and 60),
  count bigint not null default 0
);

alter table public.homepage_downloads enable row level security;

-- Read is public so each project page can show its own number.
drop policy if exists "dl anon read" on public.homepage_downloads;
create policy "dl anon read" on public.homepage_downloads
  for select using (true);

-- No insert/update policy on purpose: anon can only bump the number through
-- the function below, so nobody can set an arbitrary count or wipe a row.
create or replace function public.homepage_download_hit(p_slug text)
returns bigint
language plpgsql security definer set search_path = public as $$
declare
  v_count bigint;
begin
  if p_slug is null or char_length(p_slug) < 1 or char_length(p_slug) > 60 then
    raise exception 'bad slug';
  end if;

  insert into public.homepage_downloads (slug, count)
  values (p_slug, 1)
  on conflict (slug) do update set count = public.homepage_downloads.count + 1
  returning count into v_count;

  return v_count;
end;
$$;

grant execute on function public.homepage_download_hit(text) to anon;
