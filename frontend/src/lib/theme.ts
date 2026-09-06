/** Light/dark toggle and a curated accent-color palette (client request, 2026-09-04). */

export type ThemeMode = "light" | "dark";
export type ThemeAccent =
  | "slate"
  | "blue"
  | "emerald"
  | "amber"
  | "violet"
  | "teal"
  | "rose"
  | "indigo"
  | "orange"
  | "sunset"
  | "ocean"
  | "aurora";

export const THEME_MODE_KEY = "cb.themeMode";
export const THEME_ACCENT_KEY = "cb.themeAccent";

export const DEFAULT_MODE: ThemeMode = "light";
export const DEFAULT_ACCENT: ThemeAccent = "slate";

export const ACCENTS: {
  id: ThemeAccent;
  label: string;
  swatch: string;
  /** Decorative only (theme-picker swatch, sidebar logo badge) — every
   * functional use (buttons, badges, focus rings, …) stays the solid
   * `swatch` colour, since `background-color` can't hold a gradient. */
  gradient?: string;
}[] = [
  { id: "slate", label: "Slate", swatch: "#188ae2" },
  { id: "blue", label: "Blue", swatch: "#2563eb" },
  { id: "emerald", label: "Emerald", swatch: "#059669" },
  { id: "amber", label: "Amber", swatch: "#c9820a" },
  { id: "violet", label: "Violet", swatch: "#7c3aed" },
  { id: "teal", label: "Teal", swatch: "#0d9488" },
  { id: "rose", label: "Rose", swatch: "#e11d48" },
  { id: "indigo", label: "Indigo", swatch: "#4f46e5" },
  { id: "orange", label: "Orange", swatch: "#ea580c" },
  {
    id: "sunset",
    label: "Sunset",
    swatch: "#e2506b",
    gradient: "linear-gradient(135deg, #f0a020 0%, #e2506b 60%, #c026d3 100%)",
  },
  {
    id: "ocean",
    label: "Ocean",
    swatch: "#0891b2",
    gradient: "linear-gradient(135deg, #2563eb 0%, #0891b2 55%, #059669 100%)",
  },
  {
    id: "aurora",
    label: "Aurora",
    swatch: "#7c3aed",
    gradient: "linear-gradient(135deg, #4f46e5 0%, #7c3aed 55%, #db2777 100%)",
  },
];

/** Applies mode/accent to the document root — shared by the anti-flash boot script and the live toggle. */
export function applyTheme(mode: ThemeMode, accent: ThemeAccent) {
  const root = document.documentElement;
  root.classList.toggle("dark", mode === "dark");
  root.setAttribute("data-accent", accent);
  root.style.colorScheme = mode;
  const themeColorMeta = document.querySelector('meta[name="theme-color"]');
  if (themeColorMeta) {
    themeColorMeta.setAttribute("content", mode === "dark" ? "#1c1d27" : "#f0f4f7");
  }
}

export function readStoredMode(): ThemeMode {
  try {
    const v = window.localStorage.getItem(THEME_MODE_KEY);
    return v === "dark" ? "dark" : DEFAULT_MODE;
  } catch {
    return DEFAULT_MODE;
  }
}

export function readStoredAccent(): ThemeAccent {
  try {
    const v = window.localStorage.getItem(THEME_ACCENT_KEY);
    return ACCENTS.some((a) => a.id === v) ? (v as ThemeAccent) : DEFAULT_ACCENT;
  } catch {
    return DEFAULT_ACCENT;
  }
}

/**
 * Source for the inline anti-flash boot script (inlined as a plain string, not
 * imported, since it must run before React/any module graph loads).
 */
export const THEME_BOOT_SCRIPT = `
(function () {
  try {
    var mode = localStorage.getItem(${JSON.stringify(THEME_MODE_KEY)});
    var accent = localStorage.getItem(${JSON.stringify(THEME_ACCENT_KEY)}) || ${JSON.stringify(DEFAULT_ACCENT)};
    var root = document.documentElement;
    if (mode === "dark") root.classList.add("dark");
    root.setAttribute("data-accent", accent);
    root.style.colorScheme = mode === "dark" ? "dark" : "light";
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta && mode === "dark") meta.setAttribute("content", "#1c1d27");
  } catch (e) {}
})();
`;
