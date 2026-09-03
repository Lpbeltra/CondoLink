import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AttachmentsPreview } from "./AttachmentsPreview";
import { attachmentBlob } from "./api";
import type { Attachment } from "./types";

vi.mock("./api", () => ({ attachmentBlob: vi.fn() }));

function item(id: string, name: string, contentType: string): Attachment {
  return {
    id,
    messageId: null,
    purpose: "Request",
    originalFileName: name,
    contentType,
    fileSize: 1024,
    createdAt: "2026-08-28T12:00:00Z",
  };
}

describe("management company request attachment previews", () => {
  beforeEach(() => {
    vi.mocked(attachmentBlob).mockResolvedValue(new Blob(["content"]));
    vi.stubGlobal("URL", {
      ...URL,
      createObjectURL: vi.fn(() => "blob:authenticated"),
      revokeObjectURL: vi.fn(),
    });
  });

  it.each([
    ["image/png", "foto.png", "img"],
    ["audio/mpeg", "audio.mp3", "audio"],
    ["video/mp4", "video.mp4", "video"],
    ["application/pdf", "documento.pdf", "iframe"],
  ])(
    "loads %s on demand through an authenticated blob",
    async (contentType, name, tag) => {
      const view = render(
        <AttachmentsPreview items={[item("a", name, contentType)]} />,
      );
      fireEvent.click(screen.getByRole("button", { name: new RegExp(name) }));
      await waitFor(() => expect(attachmentBlob).toHaveBeenCalledWith("a"));
      expect(document.querySelector(tag)?.getAttribute("src")).toBe(
        "blob:authenticated",
      );
      view.unmount();
      expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:authenticated");
    },
  );

  it("offers authenticated generic files for download and reports load errors", async () => {
    const view = render(
      <AttachmentsPreview items={[item("g", "dados.csv", "text/csv")]} />,
    );
    expect(await screen.findByRole("link", { name: /baixar dados.csv/i })).toHaveAttribute("href", "blob:authenticated");
    view.unmount();

    vi.mocked(attachmentBlob).mockRejectedValueOnce(new Error("network"));
    render(
      <AttachmentsPreview items={[item("e", "erro.pdf", "application/pdf")]} />,
    );
    fireEvent.click(screen.getByRole("button", { name: /erro\.pdf/i }));
    await waitFor(() => expect(attachmentBlob).toHaveBeenCalledWith("e"));
    expect(
      await screen.findByText(/não foi possível carregar o anexo/i),
    ).toBeInTheDocument();
  });
});
