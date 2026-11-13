import { useEffect, useRef, useState } from 'react';

type Check = {
  id: string;
  label: string;
  state: 'pass' | 'fail' | 'warn' | 'skipped';
  detail?: string;
  remedy?: string;
};

type Preflight = { state: 'passing' | 'failing' | 'degraded'; checks: Check[]; checkedAt?: string };

type ProviderCapabilities = {
  kind: string;
  displayName: string;
  supportTier: string;
  canTap: boolean;
  canPeek: boolean;
  canPublish: boolean;
  canDiscard: boolean;
  canPurge: boolean;
  canReplay: boolean;
  canDeclareShadow: boolean;
};

type Capabilities = { features: string[] };

/** The Problem body the API returns for a refused creation. */
type Problem = { code?: string; title?: string; detail?: string; remedy?: string; status?: number };

type SaveState =
  | { kind: 'editing' }
  | { kind: 'saving' }
  | { kind: 'saved'; brokerId: string; revision: number; preflightState: string; preflight: Preflight }
  | { kind: 'rejected'; status: number; problem: Problem };

const newIdempotencyKey = () => crypto.randomUUID();

export function SetupWizard() {
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
  const [provider, setProvider] = useState<ProviderCapabilities | null>(null);
  const [rechecking, setRechecking] = useState(false);
  const alertRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let cancelled = false;

    fetch('/api/v1/capabilities')
      .then((response) => (response.ok ? response.json() : Promise.reject(new Error(String(response.status)))))
      .then((body: Capabilities) => {
        if (!cancelled) setCapabilities(body);
      })
      .catch(() => {
        if (!cancelled) setCapabilitiesFailed(true);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // A refused creation leaves the form where it is, so the operator can correct it. Focus moves
  // to the message, because an alert announced but off-screen still leaves them hunting.
  useEffect(() => {
    if (state.kind === 'rejected') {
      alertRef.current?.focus();
    }
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
      // DEC-021: a refused creation persists nothing, so there is no half-made profile to clean
      // up. The form keeps its values and the remedy says what to change.
      const problem = await response.json().catch(() => ({}) as Problem);
      setState({ kind: 'rejected', status: response.status, problem });
      return;
    }

    const created = await response.json();

    // The initial checks arrive with the created profile; there is no second call.
    setState({
      kind: 'saved',
      brokerId: created.id,
      revision: created.revision,
      preflightState: created.preflightState,
      preflight: created.preflight,
    });

    const capabilityResponse = await fetch(`/api/v1/brokers/${created.id}/capabilities`);
    if (capabilityResponse.ok) {
      setProvider(await capabilityResponse.json());
    }
  }

  /** Explicit recheck against the saved profile — the wizard never re-runs this on its own. */
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
    return <main aria-busy="true" />;
  }

  if (capabilitiesFailed) {
    return (
      <Shell>
        <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
        <p role="alert" style={{ color: '#b00020' }}>
          The backend could not be reached, so its capabilities are unknown. Nothing is offered
          until they are: a control that cannot work is worse than no control.
        </p>
      </Shell>
    );
  }

  if (!capabilities!.features.includes('brokers')) {
    return (
      <Shell>
        <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
        <p role="alert" style={{ color: '#8a4b00' }}>
          This backend does not report broker management as available, so the form is not shown.
        </p>
      </Shell>
    );
  }

  return (
    <Shell>
      <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
      <p style={{ color: '#444' }}>
        Marites observes a broker you already run. It never provisions or repairs one.
      </p>

      {state.kind === 'rejected' && (
        <div
          ref={alertRef}
          role="alert"
          tabIndex={-1}
          style={{ border: '1px solid #b00020', padding: 12, marginTop: 16, background: '#fff5f5' }}
        >
          <strong style={{ color: '#b00020' }}>
            {state.problem.title ?? 'The profile was not saved'} ({state.status}
            {state.problem.code ? ` ${state.problem.code}` : ''})
          </strong>
          {state.problem.detail && <p style={{ margin: '8px 0 0' }}>{state.problem.detail}</p>}
          {state.problem.remedy && (
            <p style={{ margin: '8px 0 0', color: '#8a4b00' }}>Remedy: {state.problem.remedy}</p>
          )}
          <p style={{ margin: '8px 0 0', fontSize: 13, color: '#555' }}>
            Nothing was saved. Correct the fields below and submit again.
          </p>
        </div>
      )}

      {state.kind !== 'saved' && (
        <form onSubmit={save} aria-busy={state.kind === 'saving'} style={{ display: 'grid', gap: 12, marginTop: 24 }}>
          <Field label="Name" value={form.name} onChange={update('name')} required />
          <Field
            label="AMQP endpoint"
            hint="For example amqp://host:5672/ — the address a consumer would use."
            value={form.endpoint}
            onChange={update('endpoint')}
            placeholder="amqp://host:5672/"
            required
          />
          <Field label="Management API" value={form.adminUri} onChange={update('adminUri')} placeholder="http://host:15672" />
          <Field label="Vhost" value={form.namespaceOrVhost} onChange={update('namespaceOrVhost')} />
          <Field label="Username" value={form.username} onChange={update('username')} required />
          <Field label="Password" type="password" value={form.password} onChange={update('password')} required />

          <button type="submit" disabled={state.kind === 'saving'}>
            {state.kind === 'saving' ? 'Checking the broker…' : 'Save profile'}
          </button>
          <p style={{ fontSize: 12, color: '#555' }}>
            Saving checks the broker first. If it cannot be reached, or rejects the credentials,
            nothing is saved.
          </p>
        </form>
      )}

      {state.kind === 'saved' && (
        <section aria-labelledby="result-heading" style={{ marginTop: 24 }}>
          <h2 id="result-heading" style={{ fontSize: 18 }}>
            Saved — preflight {state.preflightState}
          </h2>
          <p style={{ color: '#555' }}>
            Revision {state.revision}. The checks below ran as part of saving, not afterwards.
          </p>

          <div aria-live="polite">
            <CheckList checks={state.preflight.checks} />
          </div>

          <button type="button" onClick={recheck} disabled={rechecking} style={{ marginTop: 12 }}>
            {rechecking ? 'Rechecking…' : 'Re-run preflight'}
          </button>

          <p style={{ color: state.preflight.state === 'passing' ? '#2e7d32' : '#8a4b00', marginTop: 12 }}>
            {state.preflight.state === 'passing'
              ? 'Preflight passed. Capture can be enabled once capture policies arrive (M2).'
              : 'Live metadata is available. Capture stays disabled until the failing checks pass.'}
          </p>

          {provider && <CapabilityList provider={provider} />}
        </section>
      )}
    </Shell>
  );
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 720, margin: '0 auto', padding: 32 }}>
      {children}
    </main>
  );
}

