import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';

type Queue = {
  id: string;
  brokerId: string;
  name: string;
  vhost?: string;
  depth?: number;
  consumers?: number;
  unacked?: number;
  captureState: string;
  deadLetterConfigured: boolean;
};

type Broker = { id: string; name: string; preflightState: string };

/**
 * The queue list. One row per queue, live from the broker.
 *
 * Capture is not running in M0, so every row reads `disabled` — the honest state, not a zero that
 * looks like healthy traffic (VOCABULARY §15). The banners the wireframes describe need captured
 * state to describe, and arrive with it.
 */
export function QueueList() {
  const [params, setParams] = useSearchParams();
  const brokerId = params.get('brokerId') ?? '';
  const [queues, setQueues] = useState<Queue[] | null>(null);
  const [brokers, setBrokers] = useState<Broker[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/v1/brokers')
      .then((r) => (r.ok ? r.json() : []))
      .then(setBrokers)
      .catch(() => setBrokers([]));
  }, []);

  useEffect(() => {
    const query = brokerId ? `?brokerId=${encodeURIComponent(brokerId)}` : '';

    fetch(`/api/v1/queues${query}`)
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
      .then(setQueues)
      .catch((e) => setError(`The queue list could not be read (${e.message}).`));
  }, [brokerId]);

  return (
    <section>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 16 }}>
        <h1>Queues</h1>
        {brokers.length > 1 && (
          <label className="field" style={{ minWidth: 220 }}>
            <span>Broker</span>
            <select
              value={brokerId}
              onChange={(e) => setParams(e.target.value ? { brokerId: e.target.value } : {})}
            >
              <option value="">All brokers</option>
              {brokers.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </label>
        )}
      </div>

      <p className="muted small" style={{ marginTop: 6 }}>
        Live metadata read from the broker. Nothing here is captured yet.
      </p>

      {error && (
        <p role="alert" className="alert" style={{ marginTop: 16 }}>{error}</p>
      )}

      {queues === null && !error && <p className="muted" style={{ marginTop: 16 }} aria-busy="true">Loading…</p>}

      {queues !== null && queues.length === 0 && (
        <p className="muted" style={{ marginTop: 16 }}>This broker reports no queues.</p>
      )}

      {queues !== null && queues.length > 0 && (
        <div className="card" style={{ padding: 0, marginTop: 16, overflow: 'hidden' }}>
          <table>
            <thead>
              <tr>
                <th>Queue</th>
                <th className="num">Depth</th>
                <th className="num">Consumers</th>
                <th>Dead letters</th>
                <th>Capture</th>
              </tr>
            </thead>
            <tbody>
              {queues.map((q) => (
                <tr key={q.id}>
                  <td>
                    <Link to={{ pathname: `/queues/${q.id}`, search: params.toString() }}>{q.name}</Link>
                    {q.vhost && <div className="muted small">vhost {q.vhost}</div>}
                  </td>
                  <td className="num">{q.depth ?? '—'}</td>
                  <td className="num">{q.consumers ?? '—'}</td>
                  <td>
                    {q.deadLetterConfigured
                      ? <span style={{ color: 'hsl(var(--success))' }}>configured</span>
                      : <span style={{ color: 'hsl(var(--idle))' }}>unavailable</span>}
                  </td>
                  <td><span style={{ color: 'hsl(var(--idle))' }}>{q.captureState}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p className="muted small" style={{ marginTop: 12 }}>
        {queues?.length ? `${queues.length} queues` : ''}
      </p>
    </section>
  );
}
