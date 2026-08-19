import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  CondominiumAssistantPage,
  CondominiumDocumentsPage,
} from "./CondominiumAssistantPage";

const assistant = vi.hoisted(() => ({
  listConversations: vi.fn(),
  getConversation: vi.fn(),
  startConversation: vi.fn(),
  askAssistant: vi.fn(),
  removeRequestContext: vi.fn(),
  deleteConversation: vi.fn(),
  listDocuments: vi.fn(),
  uploadDocument: vi.fn(),
  deleteDocument: vi.fn(),
  downloadDocument: vi.fn(),
}));
vi.mock("../assistant/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../assistant/api")>()),
  ...assistant,
}));
vi.mock("../management/ManagementContext", () => ({
  useManagementContext: () => ({ activeCondominiumId: "condo-1" }),
}));

describe("CondominiumAssistantPage", () => {
  beforeEach(() => {
    Object.values(assistant).forEach((mock) => mock.mockReset());
    assistant.listConversations.mockResolvedValue({
      items: [],
      hasMore: false,
      total: 0,
    });
    assistant.listDocuments.mockResolvedValue([]);
  });

  it("rejects documents above 25 MB before upload", async () => {
    const user = userEvent.setup();
    const { container } = render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    const file = new File(["pdf"], "large.pdf", { type: "application/pdf" });
    Object.defineProperty(file, "size", { value: 25 * 1024 * 1024 + 1 });

    await user.upload(container.querySelector('input[type="file"]')!, file);

    expect(
      await screen.findByText("O arquivo excede o limite de 25 MB."),
    ).toBeInTheDocument();
    expect(assistant.uploadDocument).not.toHaveBeenCalled();
  });

  it("shows the structured backend upload error", async () => {
    const user = userEvent.setup();
    assistant.uploadDocument.mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 400,
        data: {
          code: "DocumentFileTooLarge",
          message: "O arquivo excede o limite de 25 MB.",
        },
      },
    });
    const { container } = render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    await user.upload(
      container.querySelector('input[type="file"]')!,
      new File(["pdf"], "rules.pdf", { type: "application/pdf" }),
    );
    const title = screen.getByRole("textbox", { name: "Nome rules.pdf" });
    await user.clear(title); await user.type(title, "Regimento");
    await user.click(screen.getByRole("button", { name: "Enviar documentos" }));

    expect(
      await screen.findByText("O arquivo excede o limite de 25 MB."),
    ).toBeInTheDocument();
  });

  it("uploads a valid document", async () => {
    const user = userEvent.setup();
    assistant.uploadDocument.mockResolvedValue({});
    const { container } = render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    await user.upload(
      container.querySelector('input[type="file"]')!,
      new File(["pdf"], "rules.pdf", { type: "application/pdf" }),
    );
    await user.click(screen.getByRole("button", { name: "Enviar documentos" }));

    await waitFor(() =>
      expect(assistant.uploadDocument).toHaveBeenCalledTimes(1),
    );
  });

  it("uses the safe fallback for an unknown upload error", async () => {
    const user = userEvent.setup();
    assistant.uploadDocument.mockRejectedValue(new Error("internal detail"));
    const { container } = render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    await user.upload(
      container.querySelector('input[type="file"]')!,
      new File(["pdf"], "rules.pdf", { type: "application/pdf" }),
    );
    await user.click(screen.getByRole("button", { name: "Enviar documentos" }));

    expect(
      await screen.findByText(
        "Não foi possível enviar o documento. Tente novamente.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText("internal detail")).not.toBeInTheDocument();
  });

  it("cancels or confirms permanent deletion and removes the card", async () => {
    const user = userEvent.setup();
    assistant.listDocuments.mockResolvedValue([
      {
        id: "doc-1",
        name: "Convenção",
        originalFileName: "rules.pdf",
        version: 1,
        processingStatus: "Failed",
        processingError: "Falha",
        isActive: true,
      },
    ]);
    assistant.deleteDocument.mockResolvedValue({});
    render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    expect(await screen.findByText("Convenção")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Excluir Convenção" }));
    expect(
      screen.getByText(/será removido definitivamente/),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(assistant.deleteDocument).not.toHaveBeenCalled();
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    await user.click(screen.getByRole("button", { name: "Excluir Convenção" }));
    await user.click(screen.getByRole("button", { name: "Excluir" }));
    await waitFor(() =>
      expect(assistant.deleteDocument).toHaveBeenCalledWith("condo-1", "doc-1"),
    );
    expect(screen.queryByText("Convenção")).not.toBeInTheDocument();
  });

  it("keeps the document when deletion fails", async () => {
    const user = userEvent.setup();
    assistant.listDocuments.mockResolvedValue([
      {
        id: "doc-1",
        name: "Regimento",
        originalFileName: "rules.pdf",
        version: 1,
        processingStatus: "Unsupported",
        processingError: "Sem texto",
        isActive: true,
      },
    ]);
    assistant.deleteDocument.mockRejectedValue(new Error("internal"));
    render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    await user.click(
      await screen.findByRole("button", { name: "Excluir Regimento" }),
    );
    await user.click(screen.getByRole("button", { name: "Excluir" }));
    expect(
      await screen.findByText(
        "Não foi possível excluir o documento. Tente novamente.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("Regimento")).toBeInTheDocument();
  });

  it("shows empty history and does not persist an empty new conversation", async () => {
    render(
      <MemoryRouter>
        <CondominiumAssistantPage />
      </MemoryRouter>,
    );
    expect(
      await screen.findByText("Nenhuma conversa anterior."),
    ).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole("button", { name: "Nova conversa" }),
    );
    expect(assistant.startConversation).not.toHaveBeenCalled();
    expect(
      screen.getByText(
        "Pergunte sobre documentos, regras ou informações do condomínio.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.queryByText("Quais são as regras da piscina?"),
    ).not.toBeInTheDocument();
  });

  it("sends with Enter, keeps Shift+Enter as a line break and ignores empty input", async () => {
    const user = userEvent.setup();
    assistant.startConversation.mockResolvedValue({
      conversation: { id: "chat-1", requestId: null },
      answer: "ok",
      sources: [],
    });
    render(
      <MemoryRouter>
        <CondominiumAssistantPage />
      </MemoryRouter>,
    );
    const input = await screen.findByRole("textbox", {
      name: "Pergunte ao assistente",
    });
    await user.type(input, "{enter}");
    expect(assistant.startConversation).not.toHaveBeenCalled();
    await user.type(input, "linha 1{shift>}{enter}{/shift}linha 2");
    expect(assistant.startConversation).not.toHaveBeenCalled();
    await user.type(input, "{enter}");
    await waitFor(() =>
      expect(assistant.startConversation).toHaveBeenCalledWith(
        "condo-1",
        "linha 1\nlinha 2",
        undefined,
      ),
    );
  });

  it("downloads an inactive document through the authenticated API", async () => {
    assistant.listDocuments.mockResolvedValue([
      {
        id: "doc-1",
        name: "Convenção",
        originalFileName: "convenção.pdf",
        version: 1,
        processingStatus: "Ready",
        processingError: null,
        isActive: false,
      },
    ]);
    assistant.downloadDocument.mockResolvedValue(undefined);
    render(
      <MemoryRouter>
        <CondominiumDocumentsPage />
      </MemoryRouter>,
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Baixar" }),
    );
    expect(assistant.downloadDocument).toHaveBeenCalledWith(
      "condo-1",
      "doc-1",
      "convenção.pdf",
    );
  });

  it("reopens persisted messages with historical sources", async () => {
    assistant.listConversations.mockResolvedValue({
      items: [
        {
          id: "chat-1",
          title: "Barulho",
          requestId: "request-1",
          requestTitle: "Barulho na unidade",
          createdAt: "2026-08-17T10:00:00Z",
          updatedAt: "2026-08-17T11:00:00Z",
        },
      ],
      hasMore: false,
      total: 1,
    });
    assistant.getConversation.mockResolvedValue({
      conversation: { id: "chat-1", title: "Barulho", requestId: "request-1" },
      requestContext: { id: "request-1", title: "Barulho na unidade" },
      contextUnavailable: false,
      messages: [
        {
          id: "m1",
          role: "Assistant",
          content: "Resposta anterior",
          createdAt: "2026-08-17T11:00:00Z",
          sources: [
            {
              source: {
                documentId: "doc-1",
                documentName: "Regimento",
                pageNumber: 12,
                sectionTitle: null,
                excerpt: "...",
                marker: "S1",
              },
              documentCurrentlyActive: false,
            },
          ],
        },
      ],
    });
    render(
      <MemoryRouter>
        <CondominiumAssistantPage />
      </MemoryRouter>,
    );
    await userEvent.click(
      await screen.findByRole("button", { name: /Barulho/ }),
    );
    expect(await screen.findByText("Resposta anterior")).toBeInTheDocument();
    expect(
      screen.getByText(/documento atualmente inativo/),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(assistant.getConversation).toHaveBeenCalledWith(
        "condo-1",
        "chat-1",
      ),
    );
  });

  it("renders a removed historical source without a broken download link", async () => {
    assistant.listConversations.mockResolvedValue({
      items: [
        {
          id: "chat-1",
          title: "Histórico",
          requestId: null,
          createdAt: "2026-08-17T10:00:00Z",
          updatedAt: "2026-08-17T11:00:00Z",
        },
      ],
      hasMore: false,
      total: 1,
    });
    assistant.getConversation.mockResolvedValue({
      conversation: { id: "chat-1", title: "Histórico", requestId: null },
      requestContext: null,
      contextUnavailable: false,
      messages: [
        {
          id: "m1",
          role: "Assistant",
          content: "Resposta",
          createdAt: "2026-08-17T11:00:00Z",
          sources: [
            {
              source: {
                documentId: "removed",
                documentName: "Convenção antiga",
                pageNumber: 2,
                sectionTitle: null,
                excerpt: "Trecho",
                marker: "S1",
              },
              documentExists: false,
              documentCurrentlyActive: false,
            },
          ],
        },
      ],
    });
    render(
      <MemoryRouter>
        <CondominiumAssistantPage />
      </MemoryRouter>,
    );
    await userEvent.click(
      await screen.findByRole("button", { name: /Histórico/ }),
    );
    const source = await screen.findByText(
      /Convenção antiga.*documento removido/,
    );
    expect(source.closest("a")).toBeNull();
  });
});
