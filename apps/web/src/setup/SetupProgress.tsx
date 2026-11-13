import { cn } from '../lib/utils';

/**
 * The five steps from wireframes §3.5, drawn as the specification screenshot draws them
 * (docs/08-experience/images/04-setup-profile.png): a caption above the label, in boxes.
 *
 * M0 serves step 1 and step 3. The M2 steps are shown rather than hidden, so the operator can see
 * where the flow goes — and marked as later, so nothing implies they are available.
 */
export function SetupProgress({ current, done }: { current: 1 | 3; done: boolean }) {
  const steps = [
    { n: 1, label: 'Profile' },
    { n: 2, label: 'Message store' },
    { n: 3, label: 'Preflight' },
    { n: 4, label: 'Select queues' },
    { n: 5, label: 'Capture' },
  ];

  const state = (n: number) => (done ? (n <= 3 ? 'done' : 'later') : n === current ? 'current' : n < current ? 'done' : 'later');

  return (
    <ol className="mt-5 grid grid-cols-2 gap-2 md:grid-cols-5" aria-label="Setup progress">
      {steps.map((step) => (
        <li
          key={step.n}
          data-state={state(step.n)}
          className={cn(
            'rounded-lg border px-3 py-2 text-[12.5px] leading-tight',
            state(step.n) === 'current' && 'border-primary/55 bg-card text-foreground shadow-sm',
            state(step.n) === 'done' && 'bg-card text-foreground',
            state(step.n) === 'later' && 'bg-muted text-muted-foreground opacity-70'
          )}
        >
          <span
            className={cn(
              'block text-[10px] uppercase tracking-[0.08em]',
              state(step.n) === 'current' ? 'text-primary' : 'text-muted-foreground'
            )}
          >
            Step {step.n}
          </span>
          {step.label}
          {step.n > 3 && <div className="text-[10.5px] opacity-80">later milestone</div>}
        </li>
      ))}
    </ol>
  );
}
