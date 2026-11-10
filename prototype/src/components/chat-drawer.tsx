import * as React from "react"
import { Bot, CornerDownLeft, X } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

/**
 * The chat drawer: the same agent as the MCP server, hosted inside Marites.
 * It reads and proposes; it never executes. A proposal is reviewed in the
 * normal guarded dialog, by a human (DOC-ARCH-004).
 */

type Evidence = { k: string; v: string }

type Turn =
  | { kind: "user"; text: string }
  | { kind: "agent"; text: string; evidence?: Evidence[]; propose?: { count: number } }

type Suggestion = {
  id: string
  label: string
  answer: string
  evidence: Evidence[]
  propose?: { count: number }
}

const SUGGESTIONS: Suggestion[] = [
  {
    id: "why",
    label: "Why is orders failing?",
    answer:
      "1,150 messages were rejected by the same consumer, and 54 expired. The rejections start at 14:31 — the same minute a consumer was redeployed — and every one carries routing key order.updated or order.created. The queue itself is healthy: 6 consumers, depth falling.",
    evidence: [
      { k: "Queue", v: "orders · prod" },
      { k: "Rejected", v: "1,150 since 14:31" },
      { k: "Expired", v: "54" },
      { k: "Capture", v: "live · last message 14:22:08" },
    ],
    propose: { count: 3 },
  },
  {
    id: "changed",
    label: "What changed in the last hour?",
    answer:
      "One thing: the dead-letter rate went from 0.1% to 28.6% at 14:31, and it is concentrated on orders. No other queue moved. captureState is live everywhere except reports, which stopped at 14:12 — that one is a stale worker, not a traffic problem.",
    evidence: [
      { k: "Dead-letter rate", v: "0.1% → 28.6% at 14:31" },
      { k: "Affected", v: "orders only" },
      { k: "Stale capture", v: "reports · since 14:12" },
    ],
  },
  {
    id: "worst",
    label: "Which queue is worst right now?",
    answer:
      "orders by rate (28.6%), but reports is the one I would look at first: capture stopped 10 minutes ago and its depth is rising, so its figures are frozen rather than low. payments has no dead-letter routing at all, which means a failure there would be invisible rather than zero.",
    evidence: [
      { k: "orders", v: "28.6% dead-letter rate" },
      { k: "reports", v: "capture stale since 14:12" },
      { k: "payments", v: "no dead-letter routing" },
    ],
  },
]

