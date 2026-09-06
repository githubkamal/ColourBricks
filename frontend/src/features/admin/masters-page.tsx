import Link from "next/link";

interface MasterLink {
  label: string;
  href: string;
}

interface MasterGroup {
  title: string;
  items: MasterLink[];
}

// Every master-data screen already lives under its own domain section (Vendors,
// Materials, Labour, ...) rather than centralized here — this is a jump-page to all
// of them, not a duplicate management screen. "Masters" as a distinct admin
// sub-application isn't specified anywhere in the BRD beyond the nav label itself.
const GROUPS: MasterGroup[] = [
  {
    title: "Projects & parties",
    items: [
      { label: "Project Master", href: "/projects" },
      { label: "Vendor Master", href: "/vendors" },
      { label: "Field Officer Master", href: "/field-officers" },
      { label: "Temple Master", href: "/donations/temples" },
    ],
  },
  {
    title: "Labour",
    items: [
      { label: "Departments", href: "/labour/departments" },
      { label: "Teams", href: "/labour/teams" },
    ],
  },
  {
    title: "Materials",
    items: [
      { label: "Item Master", href: "/materials" },
      { label: "Item Categories", href: "/materials/categories" },
    ],
  },
  {
    title: "Loans",
    items: [{ label: "Loan Master", href: "/loans" }],
  },
  {
    title: "Accounts",
    items: [
      { label: "Cash Accounts", href: "/accounts/cash" },
      { label: "Bank Accounts", href: "/accounts/bank" },
      { label: "Payment Modes", href: "/accounts/payment-modes" },
    ],
  },
  {
    title: "Access",
    items: [
      { label: "Users", href: "/admin/users" },
      { label: "Roles & Permissions", href: "/admin/roles" },
    ],
  },
];

/** Admin > Masters: a single jump-page to every master-data screen in the system. */
export function MastersPage() {
  return (
    <div className="max-w-3xl space-y-6">
      <div>
        <h1 className="text-lg font-semibold">Masters</h1>
        <p className="text-muted-foreground text-sm">
          Every master list lives under its own section — this page is a shortcut to all of them in
          one place.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {GROUPS.map((group) => (
          <div key={group.title} className="bg-card rounded border p-4">
            <h2 className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">
              {group.title}
            </h2>
            <ul className="space-y-1">
              {group.items.map((item) => (
                <li key={`${item.href}:${item.label}`}>
                  <Link href={item.href} className="text-primary text-sm hover:underline">
                    {item.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </div>
  );
}
