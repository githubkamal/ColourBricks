/**
 * A muted trigger never reappears in `GET /notifications` (filtered server-side
 * before the caller sees it), so there's no way to list or unmute one from the
 * API alone. This mirrors the mute/unmute calls into localStorage purely so the
 * notifications page can show "here's what you've muted" and offer an undo —
 * it's a display convenience, not the source of truth (the server-side mute row
 * is), and if it's ever out of sync the worst case is a stale list entry.
 */
const KEY = "cb.mutedNotificationTriggers";

export function getMutedTriggers(): string[] {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as string[]) : [];
  } catch {
    return [];
  }
}

export function rememberMuted(trigger: string): void {
  try {
    const next = [...new Set([...getMutedTriggers(), trigger])];
    localStorage.setItem(KEY, JSON.stringify(next));
  } catch {
    /* storage unavailable — the mute itself still went through */
  }
}

export function forgetMuted(trigger: string): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(getMutedTriggers().filter((t) => t !== trigger)));
  } catch {
    /* storage unavailable */
  }
}
