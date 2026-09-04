"use client";

import { createContext, useContext, useEffect, useState } from "react";
import {
  applyTheme,
  readStoredAccent,
  readStoredMode,
  THEME_ACCENT_KEY,
  THEME_MODE_KEY,
  type ThemeAccent,
  type ThemeMode,
} from "@/lib/theme";

interface ThemeContextValue {
  mode: ThemeMode;
  accent: ThemeAccent;
  setMode: (mode: ThemeMode) => void;
  setAccent: (accent: ThemeAccent) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  // Lazy initializers, not a mount effect + setState: the boot script (see
  // layout.tsx) already applied the stored theme to <html> before hydration, and
  // this whole shell only ever renders once a client-side auth check has passed,
  // so there's no meaningful server-rendered markup for this to mismatch against.
  const [mode, setModeState] = useState<ThemeMode>(readStoredMode);
  const [accent, setAccentState] = useState<ThemeAccent>(readStoredAccent);

  useEffect(() => {
    applyTheme(mode, accent);
  }, [mode, accent]);

  function setMode(next: ThemeMode) {
    setModeState(next);
    try {
      window.localStorage.setItem(THEME_MODE_KEY, next);
    } catch {
      /* storage unavailable — the choice just won't survive a reload */
    }
  }

  function setAccent(next: ThemeAccent) {
    setAccentState(next);
    try {
      window.localStorage.setItem(THEME_ACCENT_KEY, next);
    } catch {
      /* storage unavailable — the choice just won't survive a reload */
    }
  }

  return (
    <ThemeContext.Provider value={{ mode, accent, setMode, setAccent }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
