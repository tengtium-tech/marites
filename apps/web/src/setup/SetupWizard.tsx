import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SetupProgress } from './SetupProgress';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { cn } from '../lib/utils';

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
      <h1 className="text-[22px] font-semibold tracking-[-0.02em]">Set up Marites</h1>
      <p className="mt-1.5 text-[13.5px] text-muted-foreground">
        Marites observes a broker you already run. It never provisions or repairs one.
      </p>
      <SetupProgress current={1} done={false} />

      {state.kind === 'rejected' && (
        <div
          ref={alertRef}
          role="alert"
          tabIndex={-1}
          className="mt-4 rounded-lg border border-destructive/60 border-l-[3px] bg-destructive/5 px-4 py-3"
        >
          <div className="font-semibold text-destructive">
            {state.problem.title ?? 'The profile was not saved'}{' '}
            <span className="font-mono text-[12px]">
              {state.status}{state.problem.code ? ` ${state.problem.code}` : ''}
            </span>
          </div>
          {state.problem.detail && <p className="mt-2 text-[13px]">{state.problem.detail}</p>}
          {state.problem.remedy && (
            <p className="mt-2 text-[13px] text-warning">Remedy: {state.problem.remedy}</p>
          )}
          <p className="mt-2 text-[12.5px] text-muted-foreground">
            Nothing was saved. Correct the fields below and submit again.
          </p>
        </div>
      )}

      <form
        onSubmit={save}
        aria-busy={state.kind === 'saving'}
        className="mt-5 grid items-start gap-6 md:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]"
      >
        <Card className="p-5">
          <div className="text-[10.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground">
            Broker
          </div>

          <div className="mt-4 grid gap-3">
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

          <Button type="submit" disabled={state.kind === 'saving'} className="mt-5">
            {state.kind === 'saving' ? 'Checking the broker…' : 'Save profile'}
          </Button>
        </Card>

        <Card className="bg-muted/40 p-5">
          <div className="text-[10.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground">
            Why this comes first
          </div>
          <p className="mt-3 text-[13px] leading-relaxed text-muted-foreground">
            Saving checks the broker before anything is written. If it cannot be reached, or
            rejects the credentials, no profile is created and nothing is stored.
          </p>

          <div className="mt-4 text-[10.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground">
            What enabling capture will create
          </div>
          <p className="mt-2 text-[13px] leading-relaxed text-muted-foreground">
            Capture arrives in M2. When it does, enabling it on a queue declares a Marites-owned
            shadow queue bound to the same exchange, with a message limit, a TTL and its own
            dead-letter target — so a stalled observer can never fill the broker's disk.
          </p>
          <p className="mt-2 text-[13px] leading-relaxed text-muted-foreground">
            It requires <code className="rounded bg-card px-1 py-0.5 text-[12px]">configure</code>,{' '}
            <code className="rounded bg-card px-1 py-0.5 text-[12px]">write</code> and{' '}
            <code className="rounded bg-card px-1 py-0.5 text-[12px]">read</code> on the{' '}
            <code className="rounded bg-card px-1 py-0.5 text-[12px]">marites.</code> prefix.
          </p>
        </Card>
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
  const id = props.label.replace(/\s+/g, '-').toLowerCase();
  const hintId = props.hint ? `${id}-hint` : undefined;

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id} className="text-[12.5px] text-muted-foreground">
        {props.label}
      </Label>
      <Input
        id={id}
        type={props.type ?? 'text'}
        value={props.value}
        onChange={props.onChange}
        placeholder={props.placeholder}
        required={props.required}
        aria-describedby={hintId}
      />
      {props.hint && (
        <span id={hintId} className="text-[11.5px] text-muted-foreground">
          {props.hint}
        </span>
      )}
    </div>
  );
}
