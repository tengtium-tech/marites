import * as React from "react"
import {
  Activity,
  AlertTriangle,
  ArrowLeft,
  BarChart3,
  Bot,
  ChevronDown,
  Database,
  LogOut,
  Maximize2,
  Moon,
  RefreshCw,
  RotateCcw,
  ScrollText,
  Send,
  Settings2,
  ShieldAlert,
  Sun,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Checkbox } from "@/components/ui/checkbox"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"
import { MaritesMark } from "@/components/marites-mark"
import { ChatDrawer } from "@/components/chat-drawer"
import { AiSettingsScreen } from "@/components/ai-settings"
import { StorageSettingsScreen, type StorageConfig } from "@/components/storage-settings"
import {
  PayloadDialog,
  diffLines,
  PAYLOAD_LABEL,
  type PayloadAction,
  type PayloadView,
} from "@/components/payload-dialog"

/* ------------------------------------------------------------------ */
/* Types — captureState values are VOCABULARY §15                      */
/* ------------------------------------------------------------------ */

type Role = "viewer" | "support" | "admin"
type Screen = "queues" | "queue" | "publish" | "report" | "audit" | "ai" | "storage" | "setup"

/** The one place a hash becomes a screen. Two copies of this logic drifted
    apart once already — the mount path mapped the queue filters and the
    hashchange path did not, which blanked the app on a link click. */
function currentHash() {
  return window.location.hash.replace("#", "")
}

function screenForHash(h: string): Screen | null {
  if (h === "replay" || h === "peek" || h === "discard" || h === "purge" || h === "discard-payload")
    return "queue"
  if (h === "audit-payload" || h === "audit-bulk" || h === "audit-expired" || h.startsWith("payload-"))
    return "audit"
  if (h === "queues" || h === "queues-dlq" || h === "queues-capture") return "queues"
  if (h === "report" || h === "setup" || h === "queue" || h === "publish" || h === "audit" || h === "ai" || h === "storage")
    return h
  return null
}
type CaptureState = "live" | "stale" | "capped" | "disabled" | "unavailable"
type Provider = "rabbitmq" | "servicebus" | "pubsub"

const CAPTURE_COPY: Record<CaptureState, string> = {
  live: "live",
  stale: "stale",
  capped: "capped",
  disabled: "disabled",
  unavailable: "unavailable",
}

/** Status is a word plus a tinted chip — never colour alone (DOC-UX-002 §2.2). */
function Status({ state }: { state: CaptureState }) {
  const variant =
    state === "live" ? "success" : state === "stale" || state === "capped" ? "warning" : "idle"
  return (
    <Badge variant={variant}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden="true" />
      {CAPTURE_COPY[state]}
    </Badge>
  )
}

const QUEUES: {
  name: string
  depth: number
  rate: number
  dead: number | null
  deadRate?: string
  state: CaptureState
  bar: number
}[] = [
  { name: "orders", depth: 12481, rate: 4200, dead: 1204, deadRate: "28.6%", state: "live", bar: 78 },
  { name: "orders.dlq", depth: 1204, rate: 12, dead: null, state: "live", bar: 6 },
  { name: "payments", depth: 0, rate: 0, dead: null, state: "live", bar: 0 },
  { name: "reports", depth: 3110, rate: 0, dead: 88, deadRate: "0.4%", state: "stale", bar: 34 },
  { name: "shipments", depth: 842, rate: 310, dead: 0, deadRate: "0%", state: "capped", bar: 12 },
]

const nf = new Intl.NumberFormat("en-US")

/* ------------------------------------------------------------------ */
/* Demo accounts                                                       */
/* ------------------------------------------------------------------ */

type Session = { email: string; role: Role }

const ACCOUNTS: { email: string; role: Role; label: string; blurb: string }[] = [
  {
    email: "viewer@company.com",
    role: "viewer",
    label: "Executive",
    blurb: "Volume, trend and dead-letter rate",
  },
  {
    email: "support@company.com",
    role: "support",
    label: "Support",
    blurb: "Peek, discard, replay and publish",
  },
  {
    email: "admin@company.com",
    role: "admin",
    label: "Admin",
    blurb: "Everything, plus purge and brokers",
  },
]

function roleFromEmail(email: string): Role | null {
  return ACCOUNTS.find((a) => a.email === email.trim().toLowerCase())?.role ?? null
}

/* ------------------------------------------------------------------ */
/* Sign in                                                             */
/* ------------------------------------------------------------------ */

function Login({
  onSignIn,
  dark,
  onDark,
}: {
  onSignIn: (email: string) => void
  dark: boolean
  onDark: (v: boolean) => void
}) {
  const [email, setEmail] = React.useState("")
  const [password, setPassword] = React.useState("")
  const [error, setError] = React.useState<string | null>(null)

  function submit(e: React.FormEvent) {
    e.preventDefault()
    const role = roleFromEmail(email)
    if (!role) {
      setError("No demo account with that address. Use one of the three above.")
      return
    }
    if (!password.trim()) {
      setError("Any password works in this prototype — but type something.")
      return
    }
    onSignIn(email)
  }

  return (
    <div className="grid min-h-screen grid-cols-[1.05fr_1fr] max-[900px]:grid-cols-1">
      {/* left: the argument for the product */}
      <aside className="relative hidden flex-col justify-between overflow-hidden border-r border-border bg-[#0B0E14] p-10 text-white max-[900px]:hidden min-[901px]:flex">
        <div
          className="pointer-events-none absolute inset-0 opacity-90"
          style={{
            background:
              "radial-gradient(900px 500px at 15% 0%, rgba(88,112,255,0.28), transparent 60%), radial-gradient(700px 400px at 90% 90%, rgba(88,112,255,0.16), transparent 60%)",
          }}
        />
        <div className="relative flex items-center gap-2.5">
          <MaritesMark size={28} />
          <div className="leading-tight">
            <div className="text-[14.5px] font-semibold tracking-[-0.02em]">Marites</div>
            <div className="text-[10.5px] text-white/50">queue observability</div>
          </div>
        </div>

        <div className="relative max-w-[30ch]">
          <h1 className="text-[30px] font-semibold leading-[1.15] tracking-[-0.03em]">
            See what failed, and fix it without SSH.
          </h1>
          <p className="mt-3.5 text-[13.5px] leading-relaxed text-white/60">
            Marites observes the broker you already run, counts every message exactly, and lets support
            retry, discard or publish from one screen.
          </p>

          <div className="mt-7 space-y-3 text-[12.5px]">
            {[
              "Exact counts, kept long after the rows expire",
              "Dead-letter reasons, not just a count",
              "Every action audited, nothing scheduled",
            ].map((line) => (
              <div key={line} className="flex items-start gap-2.5 text-white/75">
                <span className="mt-[3px] h-1.5 w-1.5 shrink-0 rounded-full bg-[#8AA0FF]" />
                {line}
              </div>
            ))}
          </div>
        </div>

        <div className="relative text-[10.5px] text-white/35">
          Prototype · not authoritative · <span className="font-mono">docs/</span> is the specification
        </div>
      </aside>

      {/* right: sign in */}
      <main className="flex flex-col justify-center bg-background px-6 py-10">
        <div className="mx-auto w-full max-w-[392px]">
          <div className="mb-8 flex items-center justify-between">
            <div className="flex items-center gap-2.5 min-[901px]:hidden">
              <MaritesMark size={28} />
              <span className="text-[14.5px] font-semibold tracking-[-0.02em]">Marites</span>
            </div>
            <div className="ml-auto">
              <Button variant="ghost" size="icon" onClick={() => onDark(!dark)} aria-label="Toggle theme">
                {dark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
              </Button>
            </div>
          </div>

          <h2 className="text-[22px] font-semibold tracking-[-0.025em]">Sign in</h2>
          <p className="mt-1 text-[13px] text-muted-foreground">
            Three demo accounts. Each lands on the surface its role can actually use.
          </p>

          <div className="mt-5 space-y-2">
            {ACCOUNTS.map((a) => (
              <button
                key={a.email}
                onClick={() => onSignIn(a.email)}
                className="group flex w-full items-center gap-3 rounded-lg border bg-card px-3 py-2.5 text-left shadow-sm transition-all hover:border-primary/40 hover:bg-accent/40"
              >
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-secondary text-[11.5px] font-bold uppercase text-secondary-foreground">
                  {a.role[0]}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-baseline gap-2">
                    <span className="truncate text-[13px] font-medium">{a.email}</span>
                    <span className="shrink-0 rounded-full bg-secondary px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-[0.05em] text-muted-foreground">
                      {a.label}
                    </span>
                  </span>
                  <span className="mt-0.5 block text-[11.5px] leading-snug text-muted-foreground">
                    {a.blurb}
                  </span>
                </span>
                <span className="shrink-0 text-[11.5px] font-medium text-primary opacity-0 transition-opacity group-hover:opacity-100">
                  Continue
                </span>
              </button>
            ))}
          </div>

          <div className="my-5 flex items-center gap-3">
            <div className="h-px flex-1 bg-border" />
            <span className="text-[10.5px] uppercase tracking-[0.08em] text-muted-foreground">
              or with an email
            </span>
            <div className="h-px flex-1 bg-border" />
          </div>

          <form onSubmit={submit} className="space-y-3">
            <div>
              <Label htmlFor="login-email">Email</Label>
              <Input
                id="login-email"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
                  setError(null)
                }}
                placeholder="support@company.com"
                autoComplete="username"
                className="mt-1.5"
              />
            </div>
            <div>
              <Label htmlFor="login-password">Password</Label>
              <Input
                id="login-password"
                type="password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value)
                  setError(null)
                }}
                placeholder="any password"
                autoComplete="current-password"
                className="mt-1.5"
              />
            </div>

            {error && (
              <p className="rounded-md bg-destructive/[0.08] px-2.5 py-2 text-[12px] text-destructive">
                {error}
              </p>
            )}

            <Button type="submit" className="w-full">
              Sign in
            </Button>
          </form>

          <p className="mt-5 text-[11.5px] leading-relaxed text-muted-foreground">
            The real build redirects to <b className="text-foreground/80">Entra ID</b> — Marites never
            renders a sign-in form (ADR-008). This screen exists so the prototype can show role-based
            entry, and as a sketch of the break-glass form, which is currently undesigned.
          </p>
        </div>
      </main>
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Shell                                                               */
/* ------------------------------------------------------------------ */

