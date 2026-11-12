import { useEffect, useRef, useState } from 'react';

type Check = { id: string; label: string; state: 'pass' | 'fail' | 'warn' | 'skipped'; detail?: string; remedy?: string };
type Preflight = { state: 'passing' | 'failing' | 'degraded'; checks: Check[]; checkedAt?: string };

type SaveState =
  | { kind: 'editing' }
  | { kind: 'saving' }
  | { kind: 'saved'; brokerId: string; revision: number; preflight?: Preflight }
  | { kind: 'error'; message: string };

const newIdempotencyKey = () => crypto.randomUUID();

type Capabilities = { features: string[] };

export function SetupWizard() {
  // What the backend says it can do, asked before anything is offered. Rendering a form the
  // API cannot honour is the failure /capabilities exists to prevent (ADR-017) — and it is
  // invisible from the frontend until a request 404s.
  const [capabilities, setCapabilities] = useState<Capabilities | null>(null);
  const [capabilitiesFailed, setCapabilitiesFailed] = useState(false);

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
  const errorRef = useRef<HTMLParagraphElement>(null);

  // An alert that is announced but off-screen still leaves the user hunting for it, so a
  // failed save moves focus to the message rather than relying on announcement alone.
  useEffect(() => {
    if (state.kind === 'error') {
      errorRef.current?.focus();
    }
  }, [state]);

  const update = (field: keyof typeof form) => (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
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
      setState({ kind: 'error', message: `The profile was not saved (${response.status}).` });
      return;
    }

    const created = await response.json();
    setState({ kind: 'saved', brokerId: created.id, revision: created.revision });
  }

  async function runPreflight() {
    if (state.kind !== 'saved') return;

    const response = await fetch(`/api/v1/brokers/${state.brokerId}/preflight`, {
      method: 'POST',
      headers: { 'Idempotency-Key': newIdempotencyKey() },
    });

    if (!response.ok) {
      setState({ ...state, preflight: undefined });
      return;
    }

    setState({ ...state, preflight: await response.json() });
  }

  const passing = state.kind === 'saved' && state.preflight?.state === 'passing';

  // Nothing renders until the answer arrives: a form that appears and then withdraws is worse
  // than a moment of blank, because the user has already started typing.
  if (capabilities === null && !capabilitiesFailed) {
    return <main aria-busy="true" />;
  }

  if (capabilitiesFailed) {
    return (
      <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 720, margin: '0 auto', padding: 32 }}>
        <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
        <p role="alert" style={{ color: '#b00020' }}>
          The backend could not be reached, so its capabilities are unknown. Nothing is offered
          until they are: a control that cannot work is worse than no control.
        </p>
      </main>
    );
  }

  if (!capabilities!.features.includes('brokers')) {
    return (
      <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 720, margin: '0 auto', padding: 32 }}>
        <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
        <p role="alert" style={{ color: '#8a4b00' }}>
          This backend does not report broker management as available, so the form is not shown.
          Its capabilities are: {capabilities!.features.length ? capabilities!.features.join(', ') : 'none'}.
        </p>
      </main>
    );
  }

  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 720, margin: '0 auto', padding: 32 }}>
      <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
      <p style={{ color: '#444' }}>
        Marites observes a broker you already run. It never provisions or repairs one.
      </p>

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
          {state.kind === 'saving' ? 'Saving…' : 'Save profile'}
        </button>
      </form>

      {state.kind === 'error' && (
        <p
          ref={errorRef}
          role="alert"
          tabIndex={-1}
          style={{ color: '#b00020', marginTop: 16 }}
        >
          {state.message}
        </p>
      )}

      {state.kind === 'saved' && (
        <section aria-labelledby="preflight-heading" style={{ marginTop: 24 }}>
          <p>
            Saved as revision {state.revision}. It is <strong>not verified</strong> until preflight passes.
          </p>

          <button type="button" onClick={runPreflight}>Run preflight</button>

          {state.preflight && (
            <>
              <h2 id="preflight-heading" style={{ fontSize: 18, marginTop: 20 }}>
                Preflight: {state.preflight.state}
              </h2>
              <div aria-live="polite">
              <ul style={{ listStyle: 'none', padding: 0 }}>
                {state.preflight.checks.map((check) => (
                  <li key={check.id} style={{ padding: '8px 0', borderBottom: '1px solid #eee' }}>
                    <strong>{check.label}</strong> — {check.state}
                    {check.detail && <div style={{ color: '#555' }}>{check.detail}</div>}
                    {check.remedy && <div style={{ color: '#8a4b00' }}>Remedy: {check.remedy}</div>}
                  </li>
                ))}
              </ul>
              </div>

              {/*
                No control here, deliberately. Capture is enabled by a capture policy, which the
                contract defers to M2, and rendering a button that cannot act is the failure
                /capabilities exists to prevent (ADR-017). The rule is still enforced where it
                belongs: a profile cannot be captured until preflight passes.
              */}
              <p style={{ color: passing ? '#2e7d32' : '#8a4b00' }}>
                {passing
                  ? 'Preflight passed. Capture can be enabled once capture policies arrive (M2).'
                  : 'Capture cannot be enabled until preflight passes. The profile is saved, so it can be corrected.'}
              </p>
            </>
          )}
        </section>
      )}
    </main>
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
