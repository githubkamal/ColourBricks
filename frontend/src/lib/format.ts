/**
 * Display and parsing helpers, all `en-IN` (plan.md §5.4, §5.5, §8.2).
 * Never compute totals here — format for display only.
 */

// Client request, 2026-09-04: money's decimal precision was raised from 2 to 3 places
// everywhere (matches the backend's Money.Scale / DECIMAL(18,3) columns) — kept as one
// named constant so display and any future precision change stay in lockstep.
export const MONEY_DECIMALS = 3;

const inrFormatter = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  minimumFractionDigits: MONEY_DECIMALS,
  maximumFractionDigits: MONEY_DECIMALS,
});

/** `115000` → `"₹1,15,000.00"` (lakh/crore grouping). */
export function formatINR(value: number): string {
  return inrFormatter.format(value);
}

const MONTHS = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec",
] as const;

const YMD = /^(\d{4})-(\d{2})-(\d{2})/;

/**
 * Formats a business date held as a `YYYY-MM-DD` string (or an ISO datetime whose
 * date part is one) as `DD MMM YYYY`. Deliberately does not go through `new Date()`,
 * which would shift the day across timezones (plan.md §5.5).
 */
export function formatDate(value: string): string {
  const match = YMD.exec(value);
  if (!match) return value;

  const [, year, month, day] = match;
  const monthIndex = Number(month) - 1;
  if (monthIndex < 0 || monthIndex > 11) return value;

  return `${day} ${MONTHS[monthIndex]} ${year}`;
}

/**
 * Parses a user- or statement-entered amount. Strips the `₹` symbol, grouping
 * commas and whitespace, and honours a trailing `Cr` / `Dr` marker
 * (`Dr` → negative). Returns `NaN` for anything else (plan.md §3.1, §8.3).
 */
export function parseAmount(input: string): number {
  if (typeof input !== "string") return NaN;

  let text = input.trim();
  let sign = 1;

  const marker = /\s*(cr|dr)\.?$/i.exec(text);
  if (marker) {
    if (marker[1].toLowerCase() === "dr") sign = -1;
    text = text.slice(0, marker.index);
  }

  text = text.replace(/[₹,\s]/g, "");
  if (text === "" || !/^-?\d*\.?\d+$/.test(text)) return NaN;

  return sign * Number(text);
}