export function ChatDrawer({
  open,
  onClose,
  onOpenQueue,
  configured = false,
  canConfigure = false,
  onConfigure,
  model = "llama3.1:8b",
  sendsPayloads = false,
}: {
  open: boolean
  onClose: () => void
  onOpenQueue: () => void
  configured?: boolean
  canConfigure?: boolean
  onConfigure?: () => void
  model?: string
  sendsPayloads?: boolean
}) {
  const [turns, setTurns] = React.useState<Turn[]>([])
  const [draft, setDraft] = React.useState("")
  const inputRef = React.useRef<HTMLInputElement | null>(null)
  const endRef = React.useRef<HTMLDivElement | null>(null)

  /* A deep link may open the drawer with one exchange already answered, so the
     screen is screenshot-able without a scripted click. */
  React.useEffect(() => {
    if (!open || window.location.hash !== "#chat-answer") return
    setTurns((prev) =>
      prev.length
        ? prev
        : [
            { kind: "user", text: SUGGESTIONS[0].label },
            {
              kind: "agent",
              text: SUGGESTIONS[0].answer,
              evidence: SUGGESTIONS[0].evidence,
              propose: SUGGESTIONS[0].propose,
            },
          ]
    )
  }, [open])

  React.useEffect(() => {
    if (!open) return
    const id = window.setTimeout(() => inputRef.current?.focus(), 80)
    return () => window.clearTimeout(id)
  }, [open])

  React.useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end" })
  }, [turns])

  React.useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose()
    }
    document.addEventListener("keydown", onKey)
    return () => document.removeEventListener("keydown", onKey)
  }, [open, onClose])

  function ask(s: Suggestion) {
    setTurns((prev) => [
      ...prev,
      { kind: "user", text: s.label },
      { kind: "agent", text: s.answer, evidence: s.evidence, propose: s.propose },
    ])
  }

  function send() {
    const text = draft.trim()
    if (!text) return
    setDraft("")
    setTurns((prev) => [
      ...prev,
      { kind: "user", text },
      {
        kind: "agent",
        text: "No model is connected in this prototype, so I cannot answer that one. In the real build this question goes to the provider the admin configured, with the same read-and-propose tools you see here.",
        evidence: [{ k: "Provider", v: "not configured" }],
      },
    ])
  }

  if (!open) return null

  return (
    <>
      <div
        className="fixed inset-0 z-40 bg-[hsl(220_30%_4%/0.35)] animate-in fade-in-0 duration-150"
        onClick={onClose}
        aria-hidden="true"
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Ask Marites"
        className="fixed right-0 top-0 z-50 flex h-full w-[min(440px,100vw)] flex-col border-l bg-card shadow-2xl animate-in slide-in-from-right duration-200"
      >
        <header className="flex shrink-0 items-center gap-2.5 border-b px-4 py-3">
          <span className="grid h-7 w-7 place-items-center rounded-full bg-primary/12">
            <Bot className="h-4 w-4 text-primary" />
          </span>
          <div className="min-w-0 flex-1 leading-tight">
            <div className="text-[13.5px] font-semibold tracking-[-0.01em]">Ask Marites</div>
            <div className="flex items-center gap-1.5 text-[10.5px] text-muted-foreground">
              {configured ? (
                <>
                  <span className="font-mono">{model}</span>
                  <span aria-hidden="true">·</span>
                  <span>{sendsPayloads ? "reads message bodies" : "headers and reasons only"}</span>
                </>
              ) : (
                <span>no model connected</span>
              )}
            </div>
          </div>
          <Button variant="ghost" size="icon" onClick={onClose} aria-label="Close assistant">
            <X className="h-4 w-4" />
          </Button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
          {!configured ? (
            <div className="pt-8 text-center">
              <span className="mx-auto grid h-10 w-10 place-items-center rounded-full bg-secondary">
                <Bot className="h-5 w-5 text-muted-foreground" />
              </span>
              <h3 className="mt-3 text-[14px] font-semibold tracking-[-0.01em]">No model connected</h3>
              <p className="mx-auto mt-1.5 max-w-[34ch] text-[12.5px] leading-relaxed text-muted-foreground">
                The assistant needs a model before it can answer. Nothing is sent anywhere until one
                is connected — not a question, not a message, not a count.
              </p>
              {canConfigure ? (
                <Button className="mt-4" size="sm" onClick={onConfigure}>
                  Connect a model
                </Button>
              ) : (
                <p className="mx-auto mt-4 max-w-[32ch] text-[11.5px] leading-relaxed text-muted-foreground/80">
                  Ask an administrator to connect one under{" "}
                  <b className="text-foreground/80">Setup → AI assistant</b>.
                </p>
              )}
            </div>
          ) : turns.length === 0 ? (
            <div className="pt-6">
              <p className="text-[13px] leading-relaxed text-muted-foreground">
                I can read every queue, message and metric on this install, and I can prepare an
                action for you. I cannot send anything — that stays with you.
              </p>
              <div className="mt-4 space-y-1.5">
                {SUGGESTIONS.map((s) => (
                  <button
                    key={s.id}
                    onClick={() => ask(s)}
                    className="w-full rounded-lg border bg-card px-3 py-2.5 text-left text-[12.5px] transition-colors hover:border-primary/40 hover:bg-accent/40"
                  >
                    {s.label}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              {turns.map((t, i) =>
                t.kind === "user" ? (
                  <div key={i} className="flex justify-end">
                    <div className="max-w-[85%] rounded-2xl rounded-br-md bg-primary px-3 py-2 text-[13px] text-primary-foreground">
                      {t.text}
                    </div>
                  </div>
                ) : (
                  <div key={i} className="space-y-2.5">
                    <div className="flex items-start gap-2">
                      <span className="mt-0.5 grid h-5 w-5 shrink-0 place-items-center rounded-full bg-primary/12">
                        <Bot className="h-3 w-3 text-primary" />
                      </span>
                      <p className="text-[13px] leading-relaxed">{t.text}</p>
                    </div>

                    {t.evidence && (
                      <div className="ml-7 overflow-hidden rounded-lg border">
                        {t.evidence.map((e, j) => (
                          <div
                            key={e.k}
                            className={cn(
                              "flex items-baseline justify-between gap-3 px-2.5 py-1.5 text-[11.5px]",
                              j > 0 && "border-t"
                            )}
                          >
                            <span className="text-muted-foreground">{e.k}</span>
                            <span className="text-right font-medium">{e.v}</span>
                          </div>
                        ))}
                      </div>
                    )}

                    {t.propose && (
                      <div className="ml-7 rounded-lg border border-primary/25 bg-accent/45 p-2.5">
                        <div className="flex items-center gap-2 text-[12px] font-semibold">
                          Replayed {t.propose.count} messages
                          <Badge variant="outline" className="px-1.5 py-0 text-[9.5px] uppercase">
                            done
                          </Badge>
                        </div>
                        <p className="mt-0.5 text-[11.5px] leading-relaxed text-muted-foreground">
                          Sent as you, with your credential. The audit row names you, not me.
                        </p>
                        <Button
                          size="sm"
                          variant="outline"
                          className="mt-2 h-7 px-2.5 text-[11.5px]"
                          onClick={onOpenQueue}
                        >
                          Show {t.propose.count}
                        </Button>
                      </div>
                    )}
                  </div>
                )
              )}
              <div ref={endRef} />
            </div>
          )}
        </div>

        <footer className="shrink-0 border-t p-3">
          <div className="flex items-center gap-2">
            <input
              ref={inputRef}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") send()
              }}
              placeholder={configured ? "Ask about a queue, a failure, a trend…" : "No model connected"}
              disabled={!configured}
              className="h-9 flex-1 rounded-md border border-input bg-secondary/60 px-3 text-[13px] outline-none transition-colors placeholder:text-muted-foreground/70 focus:border-ring focus:bg-card focus:ring-[3px] focus:ring-ring/20 disabled:cursor-not-allowed disabled:opacity-60"
            />
            <Button
              size="icon"
              onClick={send}
              aria-label="Send"
              disabled={!configured || !draft.trim()}
            >
              <CornerDownLeft className="h-4 w-4" />
            </Button>
          </div>
          <p className="mt-1.5 text-[10.5px] leading-relaxed text-muted-foreground">
            {!configured
              ? "Nothing is sent anywhere until an administrator connects a model."
              : sendsPayloads
                ? "Message bodies are sent to the configured provider. An admin turned this on."
                : "Headers, reasons and counts are sent to the configured provider. Bodies stay here."}
          </p>
        </footer>
      </aside>
    </>
  )
}
