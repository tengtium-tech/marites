---
document_id: DOC-OPS-006
title: Storage Prerequisites
product: Marites
version: 0.1
date: 2026-11
status: Approved — Product owner, 2026-11-11
---

# DOC-OPS-006 — Storage Prerequisites

**Source:** [DEC-012](../01-project/decisions-and-open-questions.md); [ADR-022](../05-architecture/decisions.md); [migration plan](../07-data-and-contracts/migration-plan.md). Owning document for the rights the adopter must grant on their database, and the commands that prove they were granted.

The adopter supplies the database ([DEC-012](../01-project/decisions-and-open-questions.md)). This document says what Marites needs inside it, per engine, and what it must never be given.

## 1. Which engine to choose

**PostgreSQL is the recommended default.** SQL Server and Azure SQL are supported for organisations that standardise on them.

| | PostgreSQL | SQL Server / Azure SQL |
|---|---|---|
| Status | **Recommended** — the reference implementation | Supported |
| Built | First (M2), because it defines what the storage port needs | Second (M7) |
| Retention | Native `RANGE` partitioning; dropping a day is a metadata operation | Partition function and scheme, created up front and maintained |
| Header queries | `JSONB` + GIN index | JSON in `nvarchar` with computed columns; no GIN equivalent |
| Extensions | `pg_trgm` is trusted since PG 13, so the database owner can install it without superuser | n/a |
| Cost | One container, no licence | Licence, and Azure SQL bills by DTU or vCore |

The recommendation is not a preference for open source. It is that the two features the capture path depends on most — dropping a partition and indexing inside a JSON document — are one line each on PostgreSQL and a design exercise on SQL Server. An adopter with no standard should pick PostgreSQL and get the cheaper, faster path.

## 2. Two roles, always

| Role | Used by | Holds | Used |
|---|---|---|---|
| **Migration** | Install, and every upgrade | DDL: create schema objects, partitions, indexes | Briefly, then idle |
| **Runtime** | The application, continuously | DML only | Always |

**Why two.** The migration role can create and drop tables. If that credential is also the one the application runs with, a bug or a compromised process has DDL rights for its whole life. [DOC-DATA-001 §4](../07-data-and-contracts/data-model.md) already requires the runtime role not to own the tables; this is the same rule, expressed as two logins.

**Where one credential is unavoidable**, the adopter is choosing DDL-at-runtime. Marites still works, the least-privilege claim does not, and the onboarding screen says so rather than pretending.

## 3. PostgreSQL

### 3.1 Mode A — Marites creates the database

```sql
-- as a role that may create roles and databases
CREATE ROLE marites_migrator LOGIN PASSWORD '...' CREATEDB;
CREATE ROLE marites_app      LOGIN PASSWORD '...';

CREATE DATABASE marites OWNER marites_migrator;

-- inside the new database
CREATE SCHEMA marites AUTHORIZATION marites_migrator;
GRANT CONNECT ON DATABASE marites TO marites_app;
GRANT USAGE ON SCHEMA marites TO marites_app;

-- runtime rights, applied after the tables exist
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA marites TO marites_app;
ALTER DEFAULT PRIVILEGES FOR ROLE marites_migrator IN SCHEMA marites
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO marites_app;
```

`CREATEDB` is a role attribute, **not superuser**. An adopter who will not grant it should use mode B.

### 3.2 Mode B — the DBA creates an empty database

```sql
CREATE DATABASE marites;
\c marites
CREATE SCHEMA marites AUTHORIZATION marites_migrator;

GRANT CONNECT ON DATABASE marites TO marites_migrator, marites_app;
GRANT USAGE, CREATE ON SCHEMA marites TO marites_migrator;
GRANT USAGE ON SCHEMA marites TO marites_app;
```

This is the mode that gets approved: the DBA keeps control of the server, and the application gets rights on one schema. It is the same shape as the broker answer in [DEC-003](../01-project/decisions-and-open-questions.md).

### 3.3 Verify it

```sql
-- the migrator can create, the runtime role cannot
SELECT has_schema_privilege('marites_migrator', 'marites', 'CREATE');  -- true
SELECT has_schema_privilege('marites_app',      'marites', 'CREATE');  -- false

-- neither is a superuser
SELECT rolname, rolsuper, rolcreatedb FROM pg_roles WHERE rolname LIKE 'marites%';
```

The second query is the one that matters. A runtime role with `CREATE` passes every connection test and fails the only requirement that keeps an incident contained.

## 4. SQL Server

### 4.1 Grants

```sql
-- mode A only: create the database
ALTER SERVER ROLE dbcreator ADD MEMBER marites_migrator;

-- inside the database
ALTER ROLE db_ddladmin   ADD MEMBER marites_migrator;
ALTER ROLE db_datareader ADD MEMBER marites_app;
ALTER ROLE db_datawriter ADD MEMBER marites_app;
```

`db_ddladmin` covers table, index and partition-function creation. It does not grant the right to grant permissions, which is what you want: the migrator can build the schema and cannot widen its own access.

### 4.2 Verify it

```sql
SELECT IS_SRVROLEMEMBER('dbcreator', 'marites_migrator');   -- 1 in mode A, 0 in mode B
SELECT IS_ROLEMEMBER('db_ddladmin', 'marites_migrator');    -- 1
SELECT IS_ROLEMEMBER('db_owner',    'marites_app');         -- must be 0
```

## 5. Azure SQL — the differences that matter

