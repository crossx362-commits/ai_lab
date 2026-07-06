import { requireSupabase } from '../lib/supabaseClient.js';

export function onAuthStateChange(callback) {
  const client = requireSupabase();
  const { data } = client.auth.onAuthStateChange((_event, session) => callback(session));
  return () => data.subscription.unsubscribe();
}

export async function getSession() {
  const client = requireSupabase();
  const { data, error } = await client.auth.getSession();
  if (error) throw error;
  return data.session;
}

export async function signUpWithEmail(email, password) {
  const client = requireSupabase();
  const { data, error } = await client.auth.signUp({ email, password });
  if (error) throw error;
  return data;
}

export async function signInWithEmail(email, password) {
  const client = requireSupabase();
  const { data, error } = await client.auth.signInWithPassword({ email, password });
  if (error) throw error;
  return data;
}

export async function signInWithProvider(provider) {
  const client = requireSupabase();
  const { data, error } = await client.auth.signInWithOAuth({ provider });
  if (error) throw error;
  return data;
}

export async function signOut() {
  const client = requireSupabase();
  const { error } = await client.auth.signOut();
  if (error) throw error;
}
