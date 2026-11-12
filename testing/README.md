# Testing stacks

Throwaway brokers for local development and the integration level of the
[verification plan](../docs/10-testing/verification-plan.md). Nothing here is a deployment:
Marites is a client of a broker the adopter runs and never provisions one (ADR-001). The stack
exists so preflight and the adapters have something real to talk to.

## RabbitMQ

```bash
docker compose -f testing/compose.yaml up -d --wait
```

`--wait` blocks until the healthcheck passes, which matters: "the container is running" and
"the node answers AMQP" are several seconds apart, and a test that races that gap fails for
reasons nobody diagnoses twice.

| | |
|---|---|
| AMQP | `amqp://marites:marites@localhost:5672/` |
| Management API | `http://localhost:15672` (same credentials) |
| Container | `marites-rabbitmq` |

```bash
docker compose -f testing/compose.yaml down          # stop, keep nothing
docker compose -f testing/compose.yaml down -v       # stop and discard the volume
```

## What is not here yet

- **Service Bus and Pub/Sub.** The layout calls for one service per provider; neither is added
  until an adapter for it exists, because an emulator nothing talks to is upkeep without a
  purpose.
- **A CI job.** Testcontainers in CI is a real decision — image pull time, flakiness, and
  whether the integration level runs on every pull request or nightly. It belongs with the
  integration suite, not before it.
