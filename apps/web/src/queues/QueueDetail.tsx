import { useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import type { Problem, QueueDetail as QueueMetadata } from '../api/generated/client';
import { Card } from '../components/ui/card';

type QueueResult =
  | { kind: 'loading' }
  | { kind: 'loaded'; queue: QueueMetadata }
  | { kind: 'error'; message: string };

export function QueueDetail() {
  const { queueId } = useParams();
  const { search } = useLocation();

  return (
    <section>
      <Link to={{ pathname: '/queues', search }} className="text-sm text-primary hover:underline">
        Back to queues
      </Link>
      {queueId && <QueueDetailContent key={queueId} queueId={queueId} />}
    </section>
  );
}

function QueueDetailContent({ queueId }: { queueId: string }) {
  const [result, setResult] = useState<QueueResult>({ kind: 'loading' });

  useEffect(() => {
    const controller = new AbortController();

    async function loadQueue() {
      try {
        const response = await fetch(`/api/v1/queues/${encodeURIComponent(queueId)}`, {
          signal: controller.signal,
        });
        if (response.ok) {
          const queue = await response.json() as QueueMetadata;
          if (!controller.signal.aborted) setResult({ kind: 'loaded', queue });
        } else if (response.status === 404) {
          if (!controller.signal.aborted) {
            setResult({ kind: 'error', message: 'This queue could not be found.' });
          }
        } else {
          const problem = await response.json().catch(() => ({})) as Partial<Problem>;
          if (controller.signal.aborted) return;
          setResult({
            kind: 'error',
            message: problem.detail ?? problem.title ?? `Could not read queue details (HTTP ${response.status}).`,
          });
        }
      } catch {
        if (!controller.signal.aborted) {
          setResult({ kind: 'error', message: 'Could not reach Marites to read this queue.' });
        }
      }
    }

    void loadQueue();
    return () => controller.abort();
  }, [queueId]);

  if (result.kind === 'loading') {
    return <p role="status" className="mt-4 text-sm text-muted-foreground">Loading queue details…</p>;
  }
  if (result.kind === 'error') {
    return <p role="alert" className="mt-4 text-sm text-destructive">{result.message}</p>;
  }

  const { queue } = result;
  const metrics = [
    { label: 'Depth', value: queue.depth },
    { label: 'Consumers', value: queue.consumers },
    { label: 'Unacknowledged', value: queue.unacked },
  ];

  return (
    <>
      <h1 className="mt-4 break-words text-2xl font-semibold">{queue.name}</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        {queue.vhost && `Vhost ${queue.vhost} · `}Live broker metadata
      </p>
      <dl className="mt-5 grid gap-3 sm:grid-cols-3">
        {metrics.map(({ label, value }) => (
          <Card key={label} className="p-4">
            <dt className="text-sm text-muted-foreground">{label}</dt>
            <dd className="mt-1 text-xl font-semibold tabular-nums">{value ?? 'Unavailable'}</dd>
          </Card>
        ))}
      </dl>
      <Card className="mt-4 p-4">
        <h2 className="font-semibold">Queue status</h2>
        <dl className="mt-3 grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-muted-foreground">Dead-letter monitoring</dt>
            <dd>{queue.deadLetterConfigured ? 'Configured' : 'Unavailable'}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Capture</dt>
            <dd className="capitalize">{queue.captureState}</dd>
          </div>
        </dl>
      </Card>
    </>
  );
}
