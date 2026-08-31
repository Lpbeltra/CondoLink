import { beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "../services/api";
import {
  changeRequestStatus,
  createRequest,
  completePayment,
  listAdministratorRequests,
  listRequests,
  startRequestProcessing,
} from "./api";
vi.mock("../services/api", () => ({ api: { get: vi.fn(), post: vi.fn() } }));
describe("management company requests api", () => {
  beforeEach(() => vi.clearAllMocks());
  it("sends backend pagination and consolidated scope", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { items: [], page: 2, pageSize: 20, total: 0, hasMore: false },
    });
    await listRequests({ page: 2, search: "ADM" });
    expect(api.get).toHaveBeenCalledWith("/management-company-requests", {
      params: { page: 2, search: "ADM", pageSize: 20 },
    });
  });
  it("sends condominium and inclusive creation period filters", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, total: 0, hasMore: false },
    });
    await listRequests({
      page: 1,
      condominiumId: "condo-1",
      from: "2026-08-01",
      to: "2026-08-28",
    });
    expect(api.get).toHaveBeenCalledWith("/management-company-requests", {
      params: {
        page: 1,
        pageSize: 20,
        condominiumId: "condo-1",
        from: "2026-08-01",
        to: "2026-08-28",
      },
    });
  });
  it("creates atomically using multipart", async () => {
    vi.mocked(api.post).mockResolvedValue({
      data: { id: "1", friendlyIdentifier: "ADM-X" },
    });
    await createRequest("GeneralQuestion", { theme: "Tema" }, [
      new File(["pdf"], "a.pdf", { type: "application/pdf" }),
    ]);
    const [, body] = vi.mocked(api.post).mock.calls[0];
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).getAll("files")).toHaveLength(1);
  });
  it("queries the administrator queue with server-side filters", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { items: [], page: 1, pageSize: 20, total: 0, hasMore: false },
    });
    await listAdministratorRequests({
      page: 1,
      condominiumId: "c",
      categoryId: "cat",
      status: "Submitted",
      from: "2026-08-01",
      to: "2026-08-28",
    });
    expect(api.get).toHaveBeenCalledWith("/administrator/requests", {
      params: expect.objectContaining({
        condominiumId: "c",
        categoryId: "cat",
        status: "Submitted",
      }),
    });
  });
  it("uses domain status and atomic multipart interaction contracts", async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    await changeRequestStatus("r", "InProgress");
    expect(api.post).toHaveBeenCalledWith(
      "/management-company-requests/r/status",
      { status: "InProgress", reason: null },
    );
    await completePayment("r", [new File(["pdf"], "ata.pdf", { type: "application/pdf" })]);
    const [, body] = vi.mocked(api.post).mock.calls[1];
    expect(body).toBeInstanceOf(FormData);
    expect(JSON.parse(String((body as FormData).get("payload")))).toMatchObject({});
  });
  it("starts processing through the confirmed atomic endpoint", async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    await startRequestProcessing("r");
    expect(api.post).toHaveBeenCalledWith(
      "/management-company-requests/r/start-processing",
    );
  });
});
