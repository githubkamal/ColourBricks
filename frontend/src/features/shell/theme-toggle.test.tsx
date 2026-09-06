import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { ThemeProvider } from "./theme-context";
import { ThemeToggle } from "./theme-toggle";

function renderToggle() {
  return render(
    <ThemeProvider>
      <ThemeToggle />
    </ThemeProvider>,
  );
}

describe("ThemeToggle", () => {
  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.classList.remove("dark");
    document.documentElement.removeAttribute("data-accent");
  });

  afterEach(() => {
    document.documentElement.classList.remove("dark");
    document.documentElement.removeAttribute("data-accent");
  });

  it("ThemeToggle_SwitchingMode_TogglesTheDarkClassAndPersists", () => {
    renderToggle();

    expect(document.documentElement.classList.contains("dark")).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "Switch to dark mode" }));

    expect(document.documentElement.classList.contains("dark")).toBe(true);
    expect(window.localStorage.getItem("cb.themeMode")).toBe("dark");

    fireEvent.click(screen.getByRole("button", { name: "Switch to light mode" }));
    expect(document.documentElement.classList.contains("dark")).toBe(false);
    expect(window.localStorage.getItem("cb.themeMode")).toBe("light");
  });

  it("ThemeToggle_PickingAnAccent_SetsTheDataAttributeAndPersists", () => {
    renderToggle();

    fireEvent.click(screen.getByRole("button", { name: "Choose accent colour" }));
    fireEvent.click(screen.getByRole("button", { name: "Brick" }));

    expect(document.documentElement.getAttribute("data-accent")).toBe("brick");
    expect(window.localStorage.getItem("cb.themeAccent")).toBe("brick");
  });

  it("ThemeToggle_DefaultsToLightAndSlate_WhenNothingStored", () => {
    renderToggle();

    expect(document.documentElement.classList.contains("dark")).toBe(false);
    expect(document.documentElement.getAttribute("data-accent")).toBe("slate");
  });
});
