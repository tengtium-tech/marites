import * as React from "react"
import { AlertTriangle, Database, HardDrive, Lock } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"

export type StorageConfig = {
  configured: boolean
  engine: "postgres" | "sqlserver"
  host: string
  database: string
  schema: string
  since?: string
}

/**
 * Storage is per install, not per broker: one message store holds every profile,
 * queue and count. It can be added after first run — and adding it is the
 * moment history begins (DEC-012, DEC-014).
 */
export function StorageSettingsScreen({
  config,
  onEnable,
  onReplace,
}: {
  config: StorageConfig
  onEnable: (cfg: StorageConfig) => void
  onReplace: (cfg: StorageConfig) => void
}) {
  const [engine, setEngine] = React.useState<"postgres" | "sqlserver">(config.engine)
  const [mode, setMode] = React.useState<"precreated" | "create">("precreated")
  const [host, setHost] = React.useState("db.internal")
  const [database, setDatabase] = React.useState("marites")
  const [schema, setSchema] = React.useState("marites")
  const [tested, setTested] = React.useState(false)
  const [replacing, setReplacing] = React.useState(false)

  if (config.configured && !replacing) {
    return (
      <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
        <div className="mb-5">
          <h1 className="text-[22px] font-semibold tracking-[-0.025em]">Storage</h1>
          <p className="mt-0.5 text-[13px] text-muted-foreground">
            One message store per install. Every broker profile, queue and count writes to it.
          </p>
        </div>

        <div className="grid grid-cols-[1.1fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle>Connected database</CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <div className="flex items-center gap-2.5">
                <span className="grid h-8 w-8 place-items-center rounded-lg bg-success/10">
                  <Database className="h-4 w-4 text-success" />
                </span>
                <div className="leading-tight">
                  <div className="text-[13.5px] font-semibold">
                    {config.engine === "postgres" ? "PostgreSQL" : "SQL Server"}
                  </div>
                  <div className="text-[11.5px] text-muted-foreground">
                    {config.host} · {config.database} · schema{" "}
                    <span className="font-mono">{config.schema}</span>
                  </div>
                </div>
                <Badge variant="success" className="ml-auto">
                  healthy
                </Badge>
              </div>

              <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 border-t pt-3.5 text-[13px]">
                <dt className="text-muted-foreground">Migration role</dt>
                <dd className="text-right font-mono text-[12px]">marites_migrator</dd>
                <dt className="text-muted-foreground">Runtime role</dt>
                <dd className="text-right font-mono text-[12px]">marites_app</dd>
                <dt className="text-muted-foreground">Schema version</dt>
                <dd className="text-right font-medium">0012 · current</dd>
                <dt className="text-muted-foreground">Counting since</dt>
                <dd className="text-right font-medium">{config.since ?? "install"}</dd>
              </dl>

              <div className="mt-4 flex gap-2.5 border-t pt-3.5">
                <Button variant="outline">Test connection</Button>
                <Button variant="outline" onClick={() => setReplacing(true)}>
                  Replace…
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>What this database holds — and what it does not</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <ul className="space-y-2">
                {[
                  "Counts — every message, kept for years, never rebuilt from rows",
                  "Captured rows — the investigation window, per queue policy",
                  "The redelivery ledger that keeps the counting exact",
                ].map((l) => (
                  <li key={l} className="flex gap-2">
                    <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                    {l}
                  </li>
                ))}
              </ul>
              <p className="mt-3.5 border-t pt-3.5">
                <b className="text-foreground/80">Traffic only.</b> Broker profiles, credentials and
                the audit log live in Marites&apos; own store — they are never written here, and they
                keep working whether this is connected or not.
              </p>
              <p className="mt-2.5">
                <b className="text-foreground/80">Replacing it does not migrate captured rows.</b>{" "}
                Counts restart from the new database; the old one is left untouched for you to keep or
                drop.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    )
  }

  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-5">
        <h1 className="text-[22px] font-semibold tracking-[-0.025em]">
          {replacing ? "Replace the database" : "Add storage"}
        </h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          {replacing
            ? "The new database starts empty. Captured rows and counts in the current one are not migrated."
            : "This install is running live-only. Connecting a message store turns on history — counts, search and past payloads. Recovery and the audit are already working."}
        </p>
      </div>

      <div className="grid grid-cols-[1.1fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Database</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3.5 pt-0">
            <div>
              <Label>Engine</Label>
              <div className="mt-1.5 flex gap-0.5 rounded-[9px] bg-secondary p-0.5">
                {(
                  [
                    ["postgres", "PostgreSQL"],
                    ["sqlserver", "SQL Server"],
                  ] as const
                ).map(([id, label]) => (
                  <button
                    key={id}
                    onClick={() => {
                      setEngine(id)
                      setTested(false)
                    }}
                    aria-pressed={engine === id}
                    className={cn(
                      "flex-1 rounded-[7px] px-2 py-1.5 text-[12.5px] text-muted-foreground transition-all",
                      engine === id && "bg-card font-semibold text-foreground shadow-sm"
                    )}
                  >
                    {label}
                  </button>
                ))}
              </div>
              <p className="mt-1.5 text-[11.5px] text-muted-foreground">
                PostgreSQL is recommended: retention is a metadata operation and header queries are
                an index inside the document.
              </p>
            </div>

            <div>
              <Label>Who creates the database</Label>
              <div className="mt-1.5 grid grid-cols-2 gap-2">
                {(
                  [
                    ["precreated", "Your DBA creates it"],
                    ["create", "Marites creates it"],
                  ] as const
                ).map(([id, label]) => (
                  <button
                    key={id}
                    onClick={() => setMode(id)}
                    aria-pressed={mode === id}
                    className={cn(
                      "rounded-md border bg-card px-2.5 py-2 text-[12.5px] transition-colors",
                      mode === id
                        ? "border-primary bg-accent font-semibold text-accent-foreground"
                        : "text-muted-foreground hover:bg-secondary"
                    )}
                  >
                    {label}
                  </button>
                ))}
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="st-host">Host</Label>
                <Input
                  id="st-host"
                  value={host}
                  onChange={(e) => {
                    setHost(e.target.value)
                    setTested(false)
                  }}
                  className="mt-1.5 font-mono text-[12px]"
                />
              </div>
              <div>
                <Label htmlFor="st-db">Database</Label>
                <Input
                  id="st-db"
                  value={database}
                  onChange={(e) => setDatabase(e.target.value)}
                  className="mt-1.5 font-mono text-[12px]"
                />
              </div>
            </div>

            <div>
              <Label htmlFor="st-schema">Schema</Label>
              <Input
                id="st-schema"
                value={schema}
                onChange={(e) => setSchema(e.target.value)}
                className="mt-1.5 font-mono text-[12px]"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="st-mig">Migration role</Label>
                <Input id="st-mig" defaultValue="marites_migrator" className="mt-1.5 font-mono text-[12px]" />
              </div>
              <div>
                <Label htmlFor="st-app">Runtime role</Label>
                <Input id="st-app" defaultValue="marites_app" className="mt-1.5 font-mono text-[12px]" />
              </div>
            </div>

            <p className="flex items-start gap-1.5 text-[11.5px] leading-relaxed text-muted-foreground">
              <Lock className="mt-0.5 h-3 w-3 shrink-0" /> Credentials are write-only, encrypted per
              row, never returned. The runtime role holds no DDL — migrations run as a separate step.
            </p>

            {tested && (
              <div className="rounded-lg border border-success/30 bg-success/[0.06] px-3 py-2.5 text-[12px] leading-relaxed text-success">
                Connected · {engine === "postgres" ? "PostgreSQL 16" : "SQL Server 2022"} · schema{" "}
                <span className="font-mono">{schema}</span> · 10 tables to create
              </div>
            )}

            <div className="flex flex-wrap gap-2.5 border-t pt-3.5">
              <Button variant="outline" onClick={() => setTested(true)} disabled={tested}>
                {tested ? "Connected" : "Test connection"}
              </Button>
              <Button
                disabled={!tested}
                onClick={() =>
                  onEnable({ configured: true, engine, host, database, schema, since: "just now" })
                }
              >
                {replacing ? "Replace and start fresh" : "Enable storage"}
              </Button>
              {replacing && (
                <Button variant="ghost" onClick={() => setReplacing(false)}>
                  Cancel
                </Button>
              )}
            </div>
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle>What changes when you enable it</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <div className="space-y-2">
                {[
                  ["Counts over time", "turns on"],
                  ["Message list, search, payloads", "turns on"],
                  ["Replay, discard, purge, publish", "already on"],
                  ["Audit log and payload evidence", "already on"],
                ].map(([label, state]) => (
                  <div key={label} className="flex items-center justify-between gap-3">
                    <span>{label}</span>
                    <Badge
                      variant={state === "turns on" ? "success" : "idle"}
                      className="px-1.5 py-0 text-[9.5px] uppercase"
                    >
                      {state}
                    </Badge>
                  </div>
                ))}
              </div>
              <p className="mt-3 border-t pt-3">
                Replay, discard and purge are not affected — they were always audited in Marites'
                own store. What this adds is memory: the traffic before it is connected was never
                recorded and cannot be reconstructed.
              </p>
            </CardContent>
          </Card>

          <Card className="border-warning/30">
            <CardContent className="pt-4">
              <div className="flex gap-2.5 text-[12.5px] leading-relaxed text-warning">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <div>
                  <b>Counts begin now.</b> The period before this point was never recorded and cannot
                  be reconstructed — there is nothing to backfill from.
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>One message store per install</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <p className="flex items-start gap-2">
                <HardDrive className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                Every broker profile shares it. Adding a second broker does not need a second
                database — and a second database would split your counts.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
