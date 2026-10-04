import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setAccessToken, setUnauthorizedHandler } from '../api/client';
import { api } from '../api/endpoints';
import type { AuthResponse, User } from '../api/types';
import { readStorage, writeStorage } from '../lib/format';
import { AuthContext, type AuthState } from './context';

const STORAGE_KEY = 'shopsense.auth';

interface StoredSession {
  accessToken: string;
  expiresAt: string;
  user: User;
}

function loadSession(): StoredSession | null {
  const session = readStorage<StoredSession | null>(localStorage, STORAGE_KEY, null);
  if (!session || new Date(session.expiresAt).getTime() <= Date.now()) return null;
  return session;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<StoredSession | null>(() => {
    const s = loadSession();
    setAccessToken(s?.accessToken ?? null);
    return s;
  });

  const logout = useCallback(() => {
    setAccessToken(null);
    localStorage.removeItem(STORAGE_KEY);
    setSession(null);
    queryClient.removeQueries({ queryKey: ['orders'] });
    queryClient.removeQueries({ queryKey: ['admin'] });
  }, [queryClient]);

  useEffect(() => {
    setUnauthorizedHandler(logout);
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  // Log out automatically when the token expires.
  useEffect(() => {
    if (!session) return;
    const ms = new Date(session.expiresAt).getTime() - Date.now();
    const timer = window.setTimeout(logout, Math.max(ms, 0));
    return () => window.clearTimeout(timer);
  }, [session, logout]);

  const start = useCallback((auth: AuthResponse) => {
    const next: StoredSession = { accessToken: auth.accessToken, expiresAt: auth.expiresAt, user: auth.user };
    setAccessToken(next.accessToken);
    writeStorage(localStorage, STORAGE_KEY, next);
    setSession(next);
    return auth.user;
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user: session?.user ?? null,
      isAdmin: session?.user.role === 'admin',
      login: async (email, password) => start(await api.login(email, password)),
      register: async (email, password, displayName) => start(await api.register(email, password, displayName)),
      logout,
    }),
    [session, start, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
