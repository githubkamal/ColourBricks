export function Footer() {
  const year = new Date().getFullYear();

  return (
    <footer className="border-border bg-background text-muted-foreground flex h-9 shrink-0 items-center justify-center gap-1 border-t px-4 text-xs">
      <span>
        © {year}{" "}
        <a
          href="https://www.colourbricks.co.in"
          target="_blank"
          rel="noreferrer"
          className="hover:text-foreground underline-offset-2 hover:underline"
        >
          www.colourbricks.co.in
        </a>
      </span>
      <span aria-hidden="true">·</span>
      <span>
        Powered by{" "}
        <a
          href="https://www.livewiresdigitalsolutions.com"
          target="_blank"
          rel="noreferrer"
          className="hover:text-foreground underline-offset-2 hover:underline"
        >
          www.livewiresdigitalsolutions.com
        </a>
      </span>
    </footer>
  );
}
