import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { LocalAttachmentsPreview } from "./LocalAttachmentsPreview";

describe("LocalAttachmentsPreview", () => {
  it("previews browser media, removes files and revokes object URLs", async () => {
    Object.defineProperty(URL, "createObjectURL", { configurable: true, value: vi.fn(file => `blob:${file.name}`) });
    Object.defineProperty(URL, "revokeObjectURL", { configurable: true, value: vi.fn() });
    const remove = vi.fn();
    const files = [new File(["i"], "foto.png", { type: "image/png" }), new File(["a"], "som.mp3", { type: "audio/mpeg" }), new File(["v"], "video.mp4", { type: "video/mp4" }), new File(["x"], "dados.zip", { type: "application/zip" })];
    const { unmount } = render(<LocalAttachmentsPreview files={files} onRemove={remove} />);
    expect(screen.getByAltText("Prévia de foto.png")).toBeInTheDocument();
    expect(document.querySelector("audio")).toBeTruthy();
    expect(document.querySelector("video")).toBeTruthy();
    expect(screen.getByText("dados.zip")).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole("button", { name: /Remover/ })[0]);
    expect(remove).toHaveBeenCalledWith(0);
    unmount();
    expect(URL.revokeObjectURL).toHaveBeenCalledTimes(4);
  });
});
