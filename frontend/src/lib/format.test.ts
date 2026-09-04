import { describe, expect, it } from "vitest";
import { formatDate, formatINR, parseAmount } from "./format";

describe("formatINR", () => {
  it("formatINR_UsesLakhGrouping", () => {
    expect(formatINR(115000)).toBe("₹1,15,000.000");
  });

  it("groups crores and keeps three decimals", () => {
    expect(formatINR(12345678.5)).toBe("₹1,23,45,678.500");
  });

  it("formats zero and negatives", () => {
    expect(formatINR(0)).toBe("₹0.000");
    expect(formatINR(-2500)).toBe("-₹2,500.000");
  });
});

describe("formatDate", () => {
  it("formats a YYYY-MM-DD string with no timezone drift", () => {
    expect(formatDate("2026-04-03")).toBe("03 Apr 2026");
    expect(formatDate("2026-03-31")).toBe("31 Mar 2026");
  });

  it("accepts an ISO datetime and uses its date part", () => {
    expect(formatDate("2026-01-01T18:30:00.000Z")).toBe("01 Jan 2026");
  });

  it("returns the input unchanged when it is not a date", () => {
    expect(formatDate("not-a-date")).toBe("not-a-date");
  });
});

describe("parseAmount", () => {
  it("strips the currency symbol and grouping commas", () => {
    expect(parseAmount("₹1,15,000.00")).toBe(115000);
    expect(parseAmount("2,00,000")).toBe(200000);
  });

  it("honours trailing Cr / Dr markers", () => {
    expect(parseAmount("5,000.00 Cr")).toBe(5000);
    expect(parseAmount("2,00,000.00 Dr")).toBe(-200000);
  });

  it("returns NaN for non-numeric input", () => {
    expect(Number.isNaN(parseAmount("abc"))).toBe(true);
    expect(Number.isNaN(parseAmount(""))).toBe(true);
  });
});
