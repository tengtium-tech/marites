import { Link, useLocation } from 'react-router-dom';

/**
 * The console shell, adapted from the prototype's Sidebar (prototype/src/App.tsx at d124931).
 *
 * Only destinations the contract serves appear. The prototype also lists Publish, Audit, AI and
 * Storage; those operations are deferred, and a nav item leading to a 404 is the same failure as
 * a button the API cannot honour.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const { pathname } = useLocation();

  const items = [
    { to: '/setup', label: 'Setup', icon: '⚙' },
    { to: '/queues', label: 'Queues', icon: '▤' },
  ];

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="sidebar__brand">
          <span className="sidebar__mark" aria-hidden="true">M</span>
          <span>
            <span className="sidebar__title">Marites</span>
            <span className="sidebar__subtitle">queue observability</span>
          </span>
        </div>

        <nav className="sidebar__group" aria-label="Operations">
          <span className="sidebar__label">Operations</span>
          {items.map((item) => (
            <Link
              key={item.to}
              to={item.to}
              className="sidebar__item"
              aria-current={pathname.startsWith(item.to) ? 'page' : undefined}
            >
              <span aria-hidden="true" style={{ opacity: 0.8 }}>{item.icon}</span>
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="sidebar__foot">
          M0 build
          <div style={{ opacity: 0.75 }}>
            Setup and live queue metadata. Capture, replay and the console actions arrive in later
            milestones.
          </div>
        </div>
      </aside>

      <main className="page">{children}</main>
    </div>
  );
}
