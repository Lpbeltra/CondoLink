import { describe, expect, it } from "vitest";
import { statusLabel } from "./presentation";
describe("management company request presentation", () => {
  it("uses contextual completed labels", () => {
    expect(statusLabel("Completed", "Fine")).toBe("Processada");
    expect(statusLabel("Completed", "Payment")).toBe("Pagamento efetuado");
    expect(statusLabel("Completed", "GeneralQuestion")).toBe("Respondida");
  });
  it("marks management responsibility", () =>
    expect(statusLabel("WaitingManager", "Fine")).toBe("Em processamento"));
});
