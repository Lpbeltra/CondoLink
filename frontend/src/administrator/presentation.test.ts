import { describe, expect, it } from "vitest";
import {
  administratorActions,
  administratorRequestStatusLabel,
  administratorStatusLabel,
  completionAction,
} from "./presentation";
describe("administrator request presentation", () => {
  it("uses administrator contextual labels", () =>
    expect(administratorStatusLabel.WaitingManager).toBe("Aguardando gestão"));
  it("uses contextual completed labels", () =>
    expect(administratorRequestStatusLabel("Completed", "Payment")).toBe(
      "Pagamento efetuado",
    ));
  it("derives actions from the state machine", () => {
    expect(administratorActions("Acknowledged")).toMatchObject({
      canStart: false,
      canComplete: false,
    });
    expect(administratorActions("InProgress")).toMatchObject({
      canInteract: true,
      canComplete: true,
    });
    expect(administratorActions("Completed").readOnly).toBe(true);
    expect(administratorActions("WaitingManager").canInteract).toBe(true);
  });
  it("uses contextual completion actions", () => {
    expect(completionAction("Fine")).toBe("Marcar como processada");
    expect(completionAction("Payment")).toBe("Confirmar pagamento efetuado");
    expect(completionAction("GeneralQuestion")).toBe("Marcar como respondida");
  });
});
