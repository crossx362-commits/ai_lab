import { createClient } from '@supabase/supabase-js';

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY;

export const isSupabaseConfigured = Boolean(supabaseUrl && supabaseAnonKey);

export const supabase = isSupabaseConfigured
  ? createClient(supabaseUrl, supabaseAnonKey)
  : null;

export function requireSupabase() {
  if (!supabase) {
    throw new Error('Supabase is not configured. Add VITE_SUPABASE_URL and VITE_SUPABASE_ANON_KEY.');
  }
  return supabase;
}

export async function checkSupabaseHealth() {
  if (!isSupabaseConfigured) {
    return { ok: false, message: 'Supabase 환경변수가 없습니다.' };
  }

  try {
    const response = await fetch(`${supabaseUrl}/auth/v1/settings`, {
      headers: {
        apikey: supabaseAnonKey,
      },
    });
    if (!response.ok) {
      return { ok: false, message: `Supabase 응답 오류 ${response.status}` };
    }
    return { ok: true, message: 'Supabase 연결됨' };
  } catch {
    return { ok: false, message: 'Supabase URL에 연결할 수 없습니다.' };
  }
}
