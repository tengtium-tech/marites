# Deployment

The M0 deployment: a working UI against a test broker, with **no adopter-supplied database**.

## Clean host

```bash
export MARITES_KEK=$(openssl rand -base64 32)     # or any 32 random bytes, base64
docker compose -f deploy/compose.yaml up -d --wait
```

Open **http://localhost:8081**.

`--wait` returns only when every service is healthy. The API's healthcheck asks `/ready` on its
admin listener, which answers 503 until the bookkeeping store is reachable — so "healthy" here
means *able to serve*, not merely *started*.

## What runs

| Service | Port | Notes |
|---|---|---|
| `web` | 8081 → 80 | nginx serving the bundle and proxying `/api` to `api` |
| `api` | — | Not published. Reachable only through `web`, which keeps the browser on one origin |
| `rabbitmq` | — | The test broker. A real deployment uses the adopter's |

The admin listener (`/health`, `/ready`, default `127.0.0.1:9090`) is **not published** — it is
reachable from inside the container only, which is what configuration reference §2 requires.

## Why 8081

8080 is routinely occupied by a local `dotnet run`. Publishing on 8081 means the container and a
development API can coexist.

## Secrets

`MARITES_KEK` is required and has no default: compose refuses to start without it. Nothing else
is a secret — broker credentials are entered through the UI and stored encrypted per row.

## Known gaps

- **Images are built from the sibling worktrees**, not published to a registry. The contexts are
  `../../marites-csharp` and `../../marites-react/apps/web`; an adopter would need images instead.
- **No message store.** The bookkeeping store is a named volume; the optional PostgreSQL message
  store is not part of this profile (DEC-016 makes it optional).
- **Metrics are not exposed.** `/health` and `/ready` exist; a Prometheus endpoint does not.
