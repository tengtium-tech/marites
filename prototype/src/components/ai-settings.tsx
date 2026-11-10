import * as React from "react"
import { Bot, Lock, Server } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"

type Provider = "azure" | "openai" | "anthropic" | "local"

const PROVIDERS: { id: Provider; label: string; model: string; baseUrl: string; key: boolean }[] = [
  { id: "azure", label: "Azure OpenAI", model: "gpt-4o-mini", baseUrl: "https://acme.openai.azure.com", key: true },
  { id: "openai", label: "OpenAI", model: "gpt-4o-mini", baseUrl: "https://api.openai.com/v1", key: true },
  { id: "anthropic", label: "Anthropic", model: "claude-sonnet-4-5", baseUrl: "https://api.anthropic.com", key: true },
  { id: "local", label: "Local (Ollama)", model: "llama3.1:8b", baseUrl: "http://ollama.internal:11434", key: false },
]

export function AiSettingsScreen({
  onOpenChat,
  onSaved,
}: {
  onOpenChat: () => void
  onSaved: (cfg: { model: string; sendsPayloads: boolean }) => void
}) {
  const [provider, setProvider] = React.useState<Provider>("local")
  const [model, setModel] = React.useState("llama3.1:8b")
  const [baseUrl, setBaseUrl] = React.useState("http://ollama.internal:11434")
  const [sendsPayloads, setSendsPayloads] = React.useState(false)
  const [tested, setTested] = React.useState(false)
  const [saved, setSaved] = React.useState(false)

  const meta = PROVIDERS.find((p) => p.id === provider)!

  function pick(id: Provider) {
    const p = PROVIDERS.find((x) => x.id === id)!
    setProvider(id)
    setModel(p.model)
    setBaseUrl(p.baseUrl)
    setTested(false)
  }

  return (
    <div className="animate-in fade-in slide-in-from-bottom-1 duration-300">
      <div className="mb-5">
        <h1 className="text-[22px] font-semibold tracking-[-0.025em]">AI assistant</h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          Bring your own model. The assistant reads and proposes; it never executes — every action it
          suggests is approved by a person.
        </p>
      </div>

      <div className="grid grid-cols-[1.1fr_1fr] items-start gap-4 max-[980px]:grid-cols-1">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Provider</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3.5 pt-0">
            <div>
              <Label>Where the model runs</Label>
              <div className="mt-1.5 grid grid-cols-2 gap-2">
                {PROVIDERS.map((p) => (
                  <button
                    key={p.id}
                    onClick={() => pick(p.id)}
                    aria-pressed={provider === p.id}
                    className={cn(
                      "rounded-md border bg-card px-2.5 py-2 text-left text-[12.5px] transition-colors",
                      provider === p.id
                        ? "border-primary bg-accent font-semibold text-accent-foreground"
                        : "text-muted-foreground hover:bg-secondary"
                    )}
                  >
                    <span className="flex items-center gap-1.5">
                      {p.id === "local" ? <Server className="h-3 w-3" /> : null}
                      {p.label}
                    </span>
                    {p.id === "local" && (
                      <span className="mt-0.5 block text-[10px] font-normal opacity-80">
                        nothing leaves your network
                      </span>
                    )}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <Label htmlFor="ai-base">Base URL</Label>
              <Input
                id="ai-base"
                value={baseUrl}
                onChange={(e) => {
                  setBaseUrl(e.target.value)
                  setTested(false)
                }}
                className="mt-1.5 font-mono text-[12px]"
              />
            </div>

            <div>
              <Label htmlFor="ai-model">Model</Label>
              <Input
                id="ai-model"
                value={model}
                onChange={(e) => {
                  setModel(e.target.value)
                  setTested(false)
                }}
                className="mt-1.5 font-mono text-[12px]"
              />
            </div>

            {meta.key ? (
              <div>
                <Label htmlFor="ai-key">API key</Label>
                <Input
                  id="ai-key"
                  type="password"
                  defaultValue="sk-••••••••••••••••"
                  className="mt-1.5 font-mono text-[12px]"
                />
                <p className="mt-1.5 flex items-center gap-1.5 text-[11.5px] text-muted-foreground">
                  <Lock className="h-3 w-3" /> Write-only, encrypted per row, never returned by any
                  endpoint — the same rule as the broker credentials.
                </p>
              </div>
            ) : (
              <div className="rounded-lg border border-success/30 bg-success/[0.06] px-3 py-2.5 text-[12px] leading-relaxed text-success">
                No key required. A local model keeps every message inside your network, which is the
                only acceptable option for an air-gapped or regulated install.
              </div>
            )}

            <div className="flex items-start gap-2.5 rounded-lg border bg-secondary/40 px-3 py-2.5">
              <Checkbox
                id="ai-payloads"
                checked={sendsPayloads}
                onCheckedChange={(v) => setSendsPayloads(Boolean(v))}
                className="mt-0.5"
              />
              <label htmlFor="ai-payloads" className="cursor-pointer text-[12px] leading-relaxed">
                <span className="font-medium">Let the assistant read message bodies</span>
                <span className="mt-0.5 block text-muted-foreground">
                  Off by default. With it off, the assistant sees headers, death reasons and counts —
                  enough to answer most questions. Turning it on sends payloads to the provider.
                </span>
              </label>
            </div>

            {tested && (
              <div className="rounded-lg border border-success/30 bg-success/[0.06] px-3 py-2.5 text-[12px] text-success">
                Connected · {model} · 9 tools available · payloads {sendsPayloads ? "included" : "excluded"}
              </div>
            )}

            <div className="flex flex-wrap gap-2.5 border-t pt-3.5">
              <Button variant="outline" onClick={() => setTested(true)} disabled={tested}>
                {tested ? "Connected" : "Test connection"}
              </Button>
              <Button
                disabled={!tested || saved}
                onClick={() => {
                  onSaved({ model, sendsPayloads })
                  setSaved(true)
                }}
              >
                {saved ? "Assistant enabled" : "Save & enable"}
              </Button>
              <Button variant="ghost" onClick={onOpenChat}>
                Open the assistant
              </Button>
            </div>
            {saved && (
              <p className="text-[11.5px] leading-relaxed text-muted-foreground">
                <b className="text-foreground/80">Ask Marites</b> is now available in the header for
                Support and Admin. Viewers never see it.
              </p>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle>What it can do</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <div className="space-y-1.5">
                {[
                  ["list_queues", "read"],
                  ["get_queue", "read"],
                  ["search_messages", "read"],
                  ["get_metrics", "read"],
                  ["explain_failure", "read"],
                  ["propose_replay", "propose"],
                  ["propose_discard", "propose"],
                  ["propose_publish", "propose"],
                  ["propose_purge", "propose"],
                ].map(([name, kind]) => (
                  <div key={name} className="flex items-center justify-between gap-3">
                    <span className="font-mono text-[11.5px]">{name}</span>
                    <Badge
                      variant={kind === "read" ? "secondary" : "warning"}
                      className="px-1.5 py-0 text-[9.5px] uppercase"
                    >
                      {kind}
                    </Badge>
                  </div>
                ))}
              </div>
              <p className="mt-3 border-t pt-3">
                There is no <span className="font-mono text-[11.5px]">replay</span>, no{" "}
                <span className="font-mono text-[11.5px]">purge</span>, no{" "}
                <span className="font-mono text-[11.5px]">publish</span>. The credential the assistant
                holds cannot perform them, so the missing tool is not the only thing stopping it.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle>What leaves the building</CardTitle>
            </CardHeader>
            <CardContent className="pt-0 text-[12.5px] leading-relaxed text-muted-foreground">
              <p>
                With a hosted provider, the questions and the evidence the assistant reads go to that
                provider. Broker credentials never do — the assistant talks to Marites, not to the
                broker.
              </p>
              <p className="mt-3 border-t pt-3">
                <b className="text-foreground/80">With a local model, nothing leaves.</b> Same
                assistant, same tools, no third party — which is why it is the default here.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
