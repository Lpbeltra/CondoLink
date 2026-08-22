import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { streamAssistant } from "./streamAssistant";

function sseResponse(chunks: string[], init: ResponseInit = {}) {
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(new TextEncoder().encode(chunk));
      controller.close();
    },
  });
  return new Response(stream, {
    status: 200,
    headers: { "content-type": "text/event-stream" },
    ...init,
  });
}

describe("streamAssistant", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    localStorage.setItem("condolink.accessToken", "token-123");
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
    fetchMock.mockReset();
  });

  it("sends the bearer token and appends stream=true to the URL", async () => {
    fetchMock.mockResolvedValue(sseResponse(["data: [DONE]\n\n"]));

    await streamAssistant("/condominiums/condo-1/assistant/messages", { question: "oi" },
      {}, new AbortController().signal);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/condominiums/condo-1/assistant/messages?stream=true");
    expect(init.headers.Authorization).toBe("Bearer token-123");
    expect(JSON.parse(init.body)).toEqual({ question: "oi" });
  });

  it("parses sources, token and done events, including across chunk boundaries", async () => {
    fetchMock.mockResolvedValue(sseResponse([
      'event: sources\ndata: {"sources":[{"marker":"S1"}]}\n\nevent: tok',
      'en\ndata: {"delta":"Olá "}\n\nevent: token\ndata: {"delta":"mundo"}\n\n',
      'event: done\ndata: {"answer":"Olá mundo","sources":[],"conversation":{"id":"c1"}}\n\n',
    ]));
    const onSources = vi.fn();
    const onToken = vi.fn();
    const onDone = vi.fn();

    await streamAssistant("/path", {}, { onSources, onToken, onDone }, new AbortController().signal);

    expect(onSources).toHaveBeenCalledWith([{ marker: "S1" }]);
    expect(onToken).toHaveBeenNthCalledWith(1, "Olá ");
    expect(onToken).toHaveBeenNthCalledWith(2, "mundo");
    expect(onDone).toHaveBeenCalledWith({
      answer: "Olá mundo",
      sources: [],
      conversation: { id: "c1" },
    });
  });

  it("ignores [DONE] and malformed frames without crashing", async () => {
    fetchMock.mockResolvedValue(sseResponse([
      "data: [DONE]\n\nevent: token\ndata: not-json\n\nevent: token\ndata: {\"delta\":\"ok\"}\n\n",
    ]));
    const onToken = vi.fn();

    await streamAssistant("/path", {}, { onToken }, new AbortController().signal);

    expect(onToken).toHaveBeenCalledTimes(1);
    expect(onToken).toHaveBeenCalledWith("ok");
  });

  it("falls back to a plain JSON response when the backend does not stream", async () => {
    fetchMock.mockResolvedValue(new Response(
      JSON.stringify({ answer: "ok", sources: [] }),
      { status: 200, headers: { "content-type": "application/json" } },
    ));
    const onSources = vi.fn();
    const onDone = vi.fn();

    await streamAssistant("/path", {}, { onSources, onDone }, new AbortController().signal);

    expect(onSources).toHaveBeenCalledWith([]);
    expect(onDone).toHaveBeenCalledWith({ answer: "ok", sources: [] });
  });

  it("reports a status-mapped error for a non-ok response", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 403 }));
    const onError = vi.fn();

    await streamAssistant("/path", {}, { onError }, new AbortController().signal);

    expect(onError).toHaveBeenCalledWith("Você não possui permissão para realizar esta ação.");
  });

  it("reports a connection error when fetch itself rejects", async () => {
    fetchMock.mockRejectedValue(new TypeError("network down"));
    const onError = vi.fn();

    await streamAssistant("/path", {}, { onError }, new AbortController().signal);

    expect(onError).toHaveBeenCalledWith("Não foi possível conectar ao servidor. Tente novamente.");
  });

  it("stays silent when the request was aborted", async () => {
    const controller = new AbortController();
    fetchMock.mockImplementation(() => {
      controller.abort();
      return Promise.reject(new DOMException("aborted", "AbortError"));
    });
    const onError = vi.fn();

    await streamAssistant("/path", {}, { onError }, controller.signal);

    expect(onError).not.toHaveBeenCalled();
  });
});
