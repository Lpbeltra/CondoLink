import { describe, expect, it } from "vitest";
import { moneyInput, parseMoneyInput, statusLabel } from "./presentation";
describe("management company request presentation", () => {
  it("uses contextual completed labels", () => {
    expect(statusLabel("Completed", "Fine")).toBe("Processada");
    expect(statusLabel("Completed", "Payment")).toBe("Pagamento efetuado");
    expect(statusLabel("Completed", "GeneralQuestion")).toBe("Respondida");
  });
  it("marks management responsibility", () =>
    expect(statusLabel("WaitingManager", "Fine")).toBe("Em processamento"));
  it.each([["200", 200], ["200,00", 200], ["200,50", 200.5], ["1.234,56", 1234.56]] as const)(
    "parses Brazilian money %s",
    (input, expected) => expect(parseMoneyInput(input)).toBe(expected),
  );
  it("formats an existing fine value for editing", () => expect(moneyInput(200)).toBe("200,00"));
});