Azure SQL is SQL Server with a control plane on top, and four things differ from an on-premises instance.

| Difference | Consequence for onboarding |
|---|---|
| **Creating a database is a `master` operation** | Mode A needs a credential that can reach `master` and is a member of `dbmanager`. Many adopters refuse a second connection string for a one-time action — **mode B is the normal answer here** |
| **Users are contained in the database** | `CREATE USER marites_app WITH PASSWORD = '...'` inside the target database. There is no server login to reuse |
| **Entra ID authentication is preferred** | `CREATE USER [marites-app] FROM EXTERNAL PROVIDER;` — the same "ambient credential stores no secret" argument as [ADR-018](../05-architecture/decisions.md) for Service Bus |
| **A firewall sits in front** | The application's egress address must be allowed, or a private endpoint used. A connection timeout during setup is usually this, not a wrong password |

```sql
-- mode A, connected to master
ALTER ROLE dbmanager ADD MEMBER marites_migrator;

-- inside the target database
CREATE USER marites_migrator FROM EXTERNAL PROVIDER;
ALTER ROLE db_ddladmin ADD MEMBER marites_migrator;
CREATE USER marites_app FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER marites_app;
ALTER ROLE db_datawriter ADD MEMBER marites_app;
```

**Sizing is a capture concern, not a storage concern.** Capture writes in batches of 500; on a small DTU tier the bulk insert is the first thing to throttle, and it shows up as a growing batch buffer rather than as an error. The load test (TC-012) is run against the tier the adopter intends to use, not against a laptop.

## 6. What Marites must never be given

| Never | Why |
|---|---|
| `superuser` (PostgreSQL) or `sysadmin` (SQL Server) | Nothing in the design needs it. A request for it should end the review |
| `db_owner` for the runtime role | It carries DDL for the life of the process |
| Rights outside its own schema | The product touches one schema; a grant beyond it cannot be justified |
| `CREATE ROLE` / `ALTER ROLE` | Marites creates tables, not logins |

## 7. What is in your database, and how to look at it

The database you connect holds **traffic and nothing else** ([DEC-012](../01-project/decisions-and-open-questions.md)). What Marites did — who replayed what, with which body — is kept in Marites' own store and never written here. That distinction is the point of the split, and it means the schema you are granting is small enough to explain in one screen.

### 7.1 The three tables

| Table | What it holds | Retention | Visible in the product |
|---|---|---|---|
| `message_capture` | The messages themselves — every one that crossed the broker, dead-letter or not | Per-queue row retention, enforced by partition drop | Queue detail, search, peek |
| `message_rollup` | Counts per queue per interval, derived from the messages | Long; never rebuilt from rows (ADR-007) | Reporting dashboard |
| `capture_dedupe` | The redelivery ledger that makes counting exact | One redelivery window | **No screen** — internal |

**Why `capture_dedupe` is here rather than with the rest.** It is bookkeeping by nature, but ADR-019 requires its write to share the capture batch's transaction, and a transaction cannot span two databases. It sits with the messages for that reason and no other.

**What is deliberately absent.** No audit rows, no payload evidence for operations, no configuration, no roles. If you are looking for *"who purged the queue on Tuesday"*, that is Marites' own store — the [audit log](../08-experience/wireframes-and-navigation.md) shows it in the product, and it is not in the schema you granted.

### 7.2 What is **not** in the product

There is no screen reporting **how much** Marites has written — rows, bytes, the age of the oldest captured message, or usage against the storage ceiling. The ceiling is real (`ERR-QUE-003` refuses a policy that would exceed it, and capture pauses at it), so an operator who wants to know where they stand reads it from the database:

```sql
-- size of the Marites schema and its largest tables
SELECT relname,
       pg_size_pretty(pg_total_relation_size(c.oid)) AS total
FROM   pg_class c
JOIN   pg_namespace n ON n.oid = c.relnamespace
WHERE  n.nspname = 'marites' AND c.relkind = 'r'
ORDER  BY pg_total_relation_size(c.oid) DESC
LIMIT  10;

-- the investigation window, and what the sweeper will drop next
SELECT queue_ref_id, min(captured_at), max(captured_at), count(*)
FROM   marites.message_capture
GROUP  BY queue_ref_id;
```

SQL Server equivalents are `sys.dm_db_partition_stats` and the same aggregate over `message_capture`. Both run as the **runtime role**, which holds `SELECT` — no elevated credential is needed to answer the question.

**Why a gauge was not built.** A storage-usage panel is a small feature that invites a large one: once the product reports bytes it is expected to report them per table, per queue, per policy, with history and alerts — and every one of those numbers is an engine-specific query behind the storage port. The adopter already monitors the server this database lives on. The product's obligation is to make the numbers *findable*, which this section does, not to duplicate their monitoring.

**What would change that.** If adopters report the ceiling arriving as a surprise — capture pausing with no visible warning — then the answer is a gauge on `/storage`, and it belongs to that milestone rather than to this one.

## 8. Document control

Revision 0.1 records the grants for the two engines in v1, written November 2026 with the storage decision (DEC-012, ADR-022). No database has been connected, no grant has been tested, and every statement is derived from the engines' documented permission models rather than from an observed setup. Revision 0.2 adds §7: the table inventory, what is deliberately not in the product, and the queries that answer "how much, and until when" — because the adopter-owned database had no documented way to be inspected. Revision 0.3 corrects §7 to three tables: the first draft listed all twelve, which was the over-scope DEC-012 was revised to remove.
