import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

const { post, logout, auth } = vi.hoisted(() => ({
  post: vi.fn(),
  logout: vi.fn(),
  auth: {
    user: null as null | { id: string; email: string },
    isInitializing: false,
  },
}));
vi.mock("../services/api", () => ({ api: { post } }));
vi.mock("../auth/AuthContext", () => ({
  useAuth: () => ({ ...auth, logout }),
}));
vi.mock("../theme/ThemeModeToggle", () => ({
  ThemeModeToggle: () => <button aria-label="Alternar para o tema escuro" />,
}));
import { FirstAccessPage } from "./FirstAccessPage";

const invitation =
  "/primeiro-acesso?userId=11111111-1111-1111-1111-111111111111&token=abc";

describe("FirstAccessPage", () => {
  beforeEach(() => {
    post.mockReset();
    logout.mockReset();
    auth.user = null;
    auth.isInitializing = false;
  });

  it("validates the link and creates the password", async () => {
    post
      .mockResolvedValueOnce({ data: { valid: true } })
      .mockResolvedValueOnce({ data: {} });
    render(
      <MemoryRouter initialEntries={[invitation]}>
        <FirstAccessPage />
      </MemoryRouter>,
    );
    await screen.findByRole("heading", { name: "Crie sua senha" });
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/^Nova senha/), "NovaSenha1");
    await user.type(screen.getByLabelText(/^Confirmar senha/), "NovaSenha1");
    await user.click(screen.getByRole("button", { name: "Criar senha" }));
    expect(
      await screen.findByText("Senha criada com sucesso."),
    ).toBeInTheDocument();
  });

  it("uses new-password autocomplete and toggles each password independently", async () => {
    post.mockResolvedValueOnce({ data: { valid: true } });
    render(
      <MemoryRouter initialEntries={[invitation]}>
        <FirstAccessPage />
      </MemoryRouter>,
    );
    const password = await screen.findByLabelText(/^Nova senha/);
    const confirmation = screen.getByLabelText(/^Confirmar senha/);
    expect(password).toHaveAttribute("autocomplete", "new-password");
    expect(confirmation).toHaveAttribute("autocomplete", "new-password");
    const user = userEvent.setup();
    const toggles = screen.getAllByRole("button", { name: "Exibir senha" });
    await user.click(toggles[0]);
    expect(password).toHaveAttribute("type", "text");
    expect(confirmation).toHaveAttribute("type", "password");
    await user.click(toggles[1]);
    expect(confirmation).toHaveAttribute("type", "text");
  });

  it("shows a safe error for an invalid token", async () => {
    post.mockRejectedValueOnce(new Error("invalid"));
    render(
      <MemoryRouter initialEntries={[invitation]}>
        <FirstAccessPage />
      </MemoryRouter>,
    );
    await waitFor(() =>
      expect(
        screen.getByText(/inválido, expirou ou já foi utilizado/i),
      ).toBeInTheDocument(),
    );
  });

  it("asks for consent before leaving an authenticated session", async () => {
    auth.user = { id: "current-user", email: "atual@example.com" };
    render(
      <MemoryRouter initialEntries={[invitation]}>
        <FirstAccessPage />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", { name: "Você já está conectado" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/atual@example.com/)).toBeInTheDocument();
    expect(post).not.toHaveBeenCalled();
    await userEvent.click(
      screen.getByRole("button", { name: "Sair e continuar" }),
    );
    expect(logout).toHaveBeenCalledOnce();
    expect(post).not.toHaveBeenCalled();
  });

  it("keeps the session and invitation untouched when canceling", async () => {
    auth.user = { id: "current-user", email: "atual@example.com" };
    render(
      <MemoryRouter initialEntries={["/", invitation]} initialIndex={1}>
        <FirstAccessPage />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(logout).not.toHaveBeenCalled();
    expect(post).not.toHaveBeenCalled();
  });
});
