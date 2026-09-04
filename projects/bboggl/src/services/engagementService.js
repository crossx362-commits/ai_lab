import { requireSupabase } from '../lib/supabaseClient.js';

export async function listComments(postId) {
  const client = requireSupabase();
  const { data, error } = await client
    .from('comments')
    .select('*, profiles(display_name)')
    .eq('post_id', postId)
    .order('created_at', { ascending: true });
  if (error) throw error;
  return data;
}

export async function createComment(payload) {
  const client = requireSupabase();
  const { data, error } = await client.from('comments').insert(payload).select().single();
  if (error) throw error;
  return data;
}

export async function deleteComment(commentId) {
  const client = requireSupabase();
  const { error } = await client.from('comments').delete().eq('id', commentId);
  if (error) throw error;
}

export async function toggleLike(postId, userId, isLiked) {
  const client = requireSupabase();
  if (isLiked) {
    const { error } = await client
      .from('likes')
      .delete()
      .eq('post_id', postId)
      .eq('user_id', userId);
    if (error) throw error;
    return false;
  }

  const { error } = await client.from('likes').insert({ post_id: postId, user_id: userId });
  if (error) throw error;
  return true;
}
