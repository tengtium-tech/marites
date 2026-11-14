/**
 * The sign-in exchange and the session, as the contract declares them (OP-AUTH-001…003).
 *
 * The CSRF token lives in module state because every write must echo it, and threading it
 * through every call site would be one omission away from a 403 in production. The provider
 * sets it on sign-in and on bootstrap; `csrfHeaders()` is the only thing a write needs.
 */

export type Role = 'Viewer' | 'Support' | 'Admin';

export interface SessionUser {
  id: string;
  email: string;
  role: Role;
  capabilities: string[];
}

export interface Session {
  user: SessionUser;
  csrfToken: string;
  expiresAt: string;
}

export class AuthError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    readonly subject?: string
  ) {
    super(message);
    this.name = 'AuthError';
  }
}

let csrfToken = '';

export function setCsrfToken(token: string) {
  csrfToken = token;
}

export function csrfHeaders(): Record<string, string> {
  return csrfToken ? { 'X-CSRF-Token': csrfToken } : {};
}

interface ProblemBody {
  code?: string;
  title?: string;
  detail?: string;
  subject?: string;
}

async function toError(response: Response): Promise<AuthError> {
  const problem = (await response.json().catch(() => ({}))) as ProblemBody;

  return new AuthError(
    response.status,
    problem.code ?? 'ERR-AUTH-001',
    problem.detail ?? problem.title ?? 'Sign-in failed.',
    problem.subject
  );
}

/** The current session, or null when there is none. 401 is an answer here, not an error. */
export async function fetchSession(): Promise<Session | null> {
  const response = await fetch('/api/v1/auth/session', { credentials: 'same-origin' });

  if (response.status === 401) {
    setCsrfToken('');
    return null;
  }

  if (!response.ok) {
    throw await toError(response);
  }

  const session = (await response.json()) as Session;
  setCsrfToken(session.csrfToken);
  return session;
}

export async function signInWithGoogle(idToken: string): Promise<Session> {
  const response = await fetch('/api/v1/auth/google', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ idToken }),
  });

  if (!response.ok) {
    throw await toError(response);
  }

  const session = (await response.json()) as Session;
  setCsrfToken(session.csrfToken);
  return session;
}

export async function signOut(): Promise<void> {
  await fetch('/api/v1/auth/logout', {
    method: 'POST',
    credentials: 'same-origin',
    headers: csrfHeaders(),
  });

  setCsrfToken('');
}
