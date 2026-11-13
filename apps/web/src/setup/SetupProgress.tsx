/**
 * The five steps from wireframes §3.5. M0 serves step 1 (profile) and step 3 (preflight); the
 * message store, queue selection and capture configuration are M2, so they render as later
 * steps rather than disappearing — the operator should see where the flow goes.
 */
export function SetupProgress({ current, done }: { current: 1 | 3; done: boolean }) {
  const steps = [
    { n: 1, label: 'Profile' },
    { n: 2, label: 'Message store' },
    { n: 3, label: 'Preflight' },
    { n: 4, label: 'Select queues' },
    { n: 5, label: 'Capture' },
  ];

  const state = (n: number) => {
    if (done) return n <= 3 ? 'done' : 'later';
    if (n === current) return 'current';
    return n < current ? 'done' : 'later';
  };

  return (
    <ol className="steps" aria-label="Setup progress">
      {steps.map((step) => (
        <li key={step.n} data-state={state(step.n)}>
          <span aria-hidden="true">{state(step.n) === 'done' ? '✓' : step.n}</span>
          {step.label}
          {step.n > 3 && <span className="small">· M2</span>}
        </li>
      ))}
    </ol>
  );
}
