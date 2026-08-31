import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { ComponentProps } from "react";
import { ManagementCompanyRequestConversation } from "./Conversation";
import type { RequestDetail } from "./types";

const request = { attachments: [], messages: [], history: [] } as unknown as RequestDetail;
function setup(overrides: Partial<ComponentProps<typeof ManagementCompanyRequestConversation>> = {}) {
  const send = vi.fn(async () => {}); const files = vi.fn(); const text = vi.fn();
  render(<ManagementCompanyRequestConversation request={request} currentUserId="me" text="Mensagem" files={[]} sending={false} readOnly={false} onText={text} onFiles={files} onError={vi.fn()} onSend={send} {...overrides} />);
  return { send, files, text };
}
describe("ManagementCompanyRequestConversation", () => {
  it("sends with Enter and keeps Shift+Enter as a newline", async () => { const { send } = setup(); const input = screen.getByPlaceholderText(/escreva uma mensagem/i); fireEvent.keyDown(input, { key: "Enter", shiftKey: true }); expect(send).not.toHaveBeenCalled(); fireEvent.keyDown(input, { key: "Enter" }); await waitFor(() => expect(send).toHaveBeenCalledTimes(1)); });
  it("does not send blank content or duplicate while sending", () => { const blank = setup({ text: "   " }); fireEvent.keyDown(screen.getByPlaceholderText(/escreva/i), { key: "Enter" }); expect(blank.send).not.toHaveBeenCalled(); const busy = setup({ sending: true }); fireEvent.submit(screen.getAllByRole("button", { name: /enviar mensagem/i })[1].closest("form")!); expect(busy.send).not.toHaveBeenCalled(); });
  it("selects and removes attachments", async () => { const file = new File(["x"], "foto.png", { type: "image/png" }); const selected = setup(); await userEvent.upload(screen.getByLabelText(/anexar arquivos/i).querySelector("input")!, file); expect(selected.files).toHaveBeenCalledWith([file]); const remove = vi.fn(); Object.defineProperty(URL, "createObjectURL", { configurable: true, value: vi.fn(() => "blob:local") }); Object.defineProperty(URL, "revokeObjectURL", { configurable: true, value: vi.fn() }); render(<ManagementCompanyRequestConversation request={request} text="x" files={[file]} sending={false} readOnly={false} onText={vi.fn()} onFiles={remove} onError={vi.fn()} onSend={vi.fn()} />); await userEvent.click(screen.getByRole("button", { name: /remover/i })); expect(remove).toHaveBeenCalledWith([]); });
});
