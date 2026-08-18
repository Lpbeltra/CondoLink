import { beforeEach, describe, expect, it, vi } from "vitest";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../services/api", () => ({ api: http }));
import {
  askAssistant,
  createConversation,
  deleteConversation,
  deleteDocument,
  downloadDocument,
  getConversation,
  listConversations,
  startConversation,
  uploadDocument,
} from "./api";

describe("condominium assistant API", () => {
  beforeEach(() => Object.values(http).forEach((mock) => mock.mockReset()));

  it("uploads documents using multipart form data", async () => {
    http.post.mockResolvedValue({ data: { id: "doc-1" } });
    const form = new FormData();
    form.append("file", new File(["rules"], "rules.txt"));
    await uploadDocument("condo-1", form);
    expect(http.post).toHaveBeenCalledWith(
      "/condominiums/condo-1/documents",
      form,
      { timeout: 5 * 60 * 1000 },
    );
  });

  it("keeps request context on conversation and sends question separately", async () => {
    http.post
      .mockResolvedValueOnce({ data: { id: "chat-1", requestId: "request-1" } })
      .mockResolvedValueOnce({ data: { answer: "Resposta", sources: [] } });
    await createConversation("condo-1", "request-1");
    await askAssistant("condo-1", "chat-1", "Qual regra se aplica?");
    expect(http.post).toHaveBeenNthCalledWith(
      1,
      "/condominiums/condo-1/assistant/conversations",
      { requestId: "request-1", title: "Consulta sobre solicitação" },
    );
    expect(http.post).toHaveBeenNthCalledWith(
      2,
      "/condominiums/condo-1/assistant/conversations/chat-1/messages",
      { question: "Qual regra se aplica?" },
    );
  });

  it("supports paged history, reopening and deletion scoped by condominium", async () => {
    http.get
      .mockResolvedValueOnce({ data: { items: [], hasMore: false, total: 0 } })
      .mockResolvedValueOnce({
        data: { conversation: { id: "chat-1" }, messages: [] },
      });
    http.delete.mockResolvedValue({});
    await listConversations("condo-1", 2, "piscina");
    await getConversation("condo-1", "chat-1");
    await deleteConversation("condo-1", "chat-1");
    expect(http.get).toHaveBeenNthCalledWith(
      1,
      "/condominiums/condo-1/assistant/conversations",
      { params: { page: 2, pageSize: 20, search: "piscina" } },
    );
    expect(http.get).toHaveBeenNthCalledWith(
      2,
      "/condominiums/condo-1/assistant/conversations/chat-1",
    );
    expect(http.delete).toHaveBeenCalledWith(
      "/condominiums/condo-1/assistant/conversations/chat-1",
    );
  });

  it("creates a conversation only together with the first question", async () => {
    http.post.mockResolvedValue({
      data: { conversation: { id: "chat-1" }, answer: "ok", sources: [] },
    });
    await startConversation("condo-1", "Regras da piscina?", "request-1");
    expect(http.post).toHaveBeenCalledWith(
      "/condominiums/condo-1/assistant/messages",
      { question: "Regras da piscina?", requestId: "request-1" },
    );
  });

  it("deletes a document within its condominium scope", async () => {
    http.delete.mockResolvedValue({});
    await deleteDocument("condo-1", "doc-1");
    expect(http.delete).toHaveBeenCalledWith(
      "/condominiums/condo-1/documents/doc-1",
    );
  });

  it("downloads a document as an authenticated blob with the response filename", async () => {
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(() => {});
    const createObjectURL = vi.fn(() => "blob:test");
    const revoke = vi.fn();
    Object.defineProperty(URL, "createObjectURL", {
      value: createObjectURL,
      configurable: true,
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      value: revoke,
      configurable: true,
    });
    http.get.mockResolvedValue({
      data: new Blob(["pdf"]),
      headers: {
        "content-disposition":
          "attachment; filename*=UTF-8''Conven%C3%A7%C3%A3o.pdf",
      },
    });
    await downloadDocument("condo-1", "doc-1", "fallback.pdf");
    expect(http.get).toHaveBeenCalledWith(
      "/condominiums/condo-1/documents/doc-1/download",
      { responseType: "blob" },
    );
    expect(click).toHaveBeenCalled();
    expect(revoke).toHaveBeenCalledWith("blob:test");
  });
});
