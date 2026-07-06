import { requireSupabase } from '../lib/supabaseClient.js';

export async function listPosts({ page = 1, pageSize = 10, query = '', sort = 'created_at' } = {}) {
  const client = requireSupabase();
  const from = (page - 1) * pageSize;
  const to = from + pageSize - 1;
  let request = client
    .from('posts')
    .select('*, profiles(display_name), comments(count), likes(count)', { count: 'exact' })
    .order(sort, { ascending: false })
    .range(from, to);

  if (query) {
    request = request.ilike('title', `%${query}%`);
  }

  const { data, count, error } = await request;
  if (error) throw error;
  return { posts: data, count };
}

export async function createPost(payload) {
  const client = requireSupabase();
  const { data, error } = await client.from('posts').insert(payload).select().single();
  if (error) throw error;
  return data;
}

export async function updatePost(postId, payload) {
  const client = requireSupabase();
  const { data, error } = await client
    .from('posts')
    .update(payload)
    .eq('id', postId)
    .select()
    .single();
  if (error) throw error;
  return data;
}

export async function deletePost(postId) {
  const client = requireSupabase();
  const { error } = await client.from('posts').delete().eq('id', postId);
  if (error) throw error;
}
