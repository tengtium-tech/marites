import * as React from "react"
import { Check, Copy } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { cn } from "@/lib/utils"

/** The four operations that carry a body, plus peek, and how each one is labelled. */
export type PayloadAction = "replay" | "discard" | "purge" | "publish" | "peek"

export type PayloadView = {
  action: PayloadAction
  queue: string
  messageId: string
  size: string
  /** Absent before the operation runs — a preview has no hash yet. */
  hash?: string
  retainedUntil?: string
  before: string
  after?: string
  /** True when opened before the operation runs — a preview, not evidence. */
  preview?: boolean
}

export const PAYLOAD_LABEL: Record<
  PayloadAction,
  { past: string; before: string; after: string; fate: "sent" | "destroyed" | "returned" }
> = {
  replay: {
    past: "replayed",
    before: "Before — as dead-lettered",
    after: "Replayed — as sent",
    fate: "sent",
  },
  discard: { past: "discarded", before: "Discarded — as destroyed", after: "", fate: "destroyed" },
  purge: { past: "purged", before: "Purged — as destroyed", after: "", fate: "destroyed" },
  publish: { past: "published", before: "Published — as sent", after: "", fate: "sent" },
  peek: { past: "peeked", before: "Peeked — as returned", after: "", fate: "returned" },
}

/** Lines that differ between the two versions, so an edit is visible, not inferred. */
export function diffLines(before: string, after: string) {
  const b = before.split("\n")
  const a = after.split("\n")
  const changed = new Set<number>()
  for (let i = 0; i < Math.max(a.length, b.length); i++) if (a[i] !== b[i]) changed.add(i)
  return { b, a, changed }
}

/**
 * One body, with a line-number gutter. `sign` marks a changed line `-` or `+`,
 * the way a diff does, so the change survives being read in greyscale.
 */
function CodePane({
  label,
  code,
  changed,
  sign,
  note,
}: {
  label: string
  code: string
  changed?: Set<number>
  sign?: "-" | "+"
  note?: string
}) {
  const lines = code.split("\n")
  return (
    <div className="flex min-w-0 flex-col overflow-hidden rounded-lg border bg-card">
      <div className="flex items-center gap-2 border-b bg-secondary/50 px-3 py-1.5">
        <span className="text-[10px] font-semibold uppercase tracking-[0.07em] text-muted-foreground">
          {label}
        </span>
        {note && <span className="text-[10.5px] text-muted-foreground">{note}</span>}
        <span className="ml-auto font-mono text-[10px] text-muted-foreground">
          {lines.length} {lines.length === 1 ? "line" : "lines"}
        </span>
      </div>
      <div className="overflow-auto">
        <div className="min-w-max py-1.5 font-mono text-[11.5px] leading-[1.75]">
          {lines.map((line, i) => {
            const hit = changed?.has(i)
            return (
              <div key={i} className={cn("flex", hit && "bg-warning/12")}>
                <span className="w-10 shrink-0 select-none px-2 text-right text-[10.5px] text-muted-foreground/70">
                  {i + 1}
                </span>
                <span
                  className={cn(
                    "w-4 shrink-0 select-none text-center",
                    hit ? "font-semibold text-warning" : "text-transparent"
                  )}
                >
                  {hit ? sign : "·"}
                </span>
                <span className={cn("whitespace-pre pr-5", hit && "font-semibold text-foreground")}>
                  {line || " "}
                </span>
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}

/**
 * The full body behind an audit row. One pane for the operations that only destroy
 * or only send, two side by side when a replay replaced what was there — which is
 * the only case where "what was sent" and "what it replaced" are different questions.
 */
export function PayloadDialog({
  view,
  onClose,
}: {
  view: PayloadView | null
  onClose: () => void
}) {
  const [copied, setCopied] = React.useState(false)

  React.useEffect(() => {
    setCopied(false)
  }, [view])

  const edited = Boolean(view?.after) && view!.after !== view!.before
  const labels = view ? PAYLOAD_LABEL[view.action] ?? PAYLOAD_LABEL.replay : null
  const { b: beforeLines, a: afterLines, changed } = React.useMemo(
    () => diffLines(view?.before ?? "", view?.after ?? view?.before ?? ""),
    [view]
  )

  async function copy() {
    if (!view) return
    try {
      await navigator.clipboard.writeText(view.after ?? view.before)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1600)
    } catch {
      /* clipboard blocked; the text is on screen and selectable */
    }
  }

  return (
    <Dialog open={Boolean(view)} onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        className={cn(
          "w-[calc(100vw-4rem)]",
          /* two panes need the room; one pane looks abandoned at that width */
          edited ? "max-w-[1040px]" : "max-w-[720px]"
        )}
      >
        {view && labels && (
          <>
            <DialogHeader>
              <DialogTitle className="flex flex-wrap items-center gap-2 text-[15px]">
                <span className="font-mono">{view.messageId}</span>
                <Badge variant="idle">{view.action}</Badge>
                <span className="text-[13px] font-normal text-muted-foreground">
                  {view.queue}
                </span>
                {edited && <Badge variant="warning">payload edited</Badge>}
              </DialogTitle>
              <DialogDescription className="text-[12px]">
                {view.preview
                  ? "This is what will be discarded if you confirm. The body is written to the audit before the message is acknowledged, so the record survives the discard."
                  : edited
                    ? "The body replayed, beside the body it replaced. The marked line is the only difference."
                    : `The complete body ${labels.past}, every line, as it was ${labels.fate}.`}
              </DialogDescription>
            </DialogHeader>

            <div className="max-h-[62vh] overflow-auto px-5 py-4">
              <div className={cn("grid gap-3", edited ? "grid-cols-2" : "grid-cols-1")}>
                {edited && (
                  <CodePane label={labels.before} code={beforeLines.join("\n")} changed={changed} sign="-" />
                )}
                <CodePane
                  label={view.preview ? "Will be discarded — as it stands now" : labels.after || labels.before}
                  code={afterLines.join("\n")}
                  changed={edited ? changed : undefined}
                  sign="+"
                />
              </div>
            </div>

            <DialogFooter className="flex-wrap items-center gap-2 border-t px-5 py-3">
              <span className="mr-auto flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-muted-foreground">
                <span>{view.size}</span>
                {view.retainedUntil && (
                  <span>
                    retained until{" "}
                    <span className="text-foreground/70">{view.retainedUntil}</span>
                  </span>
                )}
                {view.hash ? (
                  <span className="font-mono">sha256 {view.hash}</span>
                ) : (
                  <span>hash computed when the operation is recorded</span>
                )}
              </span>
              <Button variant="outline" size="sm" onClick={copy}>
                {copied ? <Check className="mr-1.5 h-3.5 w-3.5" /> : <Copy className="mr-1.5 h-3.5 w-3.5" />}
                {copied ? "Copied" : "Copy body"}
              </Button>
              <Button size="sm" onClick={onClose}>
                Close
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
