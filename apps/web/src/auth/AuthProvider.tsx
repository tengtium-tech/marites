import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AuthError, fetchSession, signInWithGoogle, signOut, type Session } from './session';

type AuthState =
  | { status: 'loading' }
  | { status: 'anonymous' }
  | { status: 'authenticated'; session: Session }
  | { status: 'refused'; error: AuthError };

interface AuthContextValue {
  state: AuthState;
  signIn: (idToken: string) => Promise<void>;
  signOut: () => Promise<void>;
  /** True when the signed-in role holds the capability. Hiding a control is usability only. */
  can: (capability: string) => boolean;
  retry: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;

    fetchSession()
      .then((session) => {
        if (cancelled) return;
        setState(session ? { status: 'authenticated', session } : { status: 'anonymous' });
      })
      .catch(() => {
        if (!cancelled) setState({ status: 'anonymous' });
      });

    return () => {
      cancelled = true;
    };
  }, [attempt]);

  const signIn = useCallback(async (idToken: string) => {
    try {
      const session = await signInWithGoogle(idToken);
      setState({ status: 'authenticated', session });
    } catch (error) {
      // An unmapped account is a distinct state, not a failed login: the identity is real and
      // the screen must say so, with the subject an admin needs.
      setState({ status: 'refused', error: error as AuthError });
    }
  }, []);

  const signOut = useCallback(async () => {
    await signOut();
    setState({ status: 'anonymous' });
  }, []);

  const can = useCallback(
    (capability: string) =>
      state.status === 'authenticated' && state.session.user.capabilities.includes(capability),
    [state]
  );

  const retry = useCallback(() => {
    setState({ status: 'loading' });
    setAttempt((current) => current + 1);
  }, []);

  const value = useMemo(
    () => ({ state, signIn, signOut, can, retry }),
    [state, signIn, signOut, can, retry]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider');
  }

  return context;
}
