import { useEffect, useRef, useState } from 'react';
import { useAuth } from './AuthProvider';

/**
 * Sign-in, Google mode (ADR-008). Marites renders no password form: the popup is Google's,
 * and the only thing this screen owns is the button and the refusal states.
 */

interface GoogleCredentialResponse {
  credential: string;
}

interface GoogleIdentity {
  initialize(options: {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
    auto_select?: boolean;
    cancel_on_tap_outside?: boolean;
  }): void;
  renderButton(
    parent: HTMLElement,
    options: { theme?: 'outline' | 'filled_blue' | 'filled_black'; size?: 'large' | 'medium' | 'small'; width?: number; text?: string }
  ): void;
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleIdentity } };
  }
}

const CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;

export function SignInScreen() {
  const { state, signIn, retry } = useAuth();
  const buttonRef = useRef<HTMLDivElement>(null);
  const [scriptReady, setScriptReady] = useState(false);
  const [scriptFailed, setScriptFailed] = useState(false);

  useEffect(() => {
    if (!CLIENT_ID) {
      return;
    }

    if (window.google?.accounts?.id) {
      setScriptReady(true);
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = () => setScriptReady(true);
    script.onerror = () => setScriptFailed(true);
    document.head.appendChild(script);
  }, []);

  useEffect(() => {
    if (!scriptReady || !CLIENT_ID || !buttonRef.current || !window.google) {
      return;
    }

    window.google.accounts.id.initialize({
      client_id: CLIENT_ID,
      callback: (response) => void signIn(response.credential),
    });

    buttonRef.current.innerHTML = '';
    window.google.accounts.id.renderButton(buttonRef.current, {
      theme: 'outline',
      size: 'large',
      text: 'signin_with',
      width: 280,
    });
  }, [scriptReady, signIn]);

  const refusal = state.status === 'refused' ? state.error : null;

  return (
    <div className="grid min-h-screen place-items-center bg-background px-6">
      <div className="w-full max-w-sm">
        <div className="mb-7 flex items-center gap-2.5">
          <span className="grid h-9 w-9 place-items-center rounded-full bg-foreground text-[13px] font-bold text-card">
            M
          </span>
          <div className="leading-tight">
            <div className="text-[15px] font-semibold tracking-[-0.02em]">Marites</div>
            <div className="text-[11px] text-muted-foreground">queue observability</div>
          </div>
        </div>

        <h1 className="text-[22px] font-semibold tracking-[-0.02em]">Sign in</h1>
        <p className="mt-1.5 text-[13px] text-muted-foreground">
          Use your Google account. Access is granted per person by a Marites administrator.
        </p>

        {!CLIENT_ID && (
          <div className="mt-5 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-[12.5px]">
            <p className="font-medium text-destructive">Google sign-in is not configured</p>
            <p className="mt-1 text-muted-foreground">
              Set <code className="font-mono text-[11.5px]">VITE_GOOGLE_CLIENT_ID</code> in
              {' '}<code className="font-mono text-[11.5px]">apps/web/.env.local</code> and restart the
              dev server. The client ID is public; the client secret is not used.
            </p>
          </div>
        )}

        {CLIENT_ID && scriptFailed && (
          <div className="mt-5 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-[12.5px]">
            <p className="font-medium text-destructive">Google's sign-in script did not load</p>
            <p className="mt-1 text-muted-foreground">
              Check the network path to accounts.google.com, then reload.
            </p>
          </div>
        )}

        {CLIENT_ID && !scriptFailed && <div ref={buttonRef} className="mt-6" />}

        {refusal && refusal.status === 403 && (
          <div className="mt-5 rounded-md border border-amber-500/40 bg-amber-500/5 p-3 text-[12.5px]">
            <p className="font-medium">This account is not mapped to a Marites role</p>
            <p className="mt-1 text-muted-foreground">
              Sign-in worked, but this identity has no role. Ask an administrator to grant it.
            </p>
            {refusal.subject && (
              <p className="mt-2 font-mono text-[11px] text-muted-foreground break-all">
                subject: {refusal.subject}
              </p>
            )}
          </div>
        )}

        {refusal && refusal.status !== 403 && (
          <div className="mt-5 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-[12.5px]">
            <p className="font-medium text-destructive">Sign-in was refused</p>
            <p className="mt-1 text-muted-foreground">{refusal.message}</p>
            <button
              type="button"
              onClick={retry}
              className="mt-2 text-[12px] font-medium underline underline-offset-2"
            >
              Try again
            </button>
          </div>
        )}

        <p className="mt-8 text-[11px] leading-relaxed text-muted-foreground">
          Marites stores no Google token. Sign-in exchanges it once for a session cookie that
          expires after the configured lifetime, and signing out revokes every session you hold.
        </p>
      </div>
    </div>
  );
}
