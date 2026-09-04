create extension if not exists pgcrypto;

create table if not exists public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  display_name text not null default 'modu.user',
  avatar_url text,
  role text not null default 'user' check (role in ('user', 'admin')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.posts (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references public.profiles(id) on delete cascade,
  title text not null,
  content text not null,
  status text not null default 'published' check (status in ('draft', 'published', 'review', 'hidden')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.comments (
  id uuid primary key default gen_random_uuid(),
  post_id uuid not null references public.posts(id) on delete cascade,
  user_id uuid not null references public.profiles(id) on delete cascade,
  content text not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.likes (
  post_id uuid not null references public.posts(id) on delete cascade,
  user_id uuid not null references public.profiles(id) on delete cascade,
  created_at timestamptz not null default now(),
  primary key (post_id, user_id)
);

alter table public.profiles enable row level security;
alter table public.posts enable row level security;
alter table public.comments enable row level security;
alter table public.likes enable row level security;

create or replace function public.is_admin()
returns boolean
language sql
security definer
set search_path = public
as $$
  select exists (
    select 1 from public.profiles
    where id = auth.uid() and role = 'admin'
  );
$$;

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (id, display_name)
  values (new.id, coalesce(new.raw_user_meta_data->>'display_name', 'modu.user'));
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
after insert on auth.users
for each row execute procedure public.handle_new_user();

create policy "profiles can read their own profile"
on public.profiles for select
using (auth.uid() = id or public.is_admin());

create policy "profiles can update their own profile"
on public.profiles for update
using (auth.uid() = id)
with check (auth.uid() = id);

create policy "published posts are readable"
on public.posts for select
using (status = 'published' or auth.uid() = user_id or public.is_admin());

create policy "authenticated users can create posts"
on public.posts for insert
with check (auth.uid() = user_id);

create policy "authors can update posts"
on public.posts for update
using (auth.uid() = user_id or public.is_admin())
with check (auth.uid() = user_id or public.is_admin());

create policy "authors can delete posts"
on public.posts for delete
using (auth.uid() = user_id or public.is_admin());

create policy "comments are readable"
on public.comments for select
using (true);

create policy "authenticated users can create comments"
on public.comments for insert
with check (auth.uid() = user_id);

create policy "comment authors can update comments"
on public.comments for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

create policy "comment authors can delete comments"
on public.comments for delete
using (auth.uid() = user_id or public.is_admin());

create policy "likes are readable"
on public.likes for select
using (true);

create policy "authenticated users can like"
on public.likes for insert
with check (auth.uid() = user_id);

create policy "authenticated users can unlike"
on public.likes for delete
using (auth.uid() = user_id);

create or replace function public.admin_stats()
returns json
language sql
security definer
set search_path = public
as $$
  select case
    when public.is_admin() then json_build_object(
      'users', (select count(*) from public.profiles),
      'posts', (select count(*) from public.posts),
      'comments', (select count(*) from public.comments),
      'likes', (select count(*) from public.likes)
    )
    else null
  end;
$$;
