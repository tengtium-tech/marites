import { cn } from "@/lib/utils"

/**
 * The Marites mark: an ear, because a Marites is the neighbour who always has
 * the latest. It listens to traffic rather than gossip.
 */
export function MaritesMark({
  size = 28,
  className,
}: {
  size?: number
  className?: string
}) {
  const glyph = Math.round(size * 0.62)
  return (
    <span
      aria-hidden="true"
      className={cn("relative grid shrink-0 place-items-center rounded-[30%]", className)}
      style={{
        width: size,
        height: size,
        background: "linear-gradient(150deg, #6079FF 0%, #2F4DE0 100%)",
        boxShadow:
          "0 4px 12px -4px rgba(47,77,224,0.55), inset 0 1px 0 rgba(255,255,255,0.22)",
      }}
    >
      <svg
        width={glyph}
        height={glyph}
        viewBox="0 0 24 24"
        fill="none"
        stroke="#fff"
        strokeWidth={2.2}
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M6 8.5a6.5 6.5 0 1 1 13 0c0 6-6 6-6 10a3.5 3.5 0 1 1-7 0" />
        <path d="M15 8.5a2.5 2.5 0 0 0-5 0v1a2 2 0 1 1 0 4" />
      </svg>
    </span>
  )
}
