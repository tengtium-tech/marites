import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SetupProgress } from './SetupProgress';

type Check = {
  id: string;
  label: string;
  state: 'pass' | 'fail' | 'warn' | 'skipped';
  detail?: string;
  remedy?: string;
};

type Preflight = { state: 'passing' | 'failing' | 'degraded'; checks: Check[]; checkedAt?: string };
type Problem = { code?: string; title?: string; detail?: string; remedy?: string };
type Capabilities = { features: string[] };

type SaveState =
  | { kind: 'editing' }
  | { kind: 'saving' }
  | { kind: 'saved'; brokerId: string; revision: number; preflightState: string; preflight: Preflight }
  | { kind: 'rejected'; status: number; problem: Problem };

const newIdempotencyKey = () => crypto.randomUUID();

/**
 * The M0 setup wizard (wireframes §3.5 step 1 and 3, DEC-021).
 *
 * Saving posts to `POST /brokers`, which checks the broker before it writes and returns the
 * initial results. There is no second preflight call: the checks shown are the ones that decided
 * whether the profile exists.
 */
export function SetupWizard() {
  const navigate = useNavigate();
  const [capabilities, setCapabilities] = useState<Capabilities | null>(null);
  const [capabilitiesFailed, setCapabilitiesFailed] = useState(false);
  const [form, setForm] = useState({
    name: '',
    providerKind: 'rabbitmq',
    environment: 'dev',
    credentialKind: 'basic',
    endpoint: '',
    adminUri: '',
    namespaceOrVhost: '/',
    username: '',
    password: '',
  });
  const [state, setState] = useState<SaveState>({ kind: 'editing' });
  const [rechecking, setRechecking] = useState(false);
  const alertRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let cancelled = false;

    fetch('/api/v1/capabilities')
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
      .then((body: Capabilities) => { if (!cancelled) setCapabilities(body); })
      .catch(() => { if (!cancelled) setCapabilitiesFailed(true); });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (state.kind === 'rejected') alertRef.current?.focus();
  }, [state]);

  const update =
    (field: keyof typeof form) =>
    (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
      setForm((current) => ({ ...current, [field]: event.target.value }));

  async function save(event: React.FormEvent) {
    event.preventDefault();
    setState({ kind: 'saving' });

    const response = await fetch('/api/v1/brokers', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': newIdempotencyKey() },
      body: JSON.stringify({
        name: form.name,
        providerKind: form.providerKind,
        environment: form.environment,
        credentialKind: form.credentialKind,
        endpoint: form.endpoint,
        adminUri: form.adminUri || undefined,
        namespaceOrVhost: form.namespaceOrVhost || undefined,
        credentials: { amqp: `${form.username}:${form.password}`, admin: `${form.username}:${form.password}` },
      }),
    });

    if (!response.ok) {
      // DEC-021: nothing was persisted, so the form keeps its values and the operator corrects
      // the field the remedy names.
      const problem = await response.json().catch(() => ({}) as Problem);
      setState({ kind: 'rejected', status: response.status, problem });
      return;
    }

    const created = await response.json();
    setState({
      kind: 'saved',
      brokerId: created.id,
      revision: created.revision,
      preflightState: created.preflightState,
      preflight: created.preflight,
    });
  }

  /** Explicit recheck against the saved profile. The wizard never runs this on its own. */
  async function recheck() {
    if (state.kind !== 'saved') return;

    setRechecking(true);
    try {
      const response = await fetch(`/api/v1/brokers/${state.brokerId}/preflight`, {
        method: 'POST',
        headers: { 'Idempotency-Key': newIdempotencyKey() },
      });

      if (response.ok) {
        const result: Preflight = await response.json();
        setState({ ...state, preflight: result, preflightState: result.state });
      }
    } finally {
      setRechecking(false);
    }
  }

  if (capabilities === null && !capabilitiesFailed) {
    return <p className="muted" aria-busy="true">Loading…</p>;
  }

  if (capabilitiesFailed || !capabilities?.features.includes('brokers')) {
    return (
      <>
        <h1>Connect a broker</h1>
        <p role="alert" className="alert" style={{ marginTop: 16 }}>
          {capabilitiesFailed
            ? 'The backend could not be reached, so its capabilities are unknown. Nothing is offered until they are.'
            : 'This backend does not report broker management as available, so the form is not shown.'}
        </p>
      </>
    );
  }

  if (state.kind === 'saved') {
    const passing = state.preflight.state === 'passing';

    return (
      <>
        <h1>Connect a broker</h1>
        <SetupProgress current={3} done />
        <div className="broker-head">
          <div>
            <div className="broker-head__name">{form.name}</div>
            <div className="muted small">
              {form.providerKind} · {form.environment} · revision {state.revision}
            </div>
          </div>
          <span
            className="checks__state"
            data-state={passing ? 'pass' : 'fail'}
            style={{ fontSize: 12 }}
          >
            {state.preflight.state}
          </span>
        </div>

        <div className="setup" style={{ marginTop: 20 }}>
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between' }}>
              <h2>Preflight</h2>
              <span
                className="checks__state"
                data-state={passing ? 'pass' : 'fail'}
                style={{ fontSize: 12 }}
              >
                {state.preflight.state}
              </span>
            </div>

            <div aria-live="polite">
              <ul className="checks" style={{ marginTop: 8 }}>
                {state.preflight.checks.map((check) => (
                  <li key={check.id}>
                    <div className="checks__head">
                      <span className="checks__state" data-state={check.state}>{check.state}</span>
                      <strong>{check.label}</strong>
                    </div>
                    {check.detail && <div className="muted small">{check.detail}</div>}
                    {check.remedy && <div className="checks__remedy small">Remedy: {check.remedy}</div>}
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <div className="card">
            <h3>What happens next</h3>
            <p className="muted small" style={{ marginTop: 6 }}>
              {passing
                ? 'Live metadata is available. Capture can be configured once capture policies arrive (M2).'
                : 'Live metadata is available. Capture stays disabled until the failing checks pass — apply the grant above and re-run preflight.'}
            </p>

            <div style={{ display: 'grid', gap: 8, marginTop: 14 }}>
              <button
                type="button"
                data-variant="primary"
                onClick={() => navigate(`/queues?brokerId=${state.brokerId}`)}
              >
                Continue to queues
              </button>

              <button type="button" onClick={recheck} disabled={rechecking}>
                {rechecking ? 'Rechecking…' : 'Re-run preflight'}
              </button>
            </div>
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <h1>Connect a broker</h1>
      <p className="muted" style={{ marginTop: 6 }}>
        Marites observes a broker you already run. It never provisions or repairs one.
      </p>
      <SetupProgress current={1} done={false} />

      {state.kind === 'rejected' && (
        <div
          ref={alertRef}
          role="alert"
          tabIndex={-1}
          className="alert"
          style={{ marginTop: 16 }}
        >
          <strong>
            {state.problem.title ?? 'The profile was not saved'}{' '}
            <span className="alert__code">
              {state.status}{state.problem.code ? ` ${state.problem.code}` : ''}
            </span>
          </strong>
          {state.problem.detail && <p style={{ margin: '8px 0 0' }}>{state.problem.detail}</p>}
          {state.problem.remedy && (
            <p className="checks__remedy" style={{ margin: '8px 0 0' }}>
              Remedy: {state.problem.remedy}
            </p>
          )}
          <p className="muted small" style={{ margin: '8px 0 0' }}>
            Nothing was saved. Correct the fields below and submit again.
          </p>
        </div>
      )}

      <form onSubmit={save} aria-busy={state.kind === 'saving'} className="setup" style={{ marginTop: 20 }}>
        <div className="card">
          <span className="section-label">Broker</span>

          <div className="form-grid" style={{ marginTop: 12 }}>
            <Field label="Name" value={form.name} onChange={update('name')} required />
            <Field
              label="AMQP endpoint"
              hint="For example amqp://host:5672/ — the address a consumer would use."
              value={form.endpoint}
              onChange={update('endpoint')}
              placeholder="amqp://host:5672/"
              required
            />
            <Field
              label="Management API"
              hint="Where the broker's management plugin answers, for example http://host:15672."
              value={form.adminUri}
              onChange={update('adminUri')}
              placeholder="http://host:15672"
            />
            <Field label="Vhost" value={form.namespaceOrVhost} onChange={update('namespaceOrVhost')} />
            <Field label="Username" value={form.username} onChange={update('username')} required />
            <Field label="Password" type="password" value={form.password} onChange={update('password')} required />
          </div>

          <button
            type="submit"
            data-variant="primary"
            disabled={state.kind === 'saving'}
            style={{ marginTop: 16 }}
          >
            {state.kind === 'saving' ? 'Checking the broker…' : 'Save profile'}
          </button>
        </div>

        <aside className="card">
          <span className="section-label">Why this comes first</span>
          <p className="muted small" style={{ marginTop: 6 }}>
            Saving checks the broker before anything is written. If it cannot be reached, or
            rejects the credentials, no profile is created and nothing is stored.
          </p>

          <h3 style={{ marginTop: 16 }}>What enabling capture will create</h3>
          <p className="muted small" style={{ marginTop: 6 }}>
            Capture arrives in M2. When it does, enabling it on a queue declares a Marites-owned
            shadow queue bound to the same exchange, with a message limit, a TTL and its own
            dead-letter target — so a stalled observer can never fill the broker's disk.
          </p>
          <p className="muted small" style={{ marginTop: 8 }}>
            It requires <code>configure</code>, <code>write</code> and <code>read</code> on the
            {' '}<code>marites.</code> prefix.
          </p>
        </aside>
      </form>
    </>
  );
}

function Field(props: {
  label: string;
  value: string;
  onChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
  type?: string;
  placeholder?: string;
  required?: boolean;
  hint?: string;
}) {
  const hintId = props.hint ? `${props.label.replace(/\s+/g, '-').toLowerCase()}-hint` : undefined;

  return (
    <label className="field">
      <span>{props.label}</span>
      <input
        type={props.type ?? 'text'}
        value={props.value}
        onChange={props.onChange}
        placeholder={props.placeholder}
        required={props.required}
        aria-describedby={hintId}
      />
      {props.hint && <span id={hintId} className="small muted">{props.hint}</span>}
    </label>
  );
}