function CheckList({ checks }: { checks: Check[] }) {
  return (
    <ul style={{ listStyle: 'none', padding: 0 }}>
      {checks.map((check) => (
        <li key={check.id} style={{ padding: '8px 0', borderBottom: '1px solid #eee' }}>
          <strong>{check.label}</strong> — {check.state}
          {check.detail && <div style={{ color: '#555' }}>{check.detail}</div>}
          {check.remedy && <div style={{ color: '#8a4b00' }}>Remedy: {check.remedy}</div>}
        </li>
      ))}
    </ul>
  );
}

function CapabilityList({ provider }: { provider: ProviderCapabilities }) {
  const rows: Array<[string, boolean]> = [
    ['Observe traffic', provider.canTap],
    ['Read queue metadata', provider.canPeek],
    ['Publish a message', provider.canPublish],
    ['Discard messages', provider.canDiscard],
    ['Purge a queue', provider.canPurge],
    ['Replay messages', provider.canReplay],
    ['Declare a shadow queue', provider.canDeclareShadow],
  ];

  return (
    <section aria-labelledby="provider-heading" style={{ marginTop: 20 }}>
      <h3 id="provider-heading" style={{ fontSize: 15 }}>
        What this build can do with {provider.displayName}
      </h3>
      <ul style={{ listStyle: 'none', padding: 0, margin: '8px 0' }}>
        {rows.map(([label, available]) => (
          <li key={label} style={{ padding: '3px 0', color: available ? '#1b5e20' : '#777' }}>
            {available ? '✓' : '—'} {label}
            {!available && <span style={{ fontSize: 12 }}> (not in this build)</span>}
          </li>
        ))}
      </ul>
      <p style={{ fontSize: 12, color: '#555' }}>
        Provider support tier: {provider.supportTier}. Flags marked “not in this build” describe
        Marites, not {provider.displayName}.
      </p>
    </section>
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
    <label style={{ display: 'grid', gap: 4 }}>
      <span style={{ fontSize: 13, color: '#333' }}>{props.label}</span>
      <input
        type={props.type ?? 'text'}
        value={props.value}
        onChange={props.onChange}
        placeholder={props.placeholder}
        required={props.required}
        aria-describedby={hintId}
        style={{ padding: 8, fontSize: 14 }}
      />
      {props.hint && (
        <span id={hintId} style={{ fontSize: 12, color: '#555' }}>
          {props.hint}
        </span>
      )}
    </label>
  );
}
