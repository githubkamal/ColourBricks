import { navigation } from "@/lib/navigation";

/** Placeholder for every navigation destination whose real screen is built in a later phase. */
export default async function PlaceholderPage({ params }: PageProps<"/[...path]">) {
  const { path } = await params;
  const href = `/${path.join("/")}`;

  const label =
    navigation.flatMap((section) => section.items).find((item) => item.href.split("?")[0] === href)
      ?.label ?? href;

  return (
    <div className="space-y-2">
      <h1 className="text-lg font-semibold">{label}</h1>
      <p className="text-muted-foreground text-sm">This screen is built in a later phase.</p>
      <p className="text-muted-foreground font-mono text-xs">{href}</p>
    </div>
  );
}
