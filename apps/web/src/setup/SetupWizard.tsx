import { useState } from 'react';

type Check = { id: string; label: string; state: 'pass' | 'fail' | 'warn' | 'skipped'; detail?: string; remedy?: string };
type Preflight = { state: 'passing' | 'failing' | 'degraded'; checks: Check[]; checkedAt?: string };

type SaveState =
  | { kind: 'editing' }
  | { kind: 'saving' }
  | { kind: 'saved'; brokerId: string; revision: number; preflight?: Preflight }
  | { kind: 'error'; message: string };

const newIdempotencyKey = () => crypto.randomUUID();

export function SetupWizard() {
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

  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 720, margin: '0 auto', padding: 32 }}>
      <h1 style={{ fontSize: 24 }}>Connect a broker</h1>
      <p style={{ color: '#444' }}>
        Marites observes a broker you already run. It never provisions or repairs one.
      </p>

      <form onSubmit={save} style={{ display: 'grid', gap: 12, marginTop: 24 }}>
        <Field label="Name" value={form.name} onChange={update('name')} required />
        <Field label="AMQP endpoint" value={form.endpoint} onChange={update('endpoint')} placeholder="amqp://host:5672/" required />
        <Field label="Management API" value={form.adminUri} onChange={update('adminUri')} placeholder="http://host:15672" />
        <Field label="Vhost" value={form.namespaceOrVhost} onChange={update('namespaceOrVhost')} />
        <Field label="Username" value={form.username} onChange={update('username')} required />
        <Field label="Password" type="password" value={form.password} onChange={update('password')} required />

        <button type="submit" disabled={state.kind === 'saving'}>
          {state.kind === 'saving' ? 'Saving…' : 'Save profile'}
        </button>
      </form>

      {state.kind === 'error' && (
        <p role="alert" style={{ color: '#b00020', marginTop: 16 }}>{state.message}</p>
      )}

      {state.kind === 'saved' && (
        <section style={{ marginTop: 24 }}>
          <p>
            Saved as revision {state.revision}. It is <strong>not verified</strong> until preflight passes.
          </p>

          <button type="button" onClick={runPreflight}>Run preflight</button>

          {state.preflight && (
            <>
              <h2 style={{ fontSize: 18, marginTop: 20 }}>Preflight: {state.preflight.state}</h2>
              <ul style={{ listStyle: 'none', padding: 0 }}>
                {state.preflight.checks.map((check) => (
                  <li key={check.id} style={{ padding: '8px 0', borderBottom: '1px solid #eee' }}>
                    <strong>{check.label}</strong> — {check.state}
                    {check.detail && <div style={{ color: '#555' }}>{check.detail}</div>}
                    {check.remedy && <div style={{ color: '#8a4b00' }}>Remedy: {check.remedy}</div>}
                  </li>
                ))}
              </ul>

              {/* The acceptance criterion: a failing preflight blocks enabling, not saving. */}
              <button type="button" disabled={!passing} title={passing ? undefined : 'Preflight must pass before capture can be enabled'}>
                Enable capture
              </button>
              {!passing && (
                <p style={{ color: '#8a4b00' }}>
                  Capture stays disabled until preflight passes. The profile is saved, so it can be corrected.
                </p>
              )}
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
}) {
  return (
    <label style={{ display: 'grid', gap: 4 }}>
      <span style={{ fontSize: 13, color: '#333' }}>{props.label}</span>
      <input
        type={props.type ?? 'text'}
        value={props.value}
        onChange={props.onChange}
        placeholder={props.placeholder}
        required={props.required}
        style={{ padding: 8, fontSize: 14 }}
      />
    </label>
  );
}
