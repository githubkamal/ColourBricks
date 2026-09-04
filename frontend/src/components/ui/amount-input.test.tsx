import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { AmountInput } from "./amount-input";

function Controlled({ error }: { error?: string }) {
  const [value, setValue] = useState("");
  return <AmountInput aria-label="Amount" value={value} onChange={setValue} error={error} />;
}

/**
 * Fires one change event per character, appending to whatever the input's value
 * actually became after the previous keystroke — a rejected keystroke leaves the
 * value unchanged for the next one, exactly like a real controlled input reverting
 * a browser's tentative edit.
 */
function typeChars(input: HTMLInputElement, chars: string) {
  for (const ch of chars) {
    fireEvent.change(input, { target: { value: input.value + ch } });
  }
}

describe("AmountInput", () => {
  it("AmountInput_AcceptsDigitsAndOneDecimalPoint", () => {
    render(<Controlled />);
    const input = screen.getByLabelText("Amount") as HTMLInputElement;

    typeChars(input, "12345.67");

    expect(input).toHaveValue("12345.67");
  });

  it("AmountInput_DropsLetterKeystrokes_ButKeepsTypingDigitsAfterThem", () => {
    render(<Controlled />);
    const input = screen.getByLabelText("Amount") as HTMLInputElement;

    // Simulates typing "12a3b.4c5" one keystroke at a time — each letter keystroke
    // is rejected on its own (the input reverts, like a real controlled field), so
    // the letters never land, but every digit and the single "." still do.
    typeChars(input, "12a3b.4c5");

    expect(input).toHaveValue("123.45");
  });

  it("AmountInput_RejectsASecondDecimalPoint", () => {
    render(<Controlled />);
    const input = screen.getByLabelText("Amount") as HTMLInputElement;

    typeChars(input, "1.2.3");

    expect(input).toHaveValue("1.23");
  });

  it("AmountInput_ShowsInlineError_WhenGiven", () => {
    render(<Controlled error="Must be greater than zero" />);
    expect(screen.getByText("Must be greater than zero")).toBeInTheDocument();
    expect(screen.getByLabelText("Amount")).toHaveAttribute("aria-invalid", "true");
  });

  it("AmountInput_CapsIntegerPartAt15Digits_TheDecimal18_3Limit", () => {
    render(<Controlled />);
    const input = screen.getByLabelText("Amount") as HTMLInputElement;

    // 16 digits — the 16th is rejected, matching DECIMAL(18,3)'s 15-digit integer part.
    typeChars(input, "1234567890123456");

    expect(input).toHaveValue("123456789012345");
  });

  it("AmountInput_CapsDecimalPartAtThreeDigits", () => {
    render(<Controlled />);
    const input = screen.getByLabelText("Amount") as HTMLInputElement;

    typeChars(input, "100.9999");

    expect(input).toHaveValue("100.999");
  });
});
