import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { applyCurrencyShortcut, CurrencyField, formatBrlCurrency, parseBrlCurrency } from "./CurrencyField";
import { useState } from "react";

describe("CurrencyField", () => {
  it("formats zero, cents and thousands and clears safely", () => {
    expect(formatBrlCurrency(0)).toContain("0,00");
    expect(formatBrlCurrency(1250)).toContain("1.250,00");
    expect(parseBrlCurrency("R$ 0,25")).toBe(.25);
    expect(parseBrlCurrency("")).toBeNull();
  });
  it("applies fixed shortcuts from empty and never becomes negative", () => {
    expect(applyCurrencyShortcut(null, 100)).toBe(100);
    expect(applyCurrencyShortcut(500, 50)).toBe(550);
    expect(applyCurrencyShortcut(25, -100)).toBe(0);
  });
  it("emits the numeric decimal during progressive typing", async () => {
    const change = vi.fn();
    function Harness() { const [value, setValue] = useState<number | null>(null); return <CurrencyField label="Valor" value={value} onValueChange={next => { change(next); setValue(next); }} />; }
    render(<Harness />);
    await userEvent.type(screen.getByLabelText("Valor"), "125000");
    expect(change).toHaveBeenLastCalledWith(1250);
  });
});
