import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ManagementCompanyRequestActionCard } from "./ActionCard";
import { RequestInformationCards } from "./RequestInformationCards";
import { ManagementCompanyRequestTimeline } from "./RequestTimeline";
import { ManagementCompanyRequestConversation } from "./Conversation";
import type { ManagementCompanyRequestType, RequestDetail } from "./types";

vi.mock("./api", () => ({ attachmentBlob: vi.fn().mockResolvedValue(new Blob(["attachment"])) }));
vi.mock("./AttachmentsPreview", () => ({ AttachmentsPreview: ({ items }: { items: { originalFileName: string }[] }) => <div>{items.map(item => <span key={item.originalFileName}>{item.originalFileName}</span>)}</div> }));

function detail(type: ManagementCompanyRequestType, role: "Manager" | "SubManager" = "Manager") {
  return { type, status: "InProgress", requester: { id: "creator", fullName: role === "Manager" ? "Maria" : "João", role }, condominiumName: "Condomínio", condominium: { name: "Condomínio", address: null, city: null, state: null, managers: [] }, fine: type === "Fine" ? { unitId: "u", unit: "101", block: "A", nature: "Regra", description: "Descrição", occurrenceDate: "2026-08-31", value: null, valueNotDefined: true } : undefined, payment: type === "Payment" ? { nature: "Fornecedor", value: 100, eventDate: "2026-08-31", dueDate: "2026-09-10", isReimbursement: false, notes: null, beneficiaryName: null, pixKeyType: null, pixKey: null, thirdPartyIdentification: "Fornecedor XPTO", thirdPartyForm: "DepositAccount", thirdPartyPixKey: null, thirdPartyBank: "Banco X", thirdPartyAgency: "0001", thirdPartyAccount: "12345-6" } : undefined, question: type === "GeneralQuestion" ? { theme: "Contrato" } : undefined, history: [{ id: "h", eventType: "Created", previousStatus: null, newStatus: "Submitted", changedByUserId: "creator", reason: null, createdAt: "2026-08-31T12:00:00Z" }], messages: [], attachments: [] } as unknown as RequestDetail;
}
describe("shared management company request detail components", () => {
  it("renders numeric request and boleto purposes without mixing the chat attachment", () => {
    const base = detail("Payment");
    const request = { ...base, payment: { ...base.payment!, thirdPartyForm: "Boleto" }, attachments: [
      { id: "request", messageId: null, purpose: 1, originalFileName: "documento.pdf", contentType: "application/pdf", fileSize: 10, createdAt: "2026-08-31T12:00:00Z" },
      { id: "boleto", messageId: null, purpose: 3, originalFileName: "boleto.pdf", contentType: "application/pdf", fileSize: 10, createdAt: "2026-08-31T12:00:00Z" },
      { id: "message-file", messageId: "message", purpose: 2, originalFileName: "chat.pdf", contentType: "application/pdf", fileSize: 10, createdAt: "2026-08-31T12:00:00Z" },
    ], messages: [{ id: "message", authorUserId: "u", authorName: "Ana", authorRole: "Sindico", content: "Mensagem", createdAt: "2026-08-31T12:00:00Z" }] } as unknown as RequestDetail;
    render(<><RequestInformationCards request={request} /><ManagementCompanyRequestConversation request={request} text="" files={[]} sending={false} readOnly onText={vi.fn()} onFiles={vi.fn()} onError={vi.fn()} onSend={vi.fn()} /></>);
    expect(screen.getByText(/Anexos da solicita/)).toBeInTheDocument();
    expect(screen.getByText("documento.pdf")).toBeInTheDocument();
    expect(screen.getByText("Forma de pagamento")).toBeInTheDocument();
    expect(screen.getByText("boleto.pdf")).toBeInTheDocument();
    expect(screen.getByText("chat.pdf")).toBeInTheDocument();
  });
  it.each(["Fine", "Payment", "GeneralQuestion"] as ManagementCompanyRequestType[])("renders data and a status-only Timeline for %s", type => { render(<><RequestInformationCards request={detail(type)} showRequester /><ManagementCompanyRequestTimeline request={detail(type)} /></>); expect(screen.getByRole("heading", { name: "Dados da solicitação" })).toBeInTheDocument(); expect(screen.getByText("Solicitação criada")).toBeInTheDocument(); });
  it.each([["Manager", "Síndico: Maria"], ["SubManager", "Subsíndico: João"]] as const)("renders creator role %s", (role, label) => { render(<RequestInformationCards request={detail("GeneralQuestion", role)} showRequester />); expect(screen.getByText(label)).toBeInTheDocument(); });
  it("keeps actions in their own visual card", () => { render(<ManagementCompanyRequestActionCard><button>Concluir</button></ManagementCompanyRequestActionCard>); expect(screen.getByRole("heading", { name: "Ações de atendimento" })).toBeInTheDocument(); expect(screen.getByRole("button", { name: "Concluir" })).toBeInTheDocument(); });
});