export default function App() {
  const [session, setSession] = React.useState<Session | null>(() => {
    const stored = sessionStorage.getItem("marites.demo")
    if (stored) {
      try {
        const s = JSON.parse(stored) as Session
        if (s?.email && s?.role) return s
      } catch {
        /* a corrupt value is no session */
      }
    }
    // a deep link implies the role that can see that surface
    const h = window.location.hash.replace("#", "")
    if (h === "setup" || h === "purge" || h === "ai" || h === "storage" || h === "chat-admin")
      return { email: "admin@company.com", role: "admin" }
    if (h === "report") return { email: "viewer@company.com", role: "viewer" }
    if (
      h === "queues" ||
      h === "queues-dlq" ||
      h === "queues-capture" ||
      h === "queue" ||
      h === "publish" ||
      h === "audit" ||
      h === "audit-payload" ||
      h === "audit-bulk" ||
      h === "audit-expired" ||
      h.startsWith("payload-") ||
      h === "chat" ||
      h === "chat-answer" ||
      h === "chat-admin" ||
      h === "replay" ||
      h === "peek" ||
      h === "discard" ||
      h === "discard-payload"
    )
      return { email: "support@company.com", role: "support" }
    return null
  })

  const [screen, setScreen] = React.useState<Screen>(() => screenForHash(currentHash()) ?? "queues")
  /* Deep links are read on mount; this makes them work when the hash changes
     in an already-open tab, which is how a person actually clicks one. */
  const [hashTick, setHashTick] = React.useState(0)
  React.useEffect(() => {
    function onHash() {
      setHashTick((n) => n + 1)
      const next = screenForHash(currentHash())
      /* A hash that maps to nothing leaves the current screen alone. Setting an
         unmapped string here is what blanked the app: it is not a Screen, so
         nothing rendered. */
      if (next) setScreen(next)
    }
    window.addEventListener("hashchange", onHash)
    return () => window.removeEventListener("hashchange", onHash)
  }, [])

  const [dark, setDark] = React.useState(
    () => new URLSearchParams(window.location.search).has("dark")
  )
  const [chatOpen, setChatOpen] = React.useState(
    () =>
      window.location.hash === "#chat" ||
      window.location.hash === "#chat-answer" ||
      window.location.hash === "#chat-admin"
  )
  const [storage, setStorage] = React.useState<StorageConfig>({
    configured: !new URLSearchParams(window.location.search).has("live"),
    engine: "postgres",
    host: "db.internal",
    database: "marites",
    schema: "marites",
    since: "09 Nov 2026",
  })
  const [ai, setAi] = React.useState({
    configured: !new URLSearchParams(window.location.search).has("noai"),
    model: "llama3.1:8b",
    sendsPayloads: false,
  })
  // a deep link may open one dialog on arrival, for demos and screenshots
  const rawLink = window.location.hash.replace("#", "")
  const deepLink = rawLink === "discard-payload" ? "discard" : rawLink
  const initialAction: "replay" | "peek" | "discard" | "purge" | null =
    deepLink === "replay" || deepLink === "peek" || deepLink === "discard" || deepLink === "purge"
      ? deepLink
      : null

  React.useEffect(() => {
    document.documentElement.classList.toggle("dark", dark)
  }, [dark])

  const role = session?.role ?? "support"
  const canUseConsole = role !== "viewer"
  const canPurge = role === "admin"

  // A role never sees a surface its capabilities cannot use (DOC-UX-001 §2).
  React.useEffect(() => {
    if (session && role === "viewer" && screen !== "report") setScreen("report")
  }, [session, role, screen])

  function signIn(email: string) {
    const r = roleFromEmail(email)
    if (!r) return
    const next: Session = { email: email.trim().toLowerCase(), role: r }
    sessionStorage.setItem("marites.demo", JSON.stringify(next))
    setSession(next)
    setScreen(r === "viewer" ? "report" : "queues")
  }

  function signOut() {
    sessionStorage.removeItem("marites.demo")
    setScreen("queues")
    setSession(null)
  }

  if (!session) return <Login onSignIn={signIn} dark={dark} onDark={setDark} />

  return (
    <div className="grid min-h-screen grid-cols-[236px_1fr] max-[900px]:grid-cols-1">
      <Sidebar
        role={role}
        screen={screen}
        onNavigate={setScreen}
        canUseConsole={canUseConsole}
        canManageBrokers={role === "admin"}
      />

      <div className="flex min-w-0 flex-col">
        <Topbar
          email={session.email}
          role={role}
          dark={dark}
          onDark={setDark}
          onSignOut={signOut}
          onOpenChat={() => setChatOpen(true)}
        />

        <main key={hashTick} className="mx-auto w-full max-w-[1180px] px-6 pb-16 pt-6">
          {screen === "queues" && (
            <QueuesScreen onOpen={() => setScreen("queue")} liveOnly={!storage.configured} />
          )}
          {screen === "queue" && (
            <QueueScreen
              canPurge={canPurge}
              onBack={() => setScreen("queues")}
              initialAction={initialAction}
              liveOnly={!storage.configured}
            />
          )}
          {screen === "publish" && <PublishScreen />}
          {screen === "audit" && <AuditScreen />}
          {screen === "storage" && (
            <StorageSettingsScreen
              config={storage}
              onEnable={(cfg) => setStorage(cfg)}
              onReplace={(cfg) => setStorage(cfg)}
            />
          )}
          {screen === "ai" && (
            <AiSettingsScreen
              onOpenChat={() => setChatOpen(true)}
              onSaved={(cfg) => setAi({ configured: true, ...cfg })}
            />
          )}
          {screen === "report" && <ReportScreen liveOnly={!storage.configured} />}
          {screen === "setup" && (
            <SetupScreen
              onDone={() => setScreen("queues")}
              onStorageEnabled={() => setStorage((s) => ({ ...s, configured: true }))}
              onSkipStorage={() => setStorage((s) => ({ ...s, configured: false }))}
            />
          )}
        </main>
      </div>

      <ChatDrawer
        open={chatOpen}
        onClose={() => setChatOpen(false)}
        onOpenQueue={() => {
          setChatOpen(false)
          setScreen("queue")
        }}
        configured={ai.configured}
        canConfigure={role === "admin"}
        onConfigure={() => {
          setChatOpen(false)
          setScreen("ai")
        }}
        model={ai.model}
        sendsPayloads={ai.sendsPayloads}
      />
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Sidebar                                                             */
/* ------------------------------------------------------------------ */

function Sidebar({
  role,
  screen,
  onNavigate,
  canUseConsole,
  canManageBrokers,
}: {
  role: Role
  screen: Screen
  onNavigate: (s: Screen) => void
  canUseConsole: boolean
  canManageBrokers: boolean
}) {
  const item = (target: Screen, icon: React.ReactNode, label: string, active: boolean) => (
    <button
      onClick={() => onNavigate(target)}
      aria-current={active ? "page" : undefined}
      className={cn(
        "flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-[13.5px] text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground",
        active && "bg-accent font-semibold text-accent-foreground hover:bg-accent"
      )}
    >
      <span className="opacity-80">{icon}</span>
      {label}
    </button>
  )

  return (
    <aside className="sticky top-0 flex h-screen flex-col gap-5 border-r bg-card px-3 py-4">
      <div className="flex items-center gap-2.5 px-2 pb-1">
        <MaritesMark size={28} />
        <div className="leading-tight">
          <div className="text-[14.5px] font-semibold tracking-[-0.02em]">Marites</div>
          <div className="text-[10.5px] text-muted-foreground">queue observability</div>
        </div>
      </div>

      {canUseConsole && (
        <nav className="flex flex-col gap-0.5" aria-label="Operations">
          <div className="px-2.5 pb-1.5 text-[10.5px] uppercase tracking-[0.09em] text-muted-foreground">
            Operations
          </div>
          {item("queues", <BarChart3 className="h-4 w-4" />, "Queues", screen === "queues" || screen === "queue")}
          {item("publish", <Send className="h-4 w-4" />, "Publish", screen === "publish")}
          {item("audit", <ScrollText className="h-4 w-4" />, "Audit", screen === "audit")}
          {canManageBrokers &&
            item("setup", <Settings2 className="h-4 w-4" />, "Setup", screen === "setup")}
          {canManageBrokers && item("ai", <Bot className="h-4 w-4" />, "AI assistant", screen === "ai")}
          {canManageBrokers &&
            item("storage", <Database className="h-4 w-4" />, "Storage", screen === "storage")}
        </nav>
      )}

      <nav className="flex flex-col gap-0.5" aria-label="Reporting">
        <div className="px-2.5 pb-1.5 text-[10.5px] uppercase tracking-[0.09em] text-muted-foreground">
          Reporting
        </div>
        {item("report", <Activity className="h-4 w-4" />, "Volume & failures", screen === "report")}
      </nav>

      <div className="mt-3 border-t px-2.5 pt-3 text-[11px] leading-relaxed text-muted-foreground">
        Prototype · not authoritative
        <div className="opacity-75">
          {role === "viewer"
            ? "Viewer sees the reporting surface only"
            : role === "support"
              ? "Support: no purge, no setup"
              : "Admin: every capability"}
        </div>
      </div>
    </aside>
  )
}

/* ------------------------------------------------------------------ */
/* Topbar                                                              */
/* ------------------------------------------------------------------ */

function Topbar({
  email,
  role,
  dark,
  onDark,
  onSignOut,
  onOpenChat,
}: {
  email: string
  role: Role
  dark: boolean
  onDark: (v: boolean) => void
  onSignOut: () => void
  onOpenChat: () => void
}) {
  const label = ACCOUNTS.find((a) => a.email === email)?.label ?? role
  return (
    <header className="sticky top-0 z-20 flex items-center gap-2.5 border-b bg-card/85 px-6 py-3 backdrop-blur">
      <button className="flex items-center gap-2 rounded-md border bg-card px-2.5 py-1.5 text-[13px] font-medium shadow-sm transition-colors hover:bg-secondary">
        <span className="grid h-4 w-4 place-items-center rounded-[4px] bg-secondary">
          <span className="h-1.5 w-1.5 rounded-[1px] bg-muted-foreground" />
        </span>
        prod-cluster
        <span className="rounded-[5px] bg-destructive/10 px-1.5 py-0.5 text-[10.5px] font-bold uppercase tracking-[0.06em] text-destructive">
          prod
        </span>
        <ChevronDown className="h-3 w-3 opacity-50" />
      </button>

      <div className="flex-1" />

      {role !== "viewer" && (
        <Button variant="outline" size="sm" onClick={onOpenChat} className="gap-1.5">
          <Bot className="h-3.5 w-3.5" />
          Ask Marites
        </Button>
      )}

      <Button variant="outline" size="icon" onClick={() => onDark(!dark)} aria-label="Toggle theme">
        {dark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
      </Button>

      <div className="flex items-center gap-2 rounded-full border bg-card py-1 pl-2.5 pr-1 shadow-sm">
        <div className="hidden text-right leading-tight sm:block">
          <div className="text-[11.5px] font-medium">{email}</div>
          <div className="text-[10px] uppercase tracking-[0.06em] text-muted-foreground">{label}</div>
        </div>
        <div className="grid h-7 w-7 place-items-center rounded-full bg-secondary text-[11px] font-bold uppercase text-secondary-foreground">
          {role[0]}
        </div>
        <Button
          variant="ghost"
          size="icon"
          onClick={onSignOut}
          aria-label={`Sign out ${email}`}
          title="Sign out"
          className="h-7 w-7"
        >
          <LogOut className="h-3.5 w-3.5" />
        </Button>
      </div>
    </header>
  )
}

/* ------------------------------------------------------------------ */
/* Queues                                                              */
/* ------------------------------------------------------------------ */

function QueuesScreen({ onOpen, liveOnly }: { onOpen: () => void; liveOnly?: boolean }) {
  /* The two banners are summaries of the rows below them, so they must be able
     to show those rows. A summary that names a count and cannot point at the
     two queues it counted is a dead end. */
  const [filter, setFilter] = React.useState<null | "dlq" | "capture">(() => {
    const h = window.location.hash
    return h === "#queues-dlq" ? "dlq" : h === "#queues-capture" ? "capture" : null
  })
  const noDeadLetter = QUEUES.filter((q) => q.dead === null)
  const degraded = QUEUES.filter((q) => q.state !== "live")
  const stale = QUEUES.filter((q) => q.state === "stale")
  const capped = QUEUES.filter((q) => q.state === "capped")
  const rows = filter === "dlq" ? noDeadLetter : filter === "capture" ? degraded : QUEUES
  const toggle = (f: "dlq" | "capture") => setFilter((cur) => (cur === f ? null : f))
  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-5 flex items-end gap-4">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.025em]">Queues</h1>
          <p className="mt-0.5 text-[13px] text-muted-foreground">
            prod-cluster · /prod · 5 queues watched · polled 6 seconds ago
          </p>
        </div>
        <div className="flex-1" />
        <Button variant="outline">
          <RefreshCw className="h-3.5 w-3.5" /> Refresh
        </Button>
      </div>

      {liveOnly && (
        <div className="mb-4 flex flex-wrap items-center gap-2.5 rounded-lg border border-warning/30 bg-warning/[0.08] px-3.5 py-2.5 text-[13px] text-warning">
          <Database className="h-4 w-4 shrink-0" />
          <span>
            <b>Live only.</b> No message history and no counts over time until a database is
            connected. Recovery and the audit keep working.
          </span>
          <span className="ml-auto text-[11.5px] opacity-80">Admin → Storage</span>
        </div>
      )}

      <div
        className={cn(
          "mb-4 flex items-center gap-2.5 rounded-lg border border-warning/25 bg-warning/[0.07] px-3.5 py-2.5 text-[13px] text-warning",
          liveOnly && "hidden"
        )}
      >
        <AlertTriangle className="h-4 w-4 shrink-0" />
        <span>
          {noDeadLetter.length} queues cannot report failures — no dead-letter routing configured.{" "}
          <button
            onClick={() => toggle("dlq")}
            className="font-semibold underline underline-offset-2"
          >
            {filter === "dlq" ? "Show all" : "View"}
          </button>
        </span>
      </div>

      <div
        className={cn(
          "mb-4 flex items-center gap-2.5 rounded-lg border border-warning/25 bg-warning/[0.07] px-3.5 py-2.5 text-[13px] text-warning",
          liveOnly && "hidden"
        )}
      >
        <AlertTriangle className="h-4 w-4 shrink-0" />
        <span>
          {stale.length} queue&apos;s capture has stopped — its figures are frozen.{" "}
          {capped.length} is capped — it keeps counting, but stores no new messages.{" "}
          <button
            onClick={() => toggle("capture")}
            className="font-semibold underline underline-offset-2"
          >
            {filter === "capture" ? "Show all" : "View"}
          </button>
        </span>
      </div>

      {filter && (
        <div className="mb-3 flex items-center gap-2 text-[12px] text-muted-foreground">
          <span>
            Showing {rows.length} of {QUEUES.length} —{" "}
            {filter === "dlq" ? "no dead-letter routing" : "capture needs attention"}
          </span>
          <button
            onClick={() => setFilter(null)}
            className="rounded border px-1.5 py-0.5 text-[11px] font-medium hover:bg-secondary"
          >
            Clear
          </button>
        </div>
      )}

      <Card className="overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>Queue</TableHead>
              <TableHead className="text-right">Depth</TableHead>
              <TableHead className="text-right">Rate /s</TableHead>
              <TableHead className="text-right">Dead letters</TableHead>
              <TableHead>Capture</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((q) => (
              <TableRow
                key={q.name}
                tabIndex={0}
                onClick={q.name === "orders" ? onOpen : undefined}
                onKeyDown={(e) => {
                  if (q.name === "orders" && (e.key === "Enter" || e.key === " ")) onOpen()
                }}
                className={cn(q.name === "orders" && "cursor-pointer")}
              >
                <TableCell>
                  <div className="flex items-center gap-2">
                    <span className="font-medium tracking-[-0.01em]">{q.name}</span>
                    {q.state === "stale" && !liveOnly && (
                      <span className="text-[11px] text-warning">capture stopped</span>
                    )}
                  </div>
                </TableCell>
                <TableCell className="num text-right">{nf.format(q.depth)}</TableCell>
                <TableCell className="num text-right">{nf.format(q.rate)}</TableCell>
                <TableCell className="num text-right">
                  {q.dead === null ? (
                    <Status state="unavailable" />
                  ) : (
                    <>
                      {nf.format(q.dead)}
                      {q.deadRate && (
                        <span className="ml-1.5 text-[11.5px] text-muted-foreground">· {q.deadRate}</span>
                      )}
                    </>
                  )}
                </TableCell>
                <TableCell>
                  {liveOnly ? (
                    <span className="text-[11.5px] text-muted-foreground">off</span>
                  ) : (
                    <Status state={q.state} />
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {rows.length === 0 && (
          <p className="px-4 py-10 text-center text-[12.5px] text-muted-foreground">
            No queue matches this filter.
          </p>
        )}
      </Card>

      <p className="mt-3.5 text-[12px] text-muted-foreground">
        {liveOnly ? (
          <>
            <b className="text-foreground/80">off</b> means capture is not running, because there is
            no message store to write to. Depth and rate still come from the broker, so the console
            works — there is simply nothing recorded to look back at.
          </>
        ) : (
          <>
            <b className="text-foreground/80">stale</b> means depth is growing while capture has
            stopped advancing. <b className="text-foreground/80">capped</b> means rows paused at the
            storage ceiling — counts continue. Neither is ever rendered as zero.
          </>
        )}
      </p>

      <div className="mt-6 flex items-center gap-3 border-t pt-3 text-[11.5px] text-muted-foreground">
        <span className="num">4,200 msg/s</span>
        <span aria-hidden="true">·</span>
        <span>poll interval 10 s</span>
      </div>
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Queue detail                                                        */
/* ------------------------------------------------------------------ */

type DLMessage = {
  id: string
  time: string
  reason: "rejected" | "expired"
  routingKey: string
  correlationId: string
  size: string
  group?: number
  payload: string
}

/** Payloads are stored exactly as published (DEC-006) — no masking, no reformatting. */
const j = (o: unknown) => JSON.stringify(o, null, 2)

const MESSAGES: DLMessage[] = [
  {
    id: "m1", time: "14:21:57", reason: "rejected", routingKey: "order.created",
    correlationId: "a3f1c8e2", size: "1.2 KB", group: 400,
    payload: j({
      orderId: "ORD-88213",
      customerId: "CUS-4471",
      currency: "PHP",
      total: 2499.0,
      items: [
        { sku: "SKU-119", qty: 2, price: 999.5 },
        { sku: "SKU-204", qty: 1, price: 500.0 },
      ],
      createdAt: "2026-11-09T14:21:57Z",
      attempt: 3,
    }),
  },
  {
    id: "m2", time: "14:21:44", reason: "expired", routingKey: "order.created",
    correlationId: "b7c2d901", size: "0.9 KB",
    payload: j({
      orderId: "ORD-88204",
      customerId: "CUS-1180",
      currency: "PHP",
      total: 899.0,
      items: [{ sku: "SKU-330", qty: 1, price: 899.0 }],
      createdAt: "2026-11-09T13:58:02Z",
      attempt: 1,
    }),
  },
  {
    id: "m3", time: "14:21:39", reason: "rejected", routingKey: "order.updated",
    correlationId: "c91e4f77", size: "1.4 KB",
    payload: j({
      orderId: "ORD-88197",
      status: "shipped",
      carrier: "LBC",
      trackingNumber: "LBC-7741209",
      updatedAt: "2026-11-09T14:21:39Z",
    }),
  },
  {
    id: "m4", time: "14:21:31", reason: "rejected", routingKey: "order.created",
    correlationId: "d4a0b3c5", size: "1.1 KB",
    payload: j({
      orderId: "ORD-88190",
      customerId: "CUS-2277",
      currency: "PHP",
      total: 1299.0,
      items: [{ sku: "SKU-119", qty: 1, price: 1299.0 }],
      createdAt: "2026-11-09T14:21:31Z",
      attempt: 3,
    }),
  },
  {
    id: "m5", time: "14:21:22", reason: "rejected", routingKey: "payment.captured",
    correlationId: "e2f7a1b9", size: "2.3 KB",
    payload: j({
      paymentId: "PAY-5591",
      orderId: "ORD-88172",
      amount: 2499.0,
      currency: "PHP",
      method: "card",
      capturedAt: "2026-11-09T14:21:22Z",
      provider: "stripe",
    }),
  },
  {
    id: "m6", time: "14:21:14", reason: "expired", routingKey: "order.created",
    correlationId: "f8b3c2a4", size: "0.8 KB",
    payload: j({
      orderId: "ORD-88166",
      customerId: "CUS-9034",
      currency: "PHP",
      total: 450.0,
      items: [{ sku: "SKU-204", qty: 1, price: 450.0 }],
      createdAt: "2026-11-09T13:44:10Z",
      attempt: 1,
    }),
  },
  {
    id: "m7", time: "14:21:02", reason: "rejected", routingKey: "order.updated",
    correlationId: "0a1b2c3d", size: "1.3 KB",
    payload: j({
      orderId: "ORD-88150",
      status: "cancelled",
      reason: "payment_declined",
      updatedAt: "2026-11-09T14:21:02Z",
    }),
  },
  {
    id: "m8", time: "14:20:55", reason: "rejected", routingKey: "order.created",
    correlationId: "9e8d7c6b", size: "1.0 KB",
    payload: j({
      orderId: "ORD-88144",
      customerId: "CUS-6612",
      currency: "PHP",
      total: 1799.0,
      items: [{ sku: "SKU-330", qty: 2, price: 899.5 }],
      createdAt: "2026-11-09T14:20:55Z",
      attempt: 3,
    }),
  },
]

/** What an agent proposed. It cannot act; it can only suggest, and a human approves. */
const PROPOSAL = {
  ids: ["m3", "m4", "m7"],
  rationale:
    "All three were rejected by the same consumer, which was redeployed at 14:31. They should succeed now.",
  by: "a.reyes@company.com",
  at: "2 min ago",
}

const PEEKED = [
  { time: "14:22:08", routingKey: "order.created", size: "1.2 KB", redelivered: false },
  { time: "14:22:06", routingKey: "order.created", size: "0.9 KB", redelivered: true },
  { time: "14:22:03", routingKey: "order.updated", size: "1.4 KB", redelivered: false },
  { time: "14:21:58", routingKey: "payment.captured", size: "2.1 KB", redelivered: false },
  { time: "14:21:52", routingKey: "order.created", size: "1.1 KB", redelivered: true },
]

function QueueScreen({
  canPurge,
  onBack,
  initialAction,
  liveOnly,
}: {
  canPurge: boolean
  onBack: () => void
  initialAction: "replay" | "peek" | "discard" | "purge" | null
  liveOnly?: boolean
}) {
  const preselected =
    initialAction === "discard"
      ? new Set(MESSAGES.map((m) => m.id))
      : initialAction === "replay"
        ? new Set([MESSAGES[0].id])
        : new Set<string>()
  const [selected, setSelected] = React.useState<Set<string>>(preselected)
  const [peekOpen, setPeekOpen] = React.useState(initialAction === "peek")
  const [replayOpen, setReplayOpen] = React.useState(initialAction === "replay")
  const [discardOpen, setDiscardOpen] = React.useState(initialAction === "discard")
  const [purgeOpen, setPurgeOpen] = React.useState(initialAction === "purge")
  const [proposalOpen, setProposalOpen] = React.useState(true)
  const [fromAgent, setFromAgent] = React.useState(false)
  const [preview, setPreview] = React.useState<PayloadView | null>(() =>
    window.location.hash === "#discard-payload"
      ? {
          action: "discard",
          queue: "orders",
          messageId: MESSAGES[0].id,
          size: MESSAGES[0].size,
          before: MESSAGES[0].payload,
          preview: true,
        }
      : null
  )

  const chosen = MESSAGES.filter((m) => selected.has(m.id))
  const allOnPage = chosen.length === MESSAGES.length
  const someOnPage = chosen.length > 0 && !allOnPage

  const rejected = chosen.filter((m) => m.reason === "rejected").length
  const expired = chosen.filter((m) => m.reason === "expired").length

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function toggleAll() {
    setSelected(allOnPage ? new Set() : new Set(MESSAGES.map((m) => m.id)))
  }

  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-2.5 flex items-center gap-2 text-[12.5px] text-muted-foreground">
        <button onClick={onBack} className="inline-flex items-center gap-1 hover:text-foreground">
          <ArrowLeft className="h-3.5 w-3.5" /> Queues
        </button>
        <span>/</span>
        <span>orders</span>
        <div className="flex-1" />
        <Status state="live" />
      </div>

      <div className="mb-3.5">
        <h1 className="text-[22px] font-semibold tracking-[-0.025em]">orders</h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          prod-cluster · /prod · 6 consumers · last message 14:22:08
        </p>
      </div>

      {liveOnly && (
        <div className="mb-4 flex flex-wrap items-center gap-2.5 rounded-lg border border-warning/30 bg-warning/[0.08] px-3.5 py-2.5 text-[13px] text-warning">
          <Database className="h-4 w-4 shrink-0" />
          <span>
            <b>Live only.</b> Replay, discard and purge still work, and every one of them is audited
            in Marites' own store. What is missing is history: no message list, no search, no counts
            until a database is connected.
          </span>
          <span className="ml-auto text-[11.5px] opacity-80">Admin → Storage</span>
        </div>
      )}

      {proposalOpen && (
        <div className="mb-4 flex flex-wrap items-start gap-3 rounded-lg border border-primary/25 bg-accent/45 px-3.5 py-3">
          <span className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-full bg-primary/12">
            <Bot className="h-3.5 w-3.5 text-primary" />
          </span>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2 text-[13px] font-semibold">
              Claude replayed 3 messages
              <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-[0.05em] text-primary">
                via assistant
              </span>
            </div>
            <p className="mt-0.5 text-[12px] leading-relaxed text-muted-foreground">
              “{PROPOSAL.rationale}”
              <span className="ml-1 opacity-70">
                · {PROPOSAL.at} as {PROPOSAL.by} · recorded in the audit
              </span>
            </p>
          </div>
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={() => setSelected(new Set(PROPOSAL.ids))}
            >
              Show 3
            </Button>
          </div>
        </div>
      )}

      <div className="mb-5 grid grid-cols-4 gap-px overflow-hidden rounded-lg border bg-border max-[900px]:grid-cols-2">
        {[
          { k: "Depth", v: "12,481" },
          { k: "Publish / deliver", v: "4,200 / 4,180" },
          { k: "Dead letters", v: "1,204" },
          { k: "Dead-letter rate", v: "28.6%" },
        ].map((m) => (
          <div key={m.k} className="bg-card px-4 py-3.5">
            <div className="text-[11px] uppercase tracking-[0.07em] text-muted-foreground">{m.k}</div>
            <div className="num mt-1 text-[24px] font-semibold tracking-[-0.03em]">{m.v}</div>
          </div>
        ))}
      </div>

      <div className="mb-5 grid grid-cols-[1.55fr_1fr] items-start gap-4 max-[900px]:grid-cols-1">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Capture policy</CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 text-[13px]">
              {(liveOnly
                ? [
                    ["Capture mode", "off — no store"],
                    ["Payload", "not kept"],
                    ["Row retention", "—"],
                    ["Storage", "not connected"],
                    ["Dead-letter routing", "configured"],
                  ]
                : [
                    ["Capture mode", "rows"],
                    ["Payload", "sampled · 1 in 100"],
                    ["Row retention", "72 hours"],
                    ["Storage", "18.4 / 50 GB"],
                    ["Dead-letter routing", "configured"],
                  ]
              ).map(([k, v]) => (
                <React.Fragment key={k}>
                  <dt className="text-muted-foreground">{k}</dt>
                  <dd className="text-right font-medium">{v}</dd>
                </React.Fragment>
              ))}
            </dl>
            <p className="mt-4 border-t pt-3.5 text-[12px] leading-relaxed text-muted-foreground">
              <b className="text-foreground/80">Capture mode</b> decides whether a row exists at all.
              Set to <span className="font-mono text-[11.5px]">counts</span>, this queue keeps exact
              figures and stores no message — the setting for an adopter who cannot let a vendor keep
              their traffic.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Failure reasons</CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 text-[13px]">
              {[
                ["rejected", "1,150"],
                ["expired", "54"],
                ["maxlen", "0"],
              ].map(([k, v]) => (
                <React.Fragment key={k}>
                  <dt className="font-mono text-[12.5px] text-muted-foreground">{k}</dt>
                  <dd className="num text-right font-medium">{v}</dd>
                </React.Fragment>
              ))}
            </dl>
            <p className="mt-4 border-t pt-3.5 text-[12px] leading-relaxed text-muted-foreground">
              The same count is a different incident depending on the reason. A consumer failing is
              not a queue hitting its length limit.
            </p>
          </CardContent>
        </Card>
      </div>

      <Card className="overflow-hidden">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b px-4 py-2.5">
          <div className="flex items-baseline gap-2">
            <span className="text-[12px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
              Dead-letter messages
            </span>
            <span className="text-[11.5px] text-muted-foreground">{liveOnly ? "0 captured of 1,204 on the queue" : "showing 1–8 of 1,204"}</span>
            {chosen.length > 0 ? (
              <span className="text-[11.5px] font-medium text-foreground">
                · {chosen.length} selected
              </span>
            ) : (
              <span className="text-[11.5px] text-muted-foreground/70">· select rows to act</span>
            )}
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => setPeekOpen(true)}>
              Peek
            </Button>
            <Button
              size="sm"
              onClick={() => setReplayOpen(true)}
              disabled={chosen.length === 0}
            >
              <RotateCcw className="h-3.5 w-3.5" />
              {chosen.length === 0 ? "Replay selected" : `Replay ${chosen.length}`}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setDiscardOpen(true)}
              disabled={chosen.length === 0}
            >
              {chosen.length === 0 ? "Discard selected" : `Discard ${chosen.length}`}
            </Button>
            {canPurge && (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setPurgeOpen(true)}
              >
                <ShieldAlert className="h-3.5 w-3.5" /> Purge queue
              </Button>
            )}
          </div>
        </div>
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className="w-10">
                <Checkbox
                  checked={allOnPage ? true : someOnPage ? "indeterminate" : false}
                  onCheckedChange={toggleAll}
                  aria-label="Select this page"
                />
              </TableHead>
              <TableHead>Time</TableHead>
              <TableHead>Reason</TableHead>
              <TableHead>Routing key</TableHead>
              <TableHead>Correlation</TableHead>
              <TableHead className="text-right">Size</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {liveOnly && (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={6} className="px-4 py-10 text-center">
                  <p className="text-[13px] font-medium">No message history</p>
                  <p className="mx-auto mt-1 max-w-[420px] text-[11.5px] leading-relaxed text-muted-foreground">
                    Nothing is being captured, so there are no messages to list, search or replay
                    from here. Peek still shows what is live on the queue, and connecting a database
                    in Admin → Storage starts history from that moment.
                  </p>
                </TableCell>
              </TableRow>
            )}
            {!liveOnly &&
              MESSAGES.map((m) => {
              const on = selected.has(m.id)
              return (
                <TableRow
                  key={m.id}
                  data-state={on ? "selected" : undefined}
                  onClick={() => toggle(m.id)}
                  className={cn("cursor-pointer", on && "bg-accent/50 hover:bg-accent/60")}
                >
                  <TableCell onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      checked={on}
                      onCheckedChange={() => toggle(m.id)}
                      aria-label={`Select message at ${m.time}`}
                    />
                  </TableCell>
                  <TableCell className="num text-muted-foreground">{m.time}</TableCell>
                  <TableCell>
                    <Badge variant={m.reason === "rejected" ? "warning" : "idle"}>{m.reason}</Badge>
                  </TableCell>
                  <TableCell className="font-mono text-[12px]">{m.routingKey}</TableCell>
                  <TableCell className="font-mono text-[12px] text-muted-foreground">
                    {m.correlationId}
                    {m.group && (
                      <span className="ml-2 rounded bg-secondary px-1.5 py-0.5 text-[10.5px] text-muted-foreground">
                        ×{m.group} same hash
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="num text-right text-muted-foreground">{m.size}</TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
        <div className="border-t px-4 py-2.5 text-[11.5px] leading-relaxed text-muted-foreground">
          {liveOnly && (
            <>
              <b className="text-foreground/80">This install is live-only</b> — there is no message
              history to search, but every action you take here is still audited.{" "}
            </>
          )}
          {!canPurge && (
            <>
              <b className="text-foreground/80">Purge is Admin-only</b>, so it is absent here rather
              than disabled.{" "}
            </>
          )}
          Duplicate grouping is the view that matters when a DLQ floods: ×400 same hash is one
          producer bug, not 400 separate incidents.
        </div>
      </Card>

      <PeekDialog open={peekOpen} onOpenChange={setPeekOpen} />
      <ReplayDialog
        open={replayOpen}
        onOpenChange={(v) => {
          setReplayOpen(v)
          if (!v) setFromAgent(false)
        }}
        messages={chosen}
        proposal={fromAgent ? { rationale: PROPOSAL.rationale, by: PROPOSAL.by } : undefined}
      />

      <ConfirmDialog
        open={discardOpen}
        onOpenChange={setDiscardOpen}
        title={`Discard ${chosen.length} message${chosen.length === 1 ? "" : "s"}?`}
        description="Each body is written to the audit before the message is acknowledged. If the record cannot be written, nothing is discarded."
        fields={[{ label: "Type the queue name to confirm", placeholder: "orders", expected: "orders" }]}
        actionLabel={`Discard ${chosen.length}`}
        onConfirm={() => {
          setSelected(new Set())
          setDiscardOpen(false)
        }}
      >
        <div>
          <div className="mb-1.5 flex items-baseline justify-between">
            <Label>What will be discarded</Label>
            <span className="text-[11px] text-muted-foreground">
              {chosen.length} {chosen.length === 1 ? "body" : "bodies"} · open any one
            </span>
          </div>
          <div className="max-h-[168px] space-y-1 overflow-auto">
            {chosen.map((m) => (
              <button
                key={m.id}
                type="button"
                onClick={() =>
                  setPreview({
                    action: "discard",
                    queue: "orders",
                    messageId: m.id,
                    size: m.size,
                    before: m.payload,
                    preview: true,
                  })
                }
                aria-label={`Open the payload for ${m.id}`}
                className="flex w-full items-baseline gap-2 rounded border bg-card px-2.5 py-1.5 text-left hover:border-muted-foreground/30 hover:bg-secondary/60"
              >
                <span className="w-[92px] shrink-0 font-mono text-[11px]">{m.id}</span>
                <span className="w-[52px] shrink-0 text-[11px] text-muted-foreground">{m.size}</span>
                <span className="min-w-0 flex-1 truncate font-mono text-[11px] text-muted-foreground">
                  {m.payload.replace(/\s+/g, " ")}
                </span>
                <Maximize2 className="h-3 w-3 shrink-0 self-center text-muted-foreground/60" />
              </button>
            ))}
          </div>
        </div>
      </ConfirmDialog>

      <ConfirmDialog
        open={purgeOpen}
        onOpenChange={setPurgeOpen}
        title="Purge queue — orders"
        description="Every message in the queue is removed from the broker. This cannot be undone, and there is no scheduled purge anywhere in the product."
        fields={[
          { label: "Type the queue name", placeholder: "orders", expected: "orders" },
          { label: "Type the environment", placeholder: "prod", expected: "prod" },
        ]}
        actionLabel="Purge queue"
        onConfirm={() => setPurgeOpen(false)}
      />

      <PayloadDialog view={preview} onClose={() => setPreview(null)} />
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Peek                                                                */
/* ------------------------------------------------------------------ */

function PeekDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (v: boolean) => void }) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Peek — orders</DialogTitle>
          <DialogDescription className="flex items-center gap-2 pt-1">
            <Badge variant="destructive" className="px-1.5 py-0">prod</Badge>
            <span>/prod · 5 of 12,481 · nothing is consumed</span>
          </DialogDescription>
        </DialogHeader>

        <div className="px-5 py-4">
          <div className="mb-3 flex gap-2.5 rounded-lg border border-warning/25 bg-warning/[0.07] p-3 text-[12.5px] leading-relaxed text-warning">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <div>
              On RabbitMQ this is a <b>get-and-requeue</b>: each message is briefly removed and put
              back, and its <code className="font-mono">redelivered</code> flag is set. It is not
              free — the response says <code className="font-mono">destructive: true</code> for this
              provider.
            </div>
          </div>

          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>Time</TableHead>
                  <TableHead>Routing key</TableHead>
                  <TableHead className="text-right">Size</TableHead>
                  <TableHead>Redelivered</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {PEEKED.map((p) => (
                  <TableRow key={p.time}>
                    <TableCell className="num text-muted-foreground">{p.time}</TableCell>
                    <TableCell className="font-mono text-[12px]">{p.routingKey}</TableCell>
                    <TableCell className="num text-right text-muted-foreground">{p.size}</TableCell>
                    <TableCell>
                      {p.redelivered ? (
                        <span className="text-[11.5px] text-warning">yes</span>
                      ) : (
                        <span className="text-[11.5px] text-muted-foreground">no</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <p className="mt-3 text-[11.5px] leading-relaxed text-muted-foreground">
            Peek is rate-limited per queue and never runs on a timer. Use it to look at what is
            waiting now; use the dead-letter list to act on what already failed.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/* ------------------------------------------------------------------ */
/* Typed confirmation                                                  */
/* ------------------------------------------------------------------ */

function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  fields,
  actionLabel,
  onConfirm,
  children,
}: {
  open: boolean
  onOpenChange: (v: boolean) => void
  title: string
  description: string
  fields: { label: string; placeholder: string; expected: string }[]
  actionLabel: string
  onConfirm: () => void
  /** What is about to be affected — shown before the confirmation, not after. */
  children?: React.ReactNode
}) {
  const [values, setValues] = React.useState<string[]>(() => fields.map(() => ""))

  React.useEffect(() => {
    if (open) setValues(fields.map(() => ""))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  const ready = fields.every((f, i) => (values[i] ?? "").trim() === f.expected)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription className="pt-1">{description}</DialogDescription>
        </DialogHeader>

        <div className="min-w-0 space-y-3 px-5 py-4">
          {children}
          {fields.map((f, i) => (
            <div key={f.label}>
              <Label htmlFor={`confirm-${i}`}>{f.label}</Label>
              <Input
                id={`confirm-${i}`}
                value={values[i] ?? ""}
                onChange={(e) =>
                  setValues((prev) => prev.map((v, j) => (j === i ? e.target.value : v)))
                }
                placeholder={f.placeholder}
                autoComplete="off"
                spellCheck={false}
                className="mt-1.5"
              />
            </div>
          ))}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button variant="destructive" disabled={!ready} onClick={onConfirm}>
            {actionLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/* ------------------------------------------------------------------ */
/* Publish                                                             */
/* ------------------------------------------------------------------ */

const TEMPLATE = JSON.stringify(
  {
    orderId: "ORD-88301",
    customerId: "CUS-4471",
    currency: "PHP",
    total: 1299.0,
    items: [{ sku: "SKU-119", qty: 1, price: 1299.0 }],
    createdAt: "2026-11-09T16:02:00Z",
  },
  null,
  2
)

/** A display-only checksum, so the audit note has something concrete to point at. */
function shortHash(s: string) {
  let h = 0x811c9dc5
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i)
    h = Math.imul(h, 0x01000193)
  }
  return (h >>> 0).toString(16).padStart(8, "0")
}

function PublishScreen() {
  const [exchange, setExchange] = React.useState("orders")
  const [routingKey, setRoutingKey] = React.useState("order.created")
  const [contentType, setContentType] = React.useState("application/json")
  const [body, setBody] = React.useState(TEMPLATE)
  const [confirm, setConfirm] = React.useState("")
  const [published, setPublished] = React.useState<{ id: string; hash: string } | null>(null)

  const idempotencyKey = React.useMemo(() => {
    try {
      return crypto.randomUUID()
    } catch {
      return "9f2c1a44-0b7e-4d21-9c88-3a5f6e7d1b02"
    }
  }, [published])

  const isJson = contentType === "application/json"
  let parseError: string | null = null
  if (isJson) {
    try {
      JSON.parse(body)
    } catch (e) {
      parseError = e instanceof Error ? e.message : "Invalid JSON"
    }
  }

  const bytes = new TextEncoder().encode(body).length
  const hash = shortHash(body)
  const ready = routingKey.trim().length > 0 && !parseError && confirm.trim() === "prod"

  function send() {
    setPublished({ id: idempotencyKey, hash })
    setConfirm("")
  }

  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-5">
        <h1 className="text-[22px] font-semibold tracking-[-0.025em]">Publish a message</h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          Injects a new message into a live business flow — the highest-risk action in the product.
          Nothing is sent until the body parses and the environment is typed.
        </p>
      </div>

      {published && (
        <div className="mb-4 flex items-start gap-2.5 rounded-lg border border-success/30 bg-success/[0.07] px-3.5 py-3 text-[12.5px] text-success">
          <span className="mt-0.5 grid h-4 w-4 place-items-center rounded-full bg-success/15 text-[10px] font-bold">
            ✓
          </span>
          <div className="flex-1">
            <b>Published to {exchange} / {routingKey}.</b> One audit row records the actor, the target
            and the payload hash <span className="font-mono">{published.hash}</span>. A retry with the
            same idempotency key returns the recorded outcome instead of sending twice.
            <button
              onClick={() => setPublished(null)}
              className="ml-2 font-semibold underline underline-offset-2"
            >
              Compose another
            </button>
          </div>
        </div>
      )}

      <div className="grid grid-cols-[1.35fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Message</CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            <div className="mb-3 grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="exchange">Exchange</Label>
                <select
                  id="exchange"
                  value={exchange}
                  onChange={(e) => setExchange(e.target.value)}
                  className="mt-1.5 h-9 w-full rounded-md border border-input bg-secondary/60 px-2.5 font-mono text-[12.5px] text-foreground shadow-sm focus-visible:border-ring focus-visible:bg-card focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20"
                >
                  {["orders", "payments", "events"].map((x) => (
                    <option key={x} value={x}>{x}</option>
                  ))}
                </select>
              </div>
              <div>
                <Label htmlFor="routing-key">Routing key</Label>
                <Input
                  id="routing-key"
                  value={routingKey}
                  onChange={(e) => setRoutingKey(e.target.value)}
                  className="mt-1.5 font-mono text-[12.5px]"
                  placeholder="order.created"
                />
              </div>
            </div>

            <div className="mb-1.5 flex items-center gap-2">
              <Label htmlFor="payload">Payload</Label>
              <select
                aria-label="Content type"
                value={contentType}
                onChange={(e) => setContentType(e.target.value)}
                className="h-6 rounded border border-input bg-secondary/60 px-1.5 text-[10.5px] uppercase tracking-[0.06em] text-muted-foreground"
              >
                <option value="application/json">json</option>
                <option value="text/plain">text</option>
              </select>
              <div className="ml-auto flex gap-1">
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-[11.5px]"
                  onClick={() => {
                    try {
                      setBody(JSON.stringify(JSON.parse(body), null, 2))
                    } catch {
                      /* the validity line already explains why */
                    }
                  }}
                  disabled={!isJson}
                >
                  Format
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-[11.5px]"
                  onClick={() => setBody(TEMPLATE)}
                >
                  Template
                </Button>
              </div>
            </div>

            <textarea
              id="payload"
              value={body}
              onChange={(e) => setBody(e.target.value)}
              spellCheck={false}
              aria-invalid={Boolean(parseError)}
              className={cn(
                "h-[320px] w-full resize-y rounded-md border bg-secondary/50 p-3 font-mono text-[12px] leading-relaxed text-foreground shadow-sm",
                "focus-visible:border-ring focus-visible:bg-card focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20",
                parseError ? "border-destructive/60" : "border-input"
              )}
            />

            <div className="mt-1.5 text-[11.5px]">
              {parseError ? (
                <span className="text-destructive">Invalid JSON — {parseError}</span>
              ) : (
                <span className="text-muted-foreground">
                  {isJson ? "Valid JSON" : "Text body"} · {body.split("\n").length} lines · {bytes} bytes
                </span>
              )}
            </div>
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle>Target</CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 text-[13px]">
                <dt className="text-muted-foreground">Exchange</dt>
                <dd className="text-right font-mono text-[12.5px]">{exchange || "—"}</dd>
                <dt className="text-muted-foreground">Routing key</dt>
                <dd className="text-right font-mono text-[12.5px]">{routingKey || "—"}</dd>
                <dt className="text-muted-foreground">Environment</dt>
                <dd className="text-right">
                  <Badge variant="destructive" className="px-1.5 py-0">prod</Badge>
                </dd>
                <dt className="text-muted-foreground">Payload hash</dt>
                <dd className="text-right font-mono text-[12.5px] text-muted-foreground">{hash}</dd>
              </dl>

              <div className="mt-3.5 space-y-2 border-t pt-3.5 text-[11.5px] leading-relaxed text-muted-foreground">
                <div className="flex justify-between gap-3">
                  <span>Idempotency key</span>
                  <span className="truncate font-mono text-[11px] text-foreground/80">
                    {idempotencyKey.slice(0, 18)}…
                  </span>
                </div>
                <p>
                  Generated per message, so a double-click cannot produce two messages. A retry with
                  the same key returns <span className="font-mono">AlreadyApplied</span>.
                </p>
                <p>
                  Every publish writes an audit row with the actor, the target and the hash above —
                  never the body itself.
                </p>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-4">
              <div className="mb-3.5 flex gap-2 rounded-md border border-warning/25 bg-warning/[0.07] px-2.5 py-2 text-[11.5px] leading-relaxed text-warning">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <div>
                  Valid JSON is not a valid message. If the consumer rejects it, it lands in the
                  dead-letter queue — which is where you started.
                </div>
              </div>
              <Label htmlFor="publish-confirm">
                Type <b className="font-mono text-foreground">prod</b> to confirm the target
              </Label>
              <Input
                id="publish-confirm"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                placeholder="prod"
                autoComplete="off"
                spellCheck={false}
                className="mt-1.5"
              />
              <Button className="mt-3 w-full" disabled={!ready} onClick={send}>
                <Send className="h-3.5 w-3.5" /> Publish message
              </Button>
              <p className="mt-2 text-[11.5px] leading-relaxed text-muted-foreground">
                {parseError
                  ? "The body does not parse, so publish stays disabled."
                  : confirm.trim() !== "prod"
                    ? "The environment tag is the check that this is not staging."
                    : "Ready. Publishing injects this message into the exchange immediately."}
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Audit                                                               */
/* ------------------------------------------------------------------ */

type AuditPayload = {
  id: string
  size: string
  before: string
  after?: string
  /** A one-line preview for the list; `before` is always the complete body. */
  preview?: string
}

type AuditRow = {
  id: string
  time: string
  actor:
    | { kind: "human"; name: string; role: Role }
    | { kind: "agent"; name: string; behalf: string }
  action: string
  target: string
  /** Set when a human action arrived through the assistant rather than the UI. */
  via?: string
  result: string
  tone: "ok" | "warn" | "idle"
  count: string
  payload?: {
    count: number
    retainedUntil: string
    hash: string
    /** One entry per message the operation touched — never a sample. */
    bodies: AuditPayload[]
    /** The retention that applied at write time, in words — shown when it lapses. */
    retention?: string
    /** Set only when the operation exceeded MARITES_EVIDENCE_MAX_ROWS. */
    truncated?: boolean
    expired?: boolean
  }
}

const AUDIT: AuditRow[] = [
  {
    id: "a1", time: "14:31:02",
    actor: { kind: "human", name: "a.reyes@company.com", role: "support" },
    action: "replay", target: "orders", result: "applied", tone: "ok", count: "1",
    payload: {
      count: 1,
      retainedUntil: "13 Nov 14:31",
      hash: "abc3f1…",
      bodies: [
        {
          id: "ORD-88213",
          size: "1.2 KB",
          before: `{
  "orderId": "ORD-88213",
  "customer": "acme-pty",
  "total": 2499.0,
  "currency": "AUD",
  "status": "payment_pending"
}`,
          after: `{
  "orderId": "ORD-88213",
  "customer": "acme-pty",
  "total": 1999.0,
  "currency": "AUD",
  "status": "payment_pending"
}`,
        },
      ],
    },
  },
  {
    id: "a2", time: "14:29:41",
    actor: { kind: "human", name: "a.reyes@company.com", role: "support" },
    via: "Claude", action: "replay", target: "orders", result: "applied", tone: "ok", count: "3",
    payload: {
      count: 3,
      retainedUntil: "13 Nov 14:29",
      hash: "d90f4c…",
      bodies: [
        {
          id: "ORD-88201", size: "1.3 KB",
          preview: '{ "orderId": "ORD-88201", "status": "payment_pending", … }',
          before: `{
  "orderId": "ORD-88201",
  "customer": "acme-pty",
  "total": 1299.0,
  "currency": "AUD",
  "status": "payment_pending"
}`,
        },
        {
          id: "ORD-88206", size: "1.1 KB",
          preview: '{ "orderId": "ORD-88206", "status": "payment_pending", … }',
          before: `{
  "orderId": "ORD-88206",
  "customer": "brightwater",
  "total": 899.0,
  "currency": "AUD",
  "status": "payment_pending"
}`,
        },
        {
          id: "ORD-88209", size: "1.2 KB",
          preview: '{ "orderId": "ORD-88209", "status": "payment_pending", … }',
          before: `{
  "orderId": "ORD-88209",
  "customer": "acme-pty",
  "total": 2499.0,
  "currency": "AUD",
  "status": "payment_pending"
}`,
        },
      ],
    },
  },
  {
    id: "a3", time: "14:22:10",
    actor: { kind: "human", name: "a.reyes@company.com", role: "support" },
    action: "discard", target: "orders", result: "applied", tone: "ok", count: "2",
    payload: {
      count: 2,
      retainedUntil: "13 Nov 14:22",
      hash: "9e21c4…",
      bodies: [
        {
          id: "ORD-88197",
          size: "1.4 KB",
          preview: '{ "orderId": "ORD-88197", "status": "shipped", … }',
          before: `{
  "orderId": "ORD-88197",
  "customer": "acme-pty",
  "total": 899.0,
  "currency": "AUD",
  "status": "shipped",
  "carrier": "auspost",
  "trackingId": "3P8829910A"
}`,
        },
        {
          id: "ORD-88190",
          size: "1.1 KB",
          preview: '{ "orderId": "ORD-88190", "total": 1299.0, … }',
          before: `{
  "orderId": "ORD-88190",
  "customer": "brightwater",
  "total": 1299.0,
  "currency": "AUD",
  "status": "payment_pending"
}`,
        },
      ],
    },
  },
  {
    id: "a4", time: "14:18:55",
    actor: { kind: "human", name: "m.cruz@company.com", role: "admin" },
    action: "purge", target: "payments", result: "applied", tone: "ok", count: "10",
    payload: {
      count: 10,
      retainedUntil: "13 Nov 14:18",
      hash: "77aa10…",
      bodies: [
        {
          id: "PAY-5591",
          size: "2.3 KB",
          preview: '{ "paymentId": "PAY-5591", "amount": 2499.0, … }',
          before: `{
  "paymentId": "PAY-5591",
  "orderId": "ORD-88213",
  "amount": 2499.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "stripe",
  "capturedAt": "2026-11-10T14:18:02Z"
}`,
        },
        {
          id: "PAY-5588",
          size: "1.9 KB",
          preview: '{ "paymentId": "PAY-5588", "amount": 899.0, … }',
          before: `{
  "paymentId": "PAY-5588",
  "orderId": "ORD-88197",
  "amount": 899.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "stripe",
  "capturedAt": "2026-11-10T14:11:47Z"
}`,
        },
        {
          id: "PAY-5581",
          size: "2.1 KB",
          preview: '{ "paymentId": "PAY-5581", "amount": 1299.0, … }',
          before: `{
  "paymentId": "PAY-5581",
  "orderId": "ORD-88190",
  "amount": 1299.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "adyen",
  "capturedAt": "2026-11-10T14:02:19Z"
}`,
        },
        {
          id: "PAY-5577",
          size: "2.0 KB",
          preview: '{ "paymentId": "PAY-5577", "amount": 450.0, … }',
          before: `{
  "paymentId": "PAY-5577",
  "orderId": "ORD-88184",
  "amount": 450.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "adyen",
  "capturedAt": "2026-11-10T13:58:55Z"
}`,
        },
        {
          id: "PAY-5570",
          size: "1.7 KB",
          preview: '{ "paymentId": "PAY-5570", "amount": 1799.0, … }',
          before: `{
  "paymentId": "PAY-5570",
  "orderId": "ORD-88181",
  "amount": 1799.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "stripe",
  "capturedAt": "2026-11-10T13:51:31Z"
}`,
        },
        {
          id: "PAY-5564",
          size: "2.2 KB",
          preview: '{ "paymentId": "PAY-5564", "amount": 2499.0, … }',
          before: `{
  "paymentId": "PAY-5564",
  "orderId": "ORD-88176",
  "amount": 2499.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "stripe",
  "capturedAt": "2026-11-10T13:44:08Z"
}`,
        },
        {
          id: "PAY-5559",
          size: "1.8 KB",
          preview: '{ "paymentId": "PAY-5559", "amount": 899.0, … }',
          before: `{
  "paymentId": "PAY-5559",
  "orderId": "ORD-88173",
  "amount": 899.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "adyen",
  "capturedAt": "2026-11-10T13:39:12Z"
}`,
        },
        {
          id: "PAY-5551",
          size: "2.4 KB",
          preview: '{ "paymentId": "PAY-5551", "amount": 3199.0, … }',
          before: `{
  "paymentId": "PAY-5551",
  "orderId": "ORD-88169",
  "amount": 3199.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "stripe",
  "capturedAt": "2026-11-10T13:31:40Z"
}`,
        },
        {
          id: "PAY-5546",
          size: "1.6 KB",
          preview: '{ "paymentId": "PAY-5546", "amount": 599.0, … }',
          before: `{
  "paymentId": "PAY-5546",
  "orderId": "ORD-88164",
  "amount": 599.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "adyen",
  "capturedAt": "2026-11-10T13:24:57Z"
}`,
        },
        {
          id: "PAY-5540",
          size: "2.1 KB",
          preview: '{ "paymentId": "PAY-5540", "amount": 1499.0, … }',
          before: `{
  "paymentId": "PAY-5540",
  "orderId": "ORD-88158",
  "amount": 1499.0,
  "currency": "AUD",
  "status": "captured",
  "gateway": "stripe",
  "capturedAt": "2026-11-10T13:17:23Z"
}`,
        },
      ],
    },
  },
  {
    id: "a5", time: "13:58:12",
    actor: { kind: "agent", name: "Claude", behalf: "a.reyes@company.com" },
    action: "proposal — replay", target: "orders", result: "dismissed by human", tone: "idle", count: "5",
  },
  {
    id: "a5b", time: "13:52:44",
    actor: { kind: "human", name: "a.reyes@company.com", role: "support" },
    action: "discard", target: "orders", result: "applied", tone: "ok", count: "3",
    payload: {
      count: 3,
      retainedUntil: "expired 06 Nov",
      retention: "3-day",
      hash: "4b7d02…",
      bodies: [],
      expired: true,
    },
  },
  {
    id: "a6", time: "13:44:03",
    actor: { kind: "human", name: "a.reyes@company.com", role: "support" },
    action: "publish", target: "events / order.created", result: "applied", tone: "ok", count: "1",
    payload: {
      count: 1,
      retainedUntil: "13 Nov 13:44",
      hash: "c81e57…",
      bodies: [
        {
          id: "msg-01JCV7",
          size: "0.6 KB",
          before: `{
  "event": "order.created",
  "orderId": "ORD-88301",
  "customer": "acme-pty",
  "total": 1249.0,
  "currency": "AUD",
  "createdAt": "2026-11-10T13:44:02Z"
}`,
        },
      ],
    },
  },
  {
    id: "a7", time: "13:21:37",
    actor: { kind: "human", name: "m.cruz@company.com", role: "admin" },
    action: "update capture policy", target: "orders", result: "applied", tone: "ok", count: "—",
  },
]

/** One column of a body, with the lines that changed picked out. */
function EvidencePane({
  label,
  lines,
  changed,
}: {
  label: string
  lines: string[]
  changed?: Set<number>
}) {
  return (
    <div className="bg-card">
      <div className="border-b bg-secondary/50 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-[0.07em] text-muted-foreground">
        {label}
      </div>
      <div className="max-h-[176px] overflow-auto p-2 font-mono text-[11px] leading-[1.65]">
        {lines.map((line, i) => {
          const hit = changed?.has(i)
          return (
            <div
              key={i}
              className={cn(
                "whitespace-pre rounded-sm px-1",
                hit && "-ml-1 border-l-2 border-warning bg-warning/12 pl-1.5 font-semibold text-foreground"
              )}
            >
              {line || " "}
            </div>
          )
        })}
      </div>
    </div>
  )
}

/**
 * A single message: the whole body, not a preview. When the operator edited it
 * before replaying, both versions sit side by side so "what was replayed" and
 * "did it change" are answered without opening anything else. Clicking opens the
 * same body full-size, with line numbers.
 */
function SingleEvidence({
  body,
  retainedUntil,
  hash,
  action,
  onOpen,
}: {
  body: AuditPayload
  retainedUntil: string
  hash: string
  action: PayloadAction
  onOpen: () => void
}) {
  const labels = PAYLOAD_LABEL[action]
  const edited = Boolean(body.after) && body.after !== body.before
  const { b: beforeLines, a: afterLines, changed } = diffLines(body.before, body.after ?? body.before)
  return (
    <div>
      <div className="flex flex-wrap items-baseline gap-2 text-[12.5px]">
        <span className="font-medium">Payload evidence</span>
        <span className="text-muted-foreground">1 body · retained until {retainedUntil}</span>
        <span className="ml-auto font-mono text-[11px] text-muted-foreground">{hash}</span>
      </div>

      <div className="mt-2 overflow-hidden rounded border bg-card">
        <div className="flex flex-wrap items-center gap-2 border-b px-2.5 py-1.5">
          <span className="font-mono text-[11.5px]">{body.id}</span>
          <span className="text-[11px] text-muted-foreground">{body.size}</span>
          {edited ? (
            <>
              <Badge variant="warning">payload edited</Badge>
              <span className="text-[11px] text-muted-foreground">
                the replayed body is not the one that was dead-lettered
              </span>
            </>
          ) : (
            <span className="text-[11px] text-muted-foreground">
              {action === "replay" ? "replayed unchanged" : `the body ${labels.past}`}
            </span>
          )}
          <button
            onClick={onOpen}
            className="ml-auto inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-[10.5px] font-medium text-muted-foreground hover:bg-secondary hover:text-foreground"
          >
            <Maximize2 className="h-3 w-3" />
            Full body
          </button>
        </div>
        <button
          type="button"
          onClick={onOpen}
          aria-label={`Open the full payload for ${body.id}`}
          className={cn("grid w-full cursor-pointer gap-px bg-border text-left", edited && "grid-cols-2")}
        >
          {edited && <EvidencePane label="before" lines={beforeLines} changed={changed} />}
          <EvidencePane
            label={edited ? "replayed" : labels.before}
            lines={afterLines}
            changed={edited ? changed : undefined}
          />
        </button>
      </div>
    </div>
  )
}

/** A bulk operation: every message it touched, one row each. No sampling. */
function BulkEvidence({
  count,
  bodies,
  retainedUntil,
  hash,
  truncated,
  onOpen,
}: {
  count: number
  bodies: AuditPayload[]
  retainedUntil: string
  hash: string
  truncated?: boolean
  onOpen: (body: AuditPayload) => void
}) {
  return (
    <div>
      <div className="flex flex-wrap items-baseline gap-2 text-[12.5px]">
        <span className="font-medium">Payload evidence</span>
        <span className="text-muted-foreground">
          {count} bodies · one record per message · retained until {retainedUntil}
        </span>
        <span className="ml-auto font-mono text-[11px] text-muted-foreground">{hash}</span>
      </div>

      <div className="mt-2 max-h-[340px] space-y-1 overflow-auto">
        {bodies.map((s) => (
          <button
            key={s.id}
            type="button"
            onClick={() => onOpen(s)}
            aria-label={`Open the full payload for ${s.id}`}
            className="flex w-full items-baseline gap-2 rounded border bg-card px-2.5 py-1.5 text-left hover:border-muted-foreground/30 hover:bg-secondary/60"
          >
            <span className="w-[92px] shrink-0 font-mono text-[11px]">{s.id}</span>
            <span className="w-[52px] shrink-0 text-[11px] text-muted-foreground">{s.size}</span>
            <span className="min-w-0 flex-1 truncate font-mono text-[11px] text-muted-foreground">
              {s.preview ?? s.before}
            </span>
            {s.after && <span className="shrink-0 text-[11px] text-warning">→ edited</span>}
            <Maximize2 className="h-3 w-3 shrink-0 self-center text-muted-foreground/60" />
          </button>
        ))}
      </div>

      {truncated && (
        <p className="mt-1.5 text-[11px] text-warning">
          Truncated at MARITES_EVIDENCE_MAX_ROWS — the operation touched more messages than the
          evidence ceiling allows. The count is still exact.
        </p>
      )}
    </div>
  )
}

/** Turn an audit row plus one of its bodies into what the dialog needs. */
function toPayloadView(row: AuditRow, body: AuditPayload): PayloadView {
  return {
    action: row.action as PayloadAction,
    queue: row.target,
    messageId: body.id,
    size: body.size,
    hash: row.payload?.hash ?? "",
    retainedUntil: row.payload?.retainedUntil ?? "",
    before: body.before,
    after: body.after,
  }
}

const ROW_FOR_HASH: Record<string, string> = {
  "#audit-payload": "a1",
  "#audit-bulk": "a4",
  "#payload-diff": "a1",
  "#payload-discard": "a3",
  "#payload-purge": "a4",
  "#payload-publish": "a6",
  "#audit-expired": "a5b",
}

function AuditScreen() {
  const [open, setOpen] = React.useState<string | null>(() => ROW_FOR_HASH[window.location.hash] ?? null)
  const [view, setView] = React.useState<PayloadView | null>(() => {
    const row = AUDIT.find((r) => r.id === ROW_FOR_HASH[window.location.hash])
    const body = row?.payload?.bodies[0]
    return row && body && window.location.hash.startsWith("#payload") ? toPayloadView(row, body) : null
  })

  function openFor(row: AuditRow, body: AuditPayload) {
    setOpen(row.id)
    setView(toPayloadView(row, body))
  }

  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-5">
        <h1 className="text-[22px] font-semibold tracking-[-0.025em]">Audit</h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          Append-only. No endpoint in this product can edit or delete a row.
        </p>
      </div>

      <div className="mb-4 flex gap-2.5 rounded-lg border border-primary/25 bg-accent/40 px-3.5 py-3 text-[12.5px] leading-relaxed text-accent-foreground">
        <Bot className="mt-0.5 h-4 w-4 shrink-0" />
        <div>
          <b>An agent acts as the person whose credential it holds.</b> A replay through Claude is
          recorded against that user — marked <span className="font-mono text-[11px]">via Claude</span> —
          so a replay at 3am is attributable, and the reasoning that prompted it travels with the row.
          A credential issued read-only can only propose, which is the <span className="font-mono text-[11px]">proposal</span> row below.
        </div>
      </div>

      <Card className="overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className="w-[90px]">Time</TableHead>
              <TableHead>Actor</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>Target</TableHead>
              <TableHead>Result</TableHead>
              <TableHead className="text-right">Messages</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {AUDIT.map((r) => (
              <React.Fragment key={r.id}>
              <TableRow>
                <TableCell className="num text-muted-foreground">{r.time}</TableCell>
                <TableCell>
                  {r.actor.kind === "human" ? (
                    <div className="flex items-center gap-2">
                      <span className="text-[13px]">{r.actor.name}</span>
                      <span className="rounded-full bg-secondary px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-[0.05em] text-muted-foreground">
                        {r.actor.role}
                      </span>
                      {r.via && (
                        <span
                          title={`Submitted through ${r.via} with this user's own credential`}
                          className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold text-primary"
                        >
                          <Bot className="h-2.5 w-2.5" />
                          via {r.via}
                        </span>
                      )}
                    </div>
                  ) : (
                    <div className="flex items-center gap-2">
                      <Bot className="h-3.5 w-3.5 text-primary" />
                      <span className="text-[13px] font-medium">{r.actor.name}</span>
                      <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-[0.05em] text-primary">
                        agent
                      </span>
                      <span className="text-[11.5px] text-muted-foreground">
                        on behalf of {r.actor.behalf}
                      </span>
                    </div>
                  )}
                </TableCell>
                <TableCell className="font-mono text-[12px]">{r.action}</TableCell>
                <TableCell className="text-[12.5px] text-muted-foreground">{r.target}</TableCell>
                <TableCell>
                  <Badge variant={r.tone === "ok" ? "success" : r.tone === "warn" ? "warning" : "idle"}>
                    {r.result}
                  </Badge>
                </TableCell>
                <TableCell className="num text-right text-muted-foreground">
                  <span className="inline-flex items-center justify-end gap-1.5">
                    {r.count}
                    {r.payload?.bodies.some((b) => b.after && b.after !== b.before) && (
                      <span
                        title="A body was edited before this operation ran"
                        className="rounded bg-warning/12 px-1 py-0.5 text-[9.5px] font-semibold uppercase tracking-[0.05em] text-warning"
                      >
                        edited
                      </span>
                    )}
                  </span>
                </TableCell>
                <TableCell>
                  {r.payload && (
                    <button
                      onClick={() => setOpen(open === r.id ? null : r.id)}
                      aria-label={open === r.id ? "Hide payload evidence" : "Show payload evidence"}
                      aria-expanded={open === r.id}
                      className="grid h-6 w-6 place-items-center rounded text-muted-foreground hover:bg-secondary hover:text-foreground"
                    >
                      <ChevronDown
                        className={cn("h-3.5 w-3.5 transition-transform", open === r.id && "rotate-180")}
                      />
                    </button>
                  )}
                </TableCell>
              </TableRow>

              {open === r.id && r.payload && (
                <TableRow className="hover:bg-transparent">
                  <TableCell colSpan={7} className="bg-secondary/40 px-4 py-3">
                    {r.payload.expired ? (
                      <div>
                        <div className="flex flex-wrap items-baseline gap-2 text-[12.5px]">
                          <span className="font-medium">Payload no longer retained</span>
                          <span className="text-muted-foreground">
                            swept {r.payload.retainedUntil.replace("expired ", "")}
                          </span>
                          <span className="ml-auto font-mono text-[11px] text-muted-foreground">
                            {r.payload.hash}
                          </span>
                        </div>
                        <p className="mt-1 text-[11.5px] leading-relaxed text-muted-foreground">
                          {r.payload.count} {r.payload.count === 1 ? "body was" : "bodies were"} kept
                          on the same clock as the messages {r.payload.count === 1 ? "it came" : "they came"}{" "}
                          from — this queue's {r.payload.retention ?? "row"} row retention — and were
                          deleted with them. The operation and its hash survive; the content does not.
                        </p>
                      </div>
                    ) : r.payload.count === 1 && r.payload.bodies[0] ? (
                      <SingleEvidence
                        body={r.payload.bodies[0]}
                        retainedUntil={r.payload.retainedUntil}
                        hash={r.payload.hash}
                        action={r.action as PayloadAction}
                        onOpen={() => openFor(r, r.payload!.bodies[0])}
                      />
                    ) : (
                      <BulkEvidence
                        count={r.payload.count}
                        bodies={r.payload.bodies}
                        retainedUntil={r.payload.retainedUntil}
                        hash={r.payload.hash}
                        truncated={r.payload.truncated}
                        onOpen={(body) => openFor(r, body)}
                      />
                    )}
                  </TableCell>
                </TableRow>
              )}
              </React.Fragment>
            ))}
          </TableBody>
        </Table>
        <div className="border-t px-4 py-2.5 text-[11.5px] leading-relaxed text-muted-foreground">
          <b className="text-foreground/80">Anything that touches the broker is listed</b> — including
          peek, which briefly removes a message, and preflight, which declares and deletes a probe
          queue. Reads of Marites' own data — queue lists, metrics, message search — are not: the log
          records side effects, not attention.
        </div>
      </Card>

      <PayloadDialog view={view} onClose={() => setView(null)} />
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Reporting                                                           */
/* ------------------------------------------------------------------ */

function ReportScreen({ liveOnly }: { liveOnly?: boolean }) {
  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      {liveOnly && (
        <div className="mb-4 flex flex-wrap items-center gap-2.5 rounded-lg border border-warning/30 bg-warning/[0.08] px-3.5 py-2.5 text-[13px] text-warning">
          <Database className="h-4 w-4 shrink-0" />
          <span>
            <b>Live only.</b> Nothing has been counted — this screen draws history, and there is
            none. Counts begin the moment a message store is connected.
          </span>
          <span className="ml-auto text-[11.5px] opacity-80">Admin → Storage</span>
        </div>
      )}
      <div className="mb-5 flex items-end gap-4">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.025em]">Volume &amp; failures</h1>
          <p className="mt-0.5 text-[13px] text-muted-foreground">
            prod-cluster · last 30 days · daily totals
          </p>
        </div>
        <div className="flex-1" />
        <Button variant="outline">
          Last 30 days <ChevronDown className="h-3 w-3 opacity-50" />
        </Button>
      </div>

      <div className="mb-4 grid grid-cols-4 gap-3 max-[900px]:grid-cols-2">
        {(liveOnly
          ? [
              { k: "Messages published", v: "—", d: "not counted" },
              { k: "Dead letters", v: "—", d: "not counted" },
              { k: "Dead-letter rate", v: "—", d: "not computed" },
              { k: "Peak rate", v: "—", d: "not measured" },
            ]
          : [
              { k: "Messages published", v: "41.2M", d: "+6.4% vs previous 30 days" },
              { k: "Dead letters", v: "118,402", d: "−2.1% vs previous 30 days" },
              { k: "Dead-letter rate", v: "0.29%", d: "of published, same window" },
              { k: "Peak rate", v: "4,800 /s", d: "highest second in the window" },
            ]
        ).map((t) => (
          <Card key={t.k} className="px-4 py-3.5">
            <div className="text-[11px] uppercase tracking-[0.07em] text-muted-foreground">{t.k}</div>
            <div className="num mt-1.5 text-[26px] font-semibold tracking-[-0.035em]">{t.v}</div>
            <div className="mt-0.5 text-[12px] text-muted-foreground">{t.d}</div>
          </Card>
        ))}
      </div>

      {liveOnly ? (
        <Card className="px-6 py-16 text-center">
          <p className="text-[13px] font-medium">No history to draw</p>
          <p className="mx-auto mt-1.5 max-w-[440px] text-[12px] leading-relaxed text-muted-foreground">
            Every figure on this screen is read from the message store. Connect one and the
            trend, the per-queue breakdown and the totals all begin from that moment — the
            period before it cannot be reconstructed.
          </p>
        </Card>
      ) : (
        <>
      <Card className="mb-4 px-4 pb-2 pt-4">
        <div className="mb-1 flex items-baseline gap-2.5">
          <h3 className="text-[13px] font-semibold">Daily totals</h3>
          <span className="text-[11.5px] text-muted-foreground">· per day, UTC</span>
          <div className="ml-auto flex gap-3.5 text-[11.5px] text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-[2px] bg-primary" /> published
            </span>
            <span className="flex items-center gap-1.5">
              <span className="h-2 w-2 rounded-[2px] bg-destructive" /> dead letters
            </span>
          </div>
        </div>

        <svg viewBox="0 0 720 180" className="mt-1 block w-full" role="img"
             aria-label="Daily published volume over 30 days, with dead letters">
          <defs>
            <linearGradient id="area" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" style={{ stopColor: "hsl(var(--primary))", stopOpacity: 0.22 }} />
              <stop offset="100%" style={{ stopColor: "hsl(var(--primary))", stopOpacity: 0 }} />
            </linearGradient>
          </defs>
          <g className="stroke-border" strokeWidth={1}>
            {[40, 80, 120, 160].map((y) => (
              <line key={y} x1="0" y1={y} x2="720" y2={y} />
            ))}
          </g>
          <path
            d="M0 150 L60 138 L120 142 L180 120 L240 126 L300 104 L360 112 L420 92 L480 98 L540 74 L600 82 L660 66 L720 72 L720 180 L0 180 Z"
            fill="url(#area)"
          />
          <path
            d="M0 150 L60 138 L120 142 L180 120 L240 126 L300 104 L360 112 L420 92 L480 98 L540 74 L600 82 L660 66 L720 72"
            fill="none"
            className="stroke-primary"
            strokeWidth={2.2}
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <path
            d="M0 172 L60 170 L120 173 L180 166 L240 169 L300 162 L360 165 L420 158 L480 161 L540 154 L600 157 L660 150 L720 153"
            fill="none"
            className="stroke-destructive"
            strokeWidth={1.6}
            strokeDasharray="4 4"
            opacity={0.85}
          />
          <circle cx="660" cy="66" r="3.5" className="fill-primary stroke-card" strokeWidth={2} />
        </svg>
      </Card>

      <Card className="overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>Queue</TableHead>
              <TableHead className="text-right">Published</TableHead>
              <TableHead className="text-right">Dead letters</TableHead>
              <TableHead className="text-right">Rate</TableHead>
              <TableHead>Last seen</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {[
              ["orders", "38.1M", "110,204", "0.29%"],
              ["payments", "3.1M", "8,198", "0.26%"],
            ].map(([q, p, d, r]) => (
              <TableRow key={q}>
                <TableCell className="font-medium">{q}</TableCell>
                <TableCell className="num text-right">{p}</TableCell>
                <TableCell className="num text-right">{d}</TableCell>
                <TableCell className="num text-right">{r}</TableCell>
                <TableCell className="text-[12px] text-muted-foreground">2 min ago</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
        </>
      )}
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* Setup                                                               */
/* ------------------------------------------------------------------ */

type WatchedQueue = {
  id: string
  name: string
  kind: "queue" | "exchange"
  messages: number | null
  dlx: boolean
}

const WATCHABLE: WatchedQueue[] = [
  { id: "q1", name: "orders", kind: "queue", messages: 12481, dlx: true },
  { id: "q2", name: "orders.dlq", kind: "queue", messages: 1204, dlx: false },
  { id: "q3", name: "payments", kind: "queue", messages: 0, dlx: true },
  { id: "q4", name: "reports", kind: "queue", messages: 3110, dlx: true },
  { id: "q5", name: "shipments", kind: "queue", messages: 842, dlx: true },
  { id: "x1", name: "events", kind: "exchange", messages: null, dlx: false },
]

function SetupScreen({
  onDone,
  onStorageEnabled,
  onSkipStorage,
}: {
  onDone: () => void
  onStorageEnabled: () => void
  onSkipStorage: () => void
}) {
  // ?step=N is a review affordance: it opens the wizard at a given step
  const requested = Number(new URLSearchParams(window.location.search).get("step") ?? "1")
  const openingStep = requested >= 1 && requested <= 5 ? requested : 1

  const [step, setStep] = React.useState(openingStep)
  const [dbEngine, setDbEngine] = React.useState<"postgres" | "sqlserver">("postgres")
  const [dbMode, setDbMode] = React.useState<"create" | "precreated">("precreated")
  const [dbChecked, setDbChecked] = React.useState(openingStep >= 2)
  const [provider, setProvider] = React.useState<Provider>("rabbitmq")
  const [credential, setCredential] = React.useState<string>("basic")
  const [preflightPassing, setPreflightPassing] = React.useState(openingStep >= 4)
  const [checked, setChecked] = React.useState(openingStep >= 3)
  const [selected, setSelected] = React.useState<Set<string>>(
    () => new Set(WATCHABLE.filter((q) => q.dlx).map((q) => q.id))
  )
  const [backfill, setBackfill] = React.useState(true)
  const [enabled, setEnabled] = React.useState(false)

  const PROVIDERS: { id: Provider; label: string }[] = [
    { id: "rabbitmq", label: "RabbitMQ" },
    { id: "servicebus", label: "Service Bus" },
    { id: "pubsub", label: "Pub/Sub" },
  ]

  const CREDS: Record<Provider, { id: string; label: string; ambient?: boolean }[]> = {
    rabbitmq: [
      { id: "basic", label: "Basic (AMQP)" },
      { id: "http_basic", label: "HTTP basic (management)" },
      { id: "mtls", label: "mTLS certificate" },
    ],
    servicebus: [
      { id: "sas_key", label: "SAS policy key" },
      { id: "entra_service_principal", label: "Service principal" },
      { id: "managed_identity", label: "Managed identity", ambient: true },
    ],
    pubsub: [
      { id: "service_account_json", label: "Service account JSON" },
      { id: "workload_identity", label: "Workload identity", ambient: true },
    ],
  }

  const isRabbit = provider === "rabbitmq"
  const options = CREDS[provider]
  const ambient = options.find((o) => o.id === credential)?.ambient ?? false

  const maxStep = !dbChecked ? 1 : preflightPassing ? 5 : 3
  const chosen = WATCHABLE.filter((q) => selected.has(q.id))
  const backlog = chosen.reduce((n, q) => n + (q.messages ?? 0), 0)

  function pickProvider(id: Provider) {
    setProvider(id)
    setCredential(CREDS[id][0].id)
    setChecked(false)
    setPreflightPassing(false)
  }

  function toggleQueue(id: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const checks = [
    { state: "pass", name: "Management API reachable", note: "142 ms" },
    { state: "pass", name: "Vhost visible", note: "/prod" },
    {
      state: preflightPassing ? "pass" : "fail",
      name: "Topology write permitted",
      note: preflightPassing ? "marites.*" : "ERR-BRK-001",
    },
    { state: "pass", name: "Dead-letter routing", note: "4 of 5 queues" },
    { state: "pass", name: "Clock skew", note: "0.4 s" },
  ] as const

  const steps = [
    { n: 1, label: "Storage" },
    { n: 2, label: "Profile" },
    { n: 3, label: "Preflight" },
    { n: 4, label: "Select queues" },
    { n: 5, label: "Capture" },
  ]

  /* ---------------- enabled ---------------- */
  if (enabled) {
    return (
      <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
        <div className="mx-auto max-w-[640px] pt-6">
          <Card>
            <CardContent className="pt-5">
              <div className="mb-3 flex items-center gap-2.5">
                <span className="grid h-6 w-6 place-items-center rounded-full bg-success/12 text-[12px] font-bold text-success">
                  ✓
                </span>
                <h1 className="text-[19px] font-semibold tracking-[-0.02em]">Capture enabled</h1>
              </div>
              <p className="text-[13px] leading-relaxed text-muted-foreground">
                {chosen.length} entities are now watched on <b className="text-foreground">prod-cluster</b>.
                One shadow queue and binding were created per exchange, bounded by{" "}
                <span className="font-mono text-[12px]">x-max-length</span> and{" "}
                <span className="font-mono text-[12px]">x-message-ttl</span>. Nothing in the adopter's
                own topology was changed.
              </p>

              <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 border-t pt-3.5 text-[13px]">
                <dt className="text-muted-foreground">Watched</dt>
                <dd className="text-right font-medium">{chosen.length}</dd>
                <dt className="text-muted-foreground">Payload policy</dt>
                <dd className="text-right font-medium">sampled · 1 in 100</dd>
                <dt className="text-muted-foreground">Row retention</dt>
                <dd className="text-right font-medium">72 hours</dd>
                <dt className="text-muted-foreground">Backfill</dt>
                <dd className="text-right font-medium">
                  {backfill ? `${backlog.toLocaleString("en-US")} messages queued` : "skipped"}
                </dd>
              </dl>

              <div className="mt-4 flex gap-2.5 border-t pt-3.5">
                <Button onClick={onDone}>Open the dashboard</Button>
                <Button
                  variant="outline"
                  onClick={() => {
                    setEnabled(false)
                    setStep(4)
                  }}
                >
                  Back to selection
                </Button>
              </div>

              <p className="mt-3 text-[11.5px] leading-relaxed text-muted-foreground">
                The dashboard goes live within one poll interval. Counts appear as messages flow;
                history before the binding exists comes only from the backfill.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    )
  }

  /* ---------------- wizard ---------------- */
  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-5">
        <h1 className="text-[22px] font-semibold tracking-[-0.025em]">Set up Marites</h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          Admin only · connects a database, registers a broker, proves it with preflight, then
          chooses what to watch. Storage comes first: capture has nowhere to write until it exists.
        </p>
      </div>

      {/* step strip — clickable backwards, locked forwards until preflight passes */}
      <div className="mb-5 flex gap-1.5">
        {steps.map((s) => {
          const reachable = s.n <= maxStep
          const state = s.n === step ? "now" : s.n < step ? "done" : "todo"
          return (
            <button
              key={s.n}
              onClick={() => reachable && setStep(s.n)}
              disabled={!reachable}
              aria-current={state === "now" ? "step" : undefined}
              className={cn(
                "flex-1 rounded-lg border bg-card px-3 py-2.5 text-left text-[12.5px] text-muted-foreground transition-colors",
                state === "done" && "border-success/35 bg-success/[0.07] text-success hover:bg-success/[0.1]",
                state === "now" && "border-primary bg-accent font-semibold text-accent-foreground",
                !reachable && "cursor-not-allowed opacity-55"
              )}
            >
              <div className="mb-0.5 text-[10.5px] uppercase tracking-[0.08em] opacity-80">
                Step {s.n}
              </div>
              <div className="flex items-center gap-1.5">
                {s.label}
                {!reachable && (
                  <span className="text-[10px] opacity-70">
                    · {!dbChecked ? "database first" : "preflight first"}
                  </span>
                )}
              </div>
            </button>
          )
        })}
      </div>

      {/* STEP 1 — storage */}
      {step === 1 && (
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
                        setDbEngine(id)
                        setDbChecked(false)
                      }}
                      aria-pressed={dbEngine === id}
                      className={cn(
                        "flex-1 rounded-[7px] px-2 py-1.5 text-[12.5px] text-muted-foreground transition-all",
                        dbEngine === id && "bg-card font-semibold text-foreground shadow-sm"
                      )}
                    >
                      {label}
                    </button>
                  ))}
                </div>
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
                      onClick={() => setDbMode(id)}
                      aria-pressed={dbMode === id}
                      className={cn(
                        "rounded-md border bg-card px-2.5 py-2 text-[12.5px] transition-colors",
                        dbMode === id
                          ? "border-primary bg-accent font-semibold text-accent-foreground"
                          : "text-muted-foreground hover:bg-secondary"
                      )}
                    >
                      {label}
                    </button>
                  ))}
                </div>
                <p className="mt-1.5 text-[11.5px] leading-relaxed text-muted-foreground">
                  {dbMode === "precreated"
                    ? "Recommended. Your DBA creates an empty database and grants DDL on one schema; Marites creates the tables inside it."
                    : "Needs CREATE DATABASE, a server-level right most DBAs refuse. Marites still never drops a database."}
                </p>
              </div>

              <div className="grid grid-cols-[1fr_90px] gap-3">
                <div>
                  <Label htmlFor="db-host">Host</Label>
                  <Input id="db-host" defaultValue="db.internal" className="mt-1.5 font-mono text-[12px]" />
                </div>
                <div>
                  <Label htmlFor="db-port">Port</Label>
                  <Input
                    id="db-port"
                    defaultValue={dbEngine === "postgres" ? "5432" : "1433"}
                    className="mt-1.5 font-mono text-[12px]"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label htmlFor="db-name">Database</Label>
                  <Input id="db-name" defaultValue="marites" className="mt-1.5 font-mono text-[12px]" />
                </div>
                <div>
                  <Label htmlFor="db-schema">Schema</Label>
                  <Input id="db-schema" defaultValue="marites" className="mt-1.5 font-mono text-[12px]" />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label htmlFor="db-user">Username</Label>
                  <Input id="db-user" defaultValue="marites_app" className="mt-1.5 font-mono text-[12px]" />
                </div>
                <div>
                  <Label htmlFor="db-pass">Password</Label>
                  <Input id="db-pass" type="password" defaultValue="secret" className="mt-1.5 font-mono text-[12px]" />
                </div>
              </div>

              {dbChecked && (
                <div className="rounded-lg border border-success/30 bg-success/[0.06] px-3 py-2.5 text-[12px] leading-relaxed text-success">
                  Connected · {dbEngine === "postgres" ? "PostgreSQL 16" : "SQL Server 2022"} · 10 tables
                  to create in schema <span className="font-mono">marites</span>
                </div>
              )}

              <p className="text-[11.5px] leading-relaxed text-muted-foreground">
                Credentials are write-only, encrypted per row, and never returned by any endpoint — the
                same rule as the broker credentials.
              </p>

              <div className="flex flex-wrap gap-2.5 border-t pt-3.5">
                <Button variant="outline" onClick={() => setDbChecked(true)} disabled={dbChecked}>
                  {dbChecked ? "Connected" : "Test connection"}
                </Button>
                <Button
                  disabled={!dbChecked}
                  onClick={() => {
                    onStorageEnabled()
                    setStep(2)
                  }}
                >
                  Enable and continue
                </Button>
              </div>
              <button
                onClick={() => {
                  onSkipStorage()
                  setStep(2)
                }}
                className="text-[11.5px] text-muted-foreground underline underline-offset-2 hover:text-foreground"
              >
                Skip for now — run live-only, add storage later
              </button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>Why this comes first</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <p>
                Capture has nowhere to write until the database exists, so the wizard asks for it
                before the broker. Configure the broker first and you discover at the last step that
                there is nowhere to put the messages.
              </p>
              <ul className="mt-3 space-y-2">
                <li className="flex gap-2">
                  <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                  Ten tables in the schema you name, created by Marites from its own migrations
                </li>
                <li className="flex gap-2">
                  <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                  Nothing outside that schema is read or written
                </li>
                <li className="flex gap-2">
                  <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                  A database is never dropped, and never altered outside its own tables
                </li>
              </ul>
              <p className="mt-3.5 border-t pt-3.5">
                <b className="text-foreground/80">Counting can work without storing messages.</b> A
                queue set to <span className="font-mono text-[11.5px]">counts</span> keeps exact
                figures and writes no message at all (DEC-013).
              </p>
            </CardContent>
          </Card>
        </div>
      )}

      {/* STEP 2 — profile */}
      {step === 2 && (
        <div className="grid grid-cols-[1.1fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle>Broker profile</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3.5 pt-0">
              <div>
                <Label>Provider</Label>
                <div className="mt-1.5 flex gap-0.5 rounded-[9px] bg-secondary p-0.5">
                  {PROVIDERS.map((p) => (
                    <button
                      key={p.id}
                      onClick={() => pickProvider(p.id)}
                      aria-pressed={provider === p.id}
                      className={cn(
                        "flex-1 rounded-[7px] px-2 py-1.5 text-[12.5px] text-muted-foreground transition-all",
                        provider === p.id && "bg-card font-semibold text-foreground shadow-sm"
                      )}
                    >
                      {p.label}
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <Label htmlFor="name">Profile name</Label>
                <Input id="name" defaultValue="prod-cluster" className="mt-1.5" />
              </div>

              <div>
                <Label htmlFor="endpoint">Endpoint</Label>
                <Input
                  id="endpoint"
                  className="mt-1.5 font-mono text-[12px]"
                  defaultValue={
                    isRabbit
                      ? "amqps://marites:••••@rabbit.internal:5671/prod"
                      : provider === "servicebus"
                        ? "sb://acme-prod.servicebus.windows.net"
                        : "https://pubsub.googleapis.com"
                  }
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label htmlFor="admin">
                    {isRabbit ? "Management API" : provider === "servicebus" ? "Namespace" : "Project ID"}
                  </Label>
                  <Input
                    id="admin"
                    className="mt-1.5 font-mono text-[12px]"
                    defaultValue={
                      isRabbit
                        ? "https://rabbit.internal:15672"
                        : provider === "servicebus"
                          ? "acme-prod"
                          : "acme-prod-42"
                    }
                  />
                </div>
                <div>
                  <Label htmlFor="vhost">{isRabbit ? "vhost" : "Environment"}</Label>
                  <Input
                    id="vhost"
                    className="mt-1.5 font-mono text-[12px]"
                    defaultValue={isRabbit ? "/prod" : "prod"}
                  />
                </div>
              </div>

              <div>
                <Label htmlFor="cred">Credential kind</Label>
                <select
                  id="cred"
                  value={credential}
                  onChange={(e) => {
                    setCredential(e.target.value)
                    setChecked(false)
                  }}
                  className="mt-1.5 h-9 w-full rounded-md border border-input bg-secondary/60 px-2.5 text-[13px] text-foreground shadow-sm focus-visible:border-ring focus-visible:bg-card focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20"
                >
                  {options.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </div>

              {ambient ? (
                <div className="rounded-lg border border-success/30 bg-success/[0.06] px-3 py-2.5 text-[12px] text-success">
                  This identity stores <b>no secret</b>. Nothing is written to the credential column —
                  the platform supplies it at runtime.
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <Label htmlFor="user">Username</Label>
                    <Input id="user" defaultValue="marites" className="mt-1.5 font-mono text-[12px]" />
                  </div>
                  <div>
                    <Label htmlFor="pass">Password</Label>
                    <Input
                      id="pass"
                      type="password"
                      defaultValue="secret"
                      className="mt-1.5 font-mono text-[12px]"
                    />
                  </div>
                </div>
              )}

              <p className="text-[11.5px] leading-relaxed text-muted-foreground">
                Credentials are <b className="text-foreground/80">write-only</b>: encrypted per row,
                never returned by any endpoint, never logged.
              </p>

              <div className="flex gap-2.5 border-t pt-3.5">
                <Button
                  onClick={() => {
                    setChecked(true)
                    setStep(3)
                  }}
                >
                  Run preflight
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>What this will create</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <p>
                Marites adds <b className="text-foreground/80">one observation point</b> per watched
                entity, all named with the <span className="font-mono">marites.</span> prefix:
              </p>
              <ul className="mt-3 space-y-2">
                <li className="flex gap-2">
                  <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                  A shadow queue bound to each watched exchange
                </li>
                <li className="flex gap-2">
                  <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                  Bounded by <span className="font-mono text-[11.5px]">x-max-length</span> and{" "}
                  <span className="font-mono text-[11.5px]">x-message-ttl</span>, so it can never fill
                  the broker's disk
                </li>
                <li className="flex gap-2">
                  <span className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-muted-foreground/60" />
                  Removable on demand, leaving the adopter's topology untouched
                </li>
              </ul>
              <p className="mt-3.5 border-t pt-3.5">
                It never modifies an existing queue, exchange, policy or consumer.
              </p>
            </CardContent>
          </Card>
        </div>
      )}

      {/* STEP 3 — preflight */}
      {step === 3 && (
        <div className="grid grid-cols-[1.1fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle>Readiness — prod-cluster</CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <div>
                {checks.map((c) => (
                  <div
                    key={c.name}
                    className="grid grid-cols-[22px_1fr_auto] items-start gap-2.5 border-b py-2.5 last:border-0"
                  >
                    <span
                      className={cn(
                        "mt-0.5 grid h-[18px] w-[18px] place-items-center rounded-full text-[10px] font-bold",
                        c.state === "pass"
                          ? "bg-success/10 text-success"
                          : "bg-destructive/10 text-destructive"
                      )}
                      aria-hidden="true"
                    >
                      {c.state === "pass" ? "✓" : "!"}
                    </span>
                    <div>
                      <div className="text-[13px] font-medium">{c.name}</div>
                      {c.state === "fail" && (
                        <div className="mt-0.5 text-[12px] leading-relaxed text-muted-foreground">
                          Your user lacks{" "}
                          <code className="rounded bg-secondary px-1 py-0.5 font-mono text-[11.5px]">
                            configure
                          </code>{" "}
                          on{" "}
                          <code className="rounded bg-secondary px-1 py-0.5 font-mono text-[11.5px]">
                            marites.*
                          </code>
                          . Capture cannot be enabled until it is granted.
                        </div>
                      )}
                    </div>
                    <div className="text-[12px] text-muted-foreground">{c.note}</div>
                  </div>
                ))}
              </div>

              <div className="mt-4 flex gap-2.5 border-t pt-3.5">
                <Button
                  variant="outline"
                  onClick={() => {
                    setChecked(true)
                    setPreflightPassing(true)
                  }}
                  disabled={preflightPassing}
                >
                  {preflightPassing ? "Passing" : "Re-run preflight"}
                </Button>
                <Button disabled={!preflightPassing} onClick={() => setStep(4)}>
                  Continue to queues
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>{preflightPassing ? "Ready" : "If a check fails"}</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              {preflightPassing ? (
                <>
                  <p>
                    Every mandatory check passes. The profile is saved as{" "}
                    <span className="font-mono text-[11.5px]">passing</span> and capture can be
                    enabled.
                  </p>
                  <p className="mt-3 border-t pt-3.5">
                    The next step chooses what to watch. Nothing is captured until you confirm.
                  </p>
                </>
              ) : (
                <>
                  <p>
                    A failing check blocks saving rather than degrading silently. The profile is kept
                    as <span className="font-mono text-[11.5px]">failing</span>: the live console still
                    works, and capture stays off.
                  </p>
                  <p className="mt-3">
                    Grant the permission in the broker, then re-run. This is the most common
                    onboarding outcome, and it is a two-minute fix when the error names the grant.
                  </p>
                  <div className="mt-3 rounded-md border bg-secondary/50 px-2.5 py-2 font-mono text-[11px] text-foreground/80">
                    {'rabbitmqctl set_permissions -p /prod marites "^{prefix}.*" "^{prefix}.*" "^{prefix}.*"'}
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      {/* STEP 4 — select queues */}
      {step === 4 && (
        <div className="space-y-4">
          <Card className="overflow-hidden">
            <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b px-4 py-2.5">
              <div className="flex items-baseline gap-2">
                <span className="text-[12px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
                  What to watch
                </span>
                <span className="text-[11.5px] text-muted-foreground">
                  {chosen.length} of {WATCHABLE.length} selected
                </span>
              </div>
              <div className="ml-auto flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setSelected(new Set(WATCHABLE.map((q) => q.id)))}
                >
                  Select all
                </Button>
                <Button variant="outline" size="sm" onClick={() => setSelected(new Set())}>
                  Clear
                </Button>
              </div>
            </div>

            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="w-10" />
                  <TableHead>Name</TableHead>
                  <TableHead>Kind</TableHead>
                  <TableHead className="text-right">Backlog</TableHead>
                  <TableHead>Dead-letter routing</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {WATCHABLE.map((q) => {
                  const on = selected.has(q.id)
                  return (
                    <TableRow
                      key={q.id}
                      onClick={() => toggleQueue(q.id)}
                      className={cn("cursor-pointer", on && "bg-accent/50 hover:bg-accent/60")}
                    >
                      <TableCell onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={on}
                          onCheckedChange={() => toggleQueue(q.id)}
                          aria-label={`Watch ${q.name}`}
                        />
                      </TableCell>
                      <TableCell className="font-medium">{q.name}</TableCell>
                      <TableCell className="text-[12px] text-muted-foreground">{q.kind}</TableCell>
                      <TableCell className="num text-right text-muted-foreground">
                        {q.messages === null ? "—" : q.messages.toLocaleString("en-US")}
                      </TableCell>
                      <TableCell>
                        {q.dlx ? (
                          <Badge variant="success">
                            <span className="h-1.5 w-1.5 rounded-full bg-current" /> configured
                          </Badge>
                        ) : (
                          <Badge variant="idle">
                            <span className="h-1.5 w-1.5 rounded-full bg-current" /> unavailable
                          </Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>

            <div className="border-t px-4 py-2.5 text-[11.5px] leading-relaxed text-muted-foreground">
              Queues that already report a dead-letter exchange are pre-selected, because they are the
              ones that can tell you <b className="text-foreground/80">why</b> something failed. The
              rest still count traffic.
            </div>
          </Card>

          <div className="flex items-center gap-2.5">
            <Button variant="outline" onClick={() => setStep(3)}>
              Back
            </Button>
            <Button disabled={chosen.length === 0} onClick={() => setStep(5)}>
              Continue
            </Button>
            <span className="text-[12px] text-muted-foreground">
              {chosen.length === 0
                ? "Select at least one entity to watch."
                : `${chosen.length} selected · ${backlog.toLocaleString("en-US")} messages of backlog`}
            </span>
          </div>
        </div>
      )}

      {/* STEP 5 — capture */}
      {step === 5 && (
        <div className="grid grid-cols-[1.1fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle>Confirm</CardTitle>
            </CardHeader>
            <CardContent className="pt-0">
              <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2.5 text-[13px]">
                <dt className="text-muted-foreground">Watching</dt>
                <dd className="text-right font-medium">
                  {chosen.length} {chosen.length === 1 ? "entity" : "entities"}
                </dd>
                <dt className="text-muted-foreground">Payload policy</dt>
                <dd className="text-right font-medium">sampled · 1 in 100</dd>
                <dt className="text-muted-foreground">Row retention</dt>
                <dd className="text-right font-medium">72 hours</dd>
                <dt className="text-muted-foreground">Storage ceiling</dt>
                <dd className="text-right font-medium">50 GB</dd>
              </dl>

              <div className="mt-3.5 flex flex-wrap gap-1.5 border-t pt-3.5">
                {chosen.map((q) => (
                  <Badge key={q.id} variant="secondary" className="font-mono text-[11px]">
                    {q.name}
                  </Badge>
                ))}
              </div>

              <div className="mt-3.5 flex items-start gap-2.5 rounded-lg border bg-secondary/40 px-3 py-2.5">
                <Checkbox
                  id="backfill"
                  checked={backfill}
                  onCheckedChange={(v) => setBackfill(Boolean(v))}
                  className="mt-0.5"
                />
                <label htmlFor="backfill" className="cursor-pointer text-[12px] leading-relaxed">
                  <span className="font-medium">Backfill the existing backlog</span>
                  <span className="mt-0.5 block text-muted-foreground">
                    {backlog.toLocaleString("en-US")} messages already sitting in these queues. Rate
                    limited and user-initiated — capture is forward-only, so this is the only way to
                    see them.
                  </span>
                </label>
              </div>

              <div className="mt-4 flex gap-2.5 border-t pt-3.5">
                <Button variant="outline" onClick={() => setStep(4)}>
                  Back
                </Button>
                <Button onClick={() => setEnabled(true)}>
                  Enable capture
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>What happens on enable</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <ol className="space-y-2.5">
                {[
                  "A shadow queue and binding are declared for each exchange, bounded by max-length and TTL.",
                  "Counting starts within one poll interval; the dashboard goes live.",
                  backfill
                    ? "The backlog is read once, at a limited rate, so no consumer is starved."
                    : "No backfill: only messages published from now on are captured.",
                  "Everything created is removable, and removing the profile cleans it up.",
                ].map((line, i) => (
                  <li key={i} className="flex gap-2.5">
                    <span className="mt-0.5 grid h-4 w-4 shrink-0 place-items-center rounded-full bg-secondary text-[10px] font-bold text-muted-foreground">
                      {i + 1}
                    </span>
                    {line}
                  </li>
                ))}
              </ol>
              <p className="mt-3.5 border-t pt-3.5">
                Nothing here is destructive. Enabling capture adds an observer; it never removes or
                alters a message.
              </p>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  )
}

/* ------------------------------------------------------------------ */
/* The dialog that must be slow                                        */
/* ------------------------------------------------------------------ */

function ReplayDialog({
  open,
  onOpenChange,
  messages,
  proposal,
}: {
  open: boolean
  onOpenChange: (v: boolean) => void
  messages: DLMessage[]
  proposal?: { rationale: string; by: string }
}) {
  const [confirm, setConfirm] = React.useState("")
  const [rate, setRate] = React.useState("60")
  const [body, setBody] = React.useState("")

  const single = messages.length === 1 ? messages[0] : null
  const bodyRef = React.useRef<HTMLTextAreaElement | null>(null)

  React.useEffect(() => {
    if (!open) return
    setConfirm("")
    setBody(single ? single.payload : "")
    // the textarea mounts empty, so scroll it back to the first line once the body lands
    const id = window.requestAnimationFrame(() => {
      const el = bodyRef.current
      if (!el) return
      el.scrollTop = 0
      el.setSelectionRange(0, 0)
    })
    return () => window.cancelAnimationFrame(id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, single?.id])

  const n = messages.length
  const rejected = messages.filter((m) => m.reason === "rejected").length
  const expired = n - rejected

  let parseError: string | null = null
  if (single) {
    try {
      JSON.parse(body)
    } catch (e) {
      parseError = e instanceof Error ? e.message : "Invalid JSON"
    }
  }

  const edited = Boolean(single) && body !== single!.payload
  const ready = confirm.trim() === "prod" && n > 0 && !parseError
  const perMinute = Math.max(1, parseInt(rate, 10) || 60)
  const eta = Math.max(1, Math.ceil(n / perMinute))
  const bytes = new TextEncoder().encode(body).length

  function format() {
    try {
      setBody(JSON.stringify(JSON.parse(body), null, 2))
    } catch {
      /* leave the text alone; the validity line already says why */
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="flex max-h-[90vh] w-[min(620px,calc(100vw-2rem))] max-w-[620px] flex-col"
        onOpenAutoFocus={(e) => {
          if (single) {
            e.preventDefault()
            bodyRef.current?.focus()
            bodyRef.current?.setSelectionRange(0, 0)
          }
        }}
      >
        <DialogHeader className="shrink-0">
          <DialogTitle>
            {n === 1 ? "Replay 1 message" : `Replay ${n} messages`} — orders
          </DialogTitle>
          <DialogDescription className="flex items-center gap-2 pt-1">
            <Badge variant="destructive" className="px-1.5 py-0">prod</Badge>
            <span>/prod · only the messages you selected</span>
          </DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          {proposal && (
            <div className="mb-3.5 flex gap-2.5 rounded-lg border border-primary/25 bg-accent/40 p-3 text-[12.5px] leading-relaxed">
              <Bot className="mt-0.5 h-4 w-4 shrink-0 text-primary" />
              <div>
                <b>Proposed by Claude.</b> {proposal.rationale}
                <div className="mt-1 text-[11.5px] text-muted-foreground">
                  Approving this records <b className="text-foreground/80">you</b> as the actor. The
                  proposal stays in the audit log beside it.
                </div>
              </div>
            </div>
          )}

          <div className="mb-3 flex items-baseline gap-2">
            <span className="num text-[30px] font-semibold tracking-[-0.04em]">{n}</span>
            <span className="text-[13px] text-muted-foreground">
              {n === 1 ? "message would be replayed" : "messages would be replayed"} to{" "}
              <b className="text-foreground">orders</b>
              {single && (
                <span className="ml-1 text-muted-foreground">
                  · <span className="font-mono text-[12px]">{single.routingKey}</span>
                </span>
              )}
            </span>
          </div>

          {(n > 1 || rejected === 0) && (
            <div className="mb-3.5 flex flex-wrap gap-1.5">
              {rejected > 0 && (
                <Badge variant="warning">
                  <span className="h-1.5 w-1.5 rounded-full bg-current" /> {rejected} rejected
                </Badge>
              )}
              {expired > 0 && (
                <Badge variant="idle">
                  <span className="h-1.5 w-1.5 rounded-full bg-current" /> {expired} expired
                </Badge>
              )}
            </div>
          )}

          {rejected > 0 && (
            <div className="mb-4 flex gap-2.5 rounded-lg border border-warning/25 bg-warning/[0.07] p-3 text-[12.5px] leading-relaxed text-warning">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <div>
                {rejected === 1 ? "This message was" : `${rejected} of these were`} rejected by the
                same consumer. Replaying {rejected === 1 ? "it" : "them"} will dead-letter again
                unless the consumer is fixed first.
              </div>
            </div>
          )}

          {/* payload: editable only when one message is selected */}
          {single ? (
            <div className="mb-4">
              <div className="mb-1.5 flex items-center gap-2">
                <Label htmlFor="payload" className="text-[11.5px]">
                  Payload
                </Label>
                <span className="text-[10.5px] uppercase tracking-[0.06em] text-muted-foreground">
                  json
                </span>
                {edited && (
                  <Badge variant="warning" className="px-1.5 py-0 text-[10px]">
                    edited
                  </Badge>
                )}
                <div className="ml-auto flex gap-1">
                  <Button variant="ghost" size="sm" className="h-6 px-2 text-[11.5px]" onClick={format}>
                    Format
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 px-2 text-[11.5px]"
                    onClick={() => setBody(single.payload)}
                    disabled={!edited}
                  >
                    Reset
                  </Button>
                </div>
              </div>

              <textarea
                id="payload"
                ref={bodyRef}
                value={body}
                onChange={(e) => setBody(e.target.value)}
                spellCheck={false}
                aria-invalid={Boolean(parseError)}
                aria-describedby="payload-status"
                className={cn(
                  "h-[clamp(110px,18vh,240px)] w-full resize-y rounded-md border bg-secondary/50 p-3 font-mono text-[12px] leading-relaxed text-foreground shadow-sm",
                  "focus-visible:border-ring focus-visible:bg-card focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/20",
                  parseError ? "border-destructive/60" : "border-input"
                )}
              />

              <div id="payload-status" className="mt-1.5 flex items-center gap-2 text-[11.5px]">
                {parseError ? (
                  <span className="text-destructive">Invalid JSON — {parseError}</span>
                ) : (
                  <span className="text-muted-foreground">
                    Valid JSON · {body.split("\n").length} lines · {bytes} bytes
                  </span>
                )}
              </div>

              {edited && !parseError && (
                <div className="mt-2 flex gap-2 rounded-md border border-warning/25 bg-warning/[0.07] px-2.5 py-2 text-[11.5px] leading-relaxed text-warning">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <div>
                    You changed the body. The audit row records the hash of what you send, so this
                    replay is no longer provably the message that failed — it is a new message on the
                    same routing key.
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="mb-4 rounded-lg border bg-secondary/40 px-3 py-2.5 text-[12px] leading-relaxed text-muted-foreground">
              <b className="text-foreground/80">{n} messages selected.</b> Payload editing needs one
              message at a time — select a single message to inspect and change its body. Replaying
              several sends each one exactly as it was published.
            </div>
          )}

          <div className="grid grid-cols-[110px_96px_1fr] items-start gap-3">
            <div>
              <Label htmlFor="rate">Rate / min</Label>
              <Input
                id="rate"
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                inputMode="numeric"
                className="mt-1.5"
              />
            </div>
            <div>
              <Label htmlFor="eta">Time</Label>
              <Input id="eta" value={`${eta} min`} disabled className="mt-1.5" />
            </div>
            <div>
              <Label htmlFor="confirm">
                Type <b className="font-mono text-foreground">prod</b> to confirm
              </Label>
              <Input
                id="confirm"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                placeholder="prod"
                autoComplete="off"
                spellCheck={false}
                className="mt-1.5"
              />
            </div>
          </div>
          <p className="mt-1.5 text-[11.5px] text-muted-foreground">
            The tag is the environment, not the queue — typing it is the check that this is not staging.
          </p>
        </div>

        <DialogFooter className="shrink-0">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button disabled={!ready} onClick={() => onOpenChange(false)}>
            Replay {n}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
