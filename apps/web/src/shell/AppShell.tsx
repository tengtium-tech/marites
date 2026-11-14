import { Link, useLocation } from 'react-router-dom';
import { LogOut } from 'lucide-react';
import { useAuth } from '../auth/AuthProvider';
import { BarChart3, Settings2 } from 'lucide-react';
import { cn } from '../lib/utils';

/**
 * The console shell, taken from the prototype's Sidebar (prototype/src/App.tsx at d124931).
 *
 * Only destinations the contract serves appear. The prototype also lists Publish, Audit, AI and
 * Storage; those operations are deferred, and a nav item leading to a 404 is the same failure as
 * a button the API cannot honour.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const { pathname } = useLocation();
  const { state, signOut, can } = useAuth();
  const user = state.status === 'authenticated' ? state.session.user : null;

  const item = (to: string, icon: React.ReactNode, label: string) => {
    const active = pathname.startsWith(to);

    return (
      <Link
        to={to}
        aria-current={active ? 'page' : undefined}
        className={cn(
          'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-[13.5px] text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground',
          active && 'bg-accent font-semibold text-accent-foreground hover:bg-accent'
        )}
      >
        <span className="opacity-80">{icon}</span>
        {label}
      </Link>
    );
  };

  return (
    <div className="grid min-h-screen grid-cols-1 md:grid-cols-[232px_minmax(0,1fr)]">
      <aside className="sticky top-0 flex h-screen flex-col gap-5 border-r bg-card px-3 py-4">
        <div className="flex items-center gap-2.5 px-2 pb-1">
          <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-foreground text-[12px] font-bold text-card">
            M
          </span>
          <div className="leading-tight">
            <div className="text-[14.5px] font-semibold tracking-[-0.02em]">Marites</div>
            <div className="text-[10.5px] text-muted-foreground">queue observability</div>
          </div>
        </div>

        <nav className="flex flex-col gap-0.5" aria-label="Operations">
          <div className="px-2.5 pb-1.5 text-[10.5px] uppercase tracking-[0.09em] text-muted-foreground">
            Operations
          </div>
          {item('/queues', <BarChart3 className="h-4 w-4" />, 'Queues')}
          {/* Setup manages brokers, which is an Admin capability. Hiding it is usability; the
              API refuses the call for every other role regardless. */}
          {can('manage_brokers') && item('/setup', <Settings2 className="h-4 w-4" />, 'Setup')}
        </nav>

        <div className="mt-auto border-t px-2.5 pt-3 text-[11px] leading-relaxed text-muted-foreground">
          {user && (
            <div className="mb-3">
              <div className="truncate text-[11.5px] font-medium text-foreground" title={user.email}>
                {user.email}
              </div>
              <div className="flex items-center justify-between gap-2">
                <span>{user.role}</span>
                <button
                  type="button"
                  onClick={() => void signOut()}
                  className="inline-flex items-center gap-1 rounded px-1 py-0.5 text-[11px] hover:bg-secondary hover:text-foreground"
                >
                  <LogOut className="h-3 w-3" />
                  Sign out
                </button>
              </div>
            </div>
          )}
          M0 build
          <div className="opacity-75">
            Setup and live queue metadata. Capture, replay and the console actions arrive in later
            milestones.
          </div>
        </div>
      </aside>

      <main className="min-w-0 px-8 py-6">{children}</main>
    </div>
  );
}
