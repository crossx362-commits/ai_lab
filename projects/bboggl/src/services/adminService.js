import { requireSupabase } from '../lib/supabaseClient.js';

export async function getAdminStats() {
  const client = requireSupabase();
  const { data, error } = await client.rpc('admin_stats');
  if (error) throw error;
  return data;
}

export async function updatePostStatus(postId, status) {
  const client = requireSupabase();
  const { data, error } = await client
    .from('posts')
    .update({ status })
    .eq('id', postId)
    .select()
    .single();
  if (error) throw error;
  return data;
}
