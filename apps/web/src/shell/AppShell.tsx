import { Link, useLocation } from 'react-router-dom';

/**
 * The console shell: brand, two destinations, and the content area.
 *
 * Only the routes M0 serves appear here. `/queues/:id`, `/messages`, `/audit` and the rest of the
 * route map arrive with the milestones that serve their operations — a nav item leading to a 404
 * is the same failure as a button the API cannot honour.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const { pathname } = useLocation();

  const items = [
    { to: '/setup', label: 'Setup' },
    { to: '/queues', label: 'Queues' },
  ];

  return (
    <div className="shell">
      <header className="shell__header">
        <span className="shell__brand">Marites</span>
        <nav className="shell__nav" aria-label="Primary">
          {items.map((item) => (
            <Link
              key={item.to}
              to={item.to}
              aria-current={pathname.startsWith(item.to) ? 'page' : undefined}
            >
              {item.label}
            </Link>
          ))}
        </nav>
      </header>

      <main className="shell__main">{children}</main>
    </div>
  );
}
