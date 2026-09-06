import { Footer } from "@/features/shell/footer";

const HIGHLIGHTS = [
  "Real-time project P&L, budget vs. actual, on every job",
  "Vendors, subcontractors, loans and EMIs — one ledger",
  "Bank reconciliation with a full, tamper-evident audit trail",
  "Role-based access, notifications and reporting for the whole team",
];

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-1 flex-col">
      <div className="flex flex-1">
        {/* Brand panel — solid colour and typography, not imagery: reliable at
            any screen size and speaks to what the product actually does.
            Hidden below lg, where there's no room for a second column.
            Deliberately a fixed brand colour rather than `bg-primary` — the
            default "slate" accent's primary is a near-black, which read as
            plain black here; a warm brick/terracotta tone (Colour *Bricks*)
            stays the same regardless of which of the five accents a user has
            picked, and never renders as black. */}
        <div
          className="relative hidden flex-1 flex-col justify-between overflow-hidden px-12 py-12 text-white lg:flex xl:px-16"
          style={{ background: "#8a3f2e" }}
        >
          {/* A literal brick-wall mortar pattern — the client's own request,
              and about as low-risk as background texture gets: it's just
              straight repeating lines (horizontal joints, plus vertical
              joints alternating every other row — the "running bond" brick
              pattern), no illustration or geometry to get subtly wrong. */}
          <div
            className="pointer-events-none absolute inset-0"
            style={{
              backgroundImage:
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='60'%3E%3Cpath d='M0 0H120 M0 30H120 M0 30V0 M60 60V30' stroke='white' stroke-opacity='0.09'/%3E%3C/svg%3E\")",
              backgroundSize: "120px 60px",
            }}
          />

          {/* One large, soft, perfectly flat circle — no gradient, no linework,
              nothing that can render wrong — just enough to keep the panel
              from feeling like a flat block of colour. */}
          <div className="pointer-events-none absolute -top-40 -right-40 size-[560px] rounded-full bg-white/[0.07]" />

          <div className="relative flex items-center gap-3">
            {/* Placeholder mark until a real logo file is supplied — a plain
                house glyph in a rounded badge, same treatment a real logo
                would get (drop in an <Image> here once one exists). */}
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-white/10">
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                className="size-6"
                aria-hidden="true"
              >
                <path d="M4 11.5 12 4l8 7.5" strokeLinecap="round" strokeLinejoin="round" />
                <path
                  d="M6 10v9a1 1 0 0 0 1 1h10a1 1 0 0 0 1-1v-9"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
                <path d="M10 20v-5h4v5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </span>
            <span className="font-heading text-3xl font-semibold tracking-tight">
              Colour Bricks
            </span>
          </div>

          <div className="relative -mt-16 max-w-md space-y-8">
            <div className="space-y-2">
              <p className="text-xs font-semibold tracking-widest text-white/60 uppercase">
                Built for construction companies
              </p>
              <h2 className="text-3xl leading-tight font-semibold text-balance">
                Every rupee, every project, one system of record.
              </h2>
            </div>
            <ul className="space-y-4">
              {HIGHLIGHTS.map((line) => (
                <li key={line} className="flex items-start gap-3 text-sm">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2.5"
                    className="mt-0.5 size-4 shrink-0 text-white/70"
                    aria-hidden="true"
                  >
                    <path d="m5 13 4 4L19 7" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  <span className="text-white/90">{line}</span>
                </li>
              ))}
            </ul>
          </div>

          <p className="font-heading relative text-xs text-white/50 italic">
            Construction · Finance · Vendors · Payments · Loans · Roles · Compliance · Reporting
          </p>
        </div>

        {/* Form panel — a border-left defines the seam between the two panels
            without boxing the form itself in a card. Only from `lg` up, same
            breakpoint the brand panel appears at — otherwise it's a stray
            border on the left edge of the viewport with nothing beside it. */}
        <div className="border-border flex flex-1 items-center justify-center p-6 lg:border-l">
          <div className="w-full max-w-sm">{children}</div>
        </div>
      </div>
      <Footer />
    </div>
  );
}
