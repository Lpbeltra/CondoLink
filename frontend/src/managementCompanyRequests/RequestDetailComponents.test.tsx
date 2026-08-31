import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ManagementCompanyRequestActionCard } from "./ActionCard";
import { RequestInformationCards } from "./RequestInformationCards";
import { ManagementCompanyRequestTimeline } from "./RequestTimeline";
import type { ManagementCompanyRequestType, RequestDetail } from "./types";

function detail(type: ManagementCompanyRequestType, role: "Manager" | "SubManager" = "Manager") {
  return { type, status: "InProgress", requester: { id: "creator", fullName: role === "Manager" ? "Maria" : "João", role }, condominiumName: "Condomínio", condominium: { name: "Condomínio", address: null, city: null, state: null, managers: [] }, fine: type === "Fine" ? { unitId: "u", unit: "101", block: "A", nature: "Regra", description: "Descrição", occurrenceDate: "2026-08-31", value: null, valueNotDefined: true } : undefined, payment: type === "Payment" ? { nature: "Fornecedor", value: 100, eventDate: "2026-08-31", isReimbursement: false, notes: null, beneficiaryName: null, pixKeyType: null, pixKey: null } : undefined, question: type === "GeneralQuestion" ? { theme: "Contrato" } : undefined, history: [{ id: "h", eventType: "Created", previousStatus: null, newStatus: "Submitted", changedByUserId: "creator", reason: null, createdAt: "2026-08-31T12:00:00Z" }], messages: [], attachments: [] } as unknown as RequestDetail;
}
describe("shared management company request detail components", () => {
  it.each(["Fine", "Payment", "GeneralQuestion"] as ManagementCompanyRequestType[])("renders data and a status-only Timeline for %s", type => { render(<><RequestInformationCards request={detail(type)} showRequester /><ManagementCompanyRequestTimeline request={detail(type)} /></>); expect(screen.getByRole("heading", { name: "Dados da solicitação" })).toBeInTheDocument(); expect(screen.getByText("Solicitação criada")).toBeInTheDocument(); });
  it.each([["Manager", "Síndico: Maria"], ["SubManager", "Subsíndico: João"]] as const)("renders creator role %s", (role, label) => { render(<RequestInformationCards request={detail("GeneralQuestion", role)} showRequester />); expect(screen.getByText(label)).toBeInTheDocument(); });
  it("keeps actions in their own visual card", () => { render(<ManagementCompanyRequestActionCard><button>Concluir</button></ManagementCompanyRequestActionCard>); expect(screen.getByRole("heading", { name: "Ações de atendimento" })).toBeInTheDocument(); expect(screen.getByRole("button", { name: "Concluir" })).toBeInTheDocument(); });
});
