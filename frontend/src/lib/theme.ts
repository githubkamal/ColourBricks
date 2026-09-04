/** Light/dark toggle and a curated accent-color palette (client request, 2026-09-04). */

export type ThemeMode = "light" | "dark";
export type ThemeAccent = "slate" | "blue" | "emerald" | "amber" | "violet";

export const THEME_MODE_KEY = "cb.themeMode";
export const THEME_ACCENT_KEY = "cb.themeAccent";

export const DEFAULT_MODE: ThemeMode = "light";
export const DEFAULT_ACCENT: ThemeAccent = "slate";

export const ACCENTS: { id: ThemeAccent; label: string; swatch: string }[] = [
  { id: "slate", label: "Slate", swatch: "#1a1d21" },
  { id: "blue", label: "Blue", swatch: "#2563eb" },
  { id: "emerald", label: "Emerald", swatch: "#059669" },
  { id: "amber", label: "Amber", swatch: "#c9820a" },
  { id: "violet", label: "Violet", swatch: "#7c3aed" },
];

/** Applies mode/accent to the document root — shared by the anti-flash boot script and the live toggle. */
export function applyTheme(mode: ThemeMode, accent: ThemeAccent) {
  const root = document.documentElement;
  root.classList.toggle("dark", mode === "dark");
  root.setAttribute("data-accent", accent);
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
  } catch (e) {}
})();
`;
