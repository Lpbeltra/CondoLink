import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CondominiumMember } from "../management/types";

const managementApi = vi.hoisted(() => ({
  listCondominiumMembers: vi.fn(),
  listUnits: vi.fn(),
  onboardMember: vi.fn(),
  resendFirstAccess: vi.fn(),
  createFirstAccessLink: vi.fn(),
  deleteResident: vi.fn(),
  inactivateResident: vi.fn(),
  reactivateResident: vi.fn(),
  resetMemberTemporaryPassword: vi.fn(),
  updateCondominiumMember: vi.fn(),
}));

vi.mock("../management/api", () => managementApi);
vi.mock("../management/ManagementContext", () => ({
  useManagementContext: () => ({
    activeCondominiumId: "condominium-id",
  }),
}));

import { ManagementPeoplePage } from "./ManagementPeoplePage";

const member: CondominiumMember = {
  membershipId: "membership-id",
  userId: "user-id",
  fullName: "Maria Silva",
  email: "maria@example.com",
  phoneNumber: null,
  userActive: true,
  mustChangePassword: false,
  emailDeliveryEnabled: true,
  firstAccessStatus: "Completed",
  lastLoginAt: null,
  membershipActive: true,
  joinedAt: "2026-07-28T10:00:00Z",
  endedAt: null,
  roles: ["Resident"],
  unitLinks: [],
};

describe("ManagementPeoplePage password reset", () => {
  beforeEach(() => {
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
    });
    Object.values(managementApi).forEach((mock) => mock.mockReset());
    managementApi.listCondominiumMembers.mockResolvedValue([member]);
    managementApi.listUnits.mockResolvedValue([]);
    managementApi.resetMemberTemporaryPassword.mockResolvedValue({
      userId: member.userId,
      fullName: member.fullName,
      email: member.email,
      temporaryPassword: "NovaTemporaria1",
    });
    managementApi.updateCondominiumMember.mockResolvedValue({
      userId: member.userId,
      fullName: "Maria Atualizada",
      email: member.email,
      phoneNumber: null,
      cpf: null,
      cnpj: null,
      address: null,
      city: null,
      state: null,
      membershipActive: true,
      unitLink: null,
    });
  });

  it("confirms reset and shows the new temporary credential once", async () => {
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);

    await user.click(
      await screen.findByRole("button", {
        name: "Redefinir senha temporária",
      }),
    );
    expect(screen.getByText("Redefinir senha temporária?")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", {
        name: "Gerar nova senha",
      }),
    );

    expect(
      await screen.findByText("Senha temporária regenerada."),
    ).toBeInTheDocument();
    expect(screen.getByText(/NovaTemporaria1/)).toBeInTheDocument();
    expect(managementApi.resetMemberTemporaryPassword).toHaveBeenCalledWith(
      "condominium-id",
      "user-id",
    );
    await waitFor(() => {
      expect(screen.getByText("Senha temporária")).toBeInTheDocument();
    });
  });

  it("opens the populated edit form and updates the list locally", async () => {
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);

    await user.click(
      await screen.findByRole("button", { name: /Ações de Maria Silva/i }),
    );
    await user.click(await screen.findByRole("menuitem", { name: "Editar" }));
    expect(
      screen.getByRole("heading", {
        name: "Editar pessoa",
      }),
    ).toBeInTheDocument();
    const name = screen.getByRole("textbox", { name: "Nome completo" });
    expect(name).toHaveValue("Maria Silva");

    await user.clear(name);
    await user.type(name, "Maria Atualizada");
    await user.click(
      screen.getByRole("button", {
        name: "Salvar alterações",
      }),
    );

    expect(
      await screen.findByText("Pessoa atualizada com sucesso."),
    ).toBeInTheDocument();
    expect(screen.getByText("Maria Atualizada")).toBeInTheDocument();
    expect(managementApi.updateCondominiumMember).toHaveBeenCalledWith(
      "condominium-id",
      "user-id",
      expect.objectContaining({ fullName: "Maria Atualizada" }),
    );
  });

  it("copies the password and WhatsApp message and reports clipboard failure", async () => {
    const user = userEvent.setup();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });
    render(<ManagementPeoplePage />);

    await user.click(
      await screen.findByRole("button", {
        name: "Redefinir senha temporária",
      }),
    );
    await user.click(
      screen.getByRole("button", {
        name: "Gerar nova senha",
      }),
    );

    await user.click(
      await screen.findByRole("button", {
        name: "Copiar senha",
      }),
    );
    expect(writeText).toHaveBeenCalledWith("NovaTemporaria1");
    expect(screen.getByText("Senha copiada.")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", {
        name: "Copiar mensagem para WhatsApp",
      }),
    );
    expect(writeText).toHaveBeenLastCalledWith(
      expect.stringContaining("\nSenha temporária:\n`NovaTemporaria1`\n"),
    );
    expect(screen.getByText("Mensagem copiada.")).toBeInTheDocument();

    writeText.mockRejectedValueOnce(new Error("clipboard denied"));
    await user.click(screen.getByRole("button", { name: "Copiar senha" }));
    expect(
      screen.getByText(
        "Não foi possível copiar. Selecione o conteúdo manualmente.",
      ),
    ).toBeInTheDocument();
  });

  it("searches within the selected tab and confirms inactivation", async () => {
    const linkedMember = {
      ...member,
      isResidentActive: true,
      unitLinks: [
        {
          unitMembershipId: "link-id",
          unitId: "unit-id",
          unitIdentifier: "1201",
          block: "1",
          relationshipType: "Owner" as const,
          isResident: true,
          isPrimaryResidence: true,
          isActive: true,
          endedAt: null,
        },
      ],
      canDelete: false,
      deleteBlockedReason: "Possui histórico.",
    };
    managementApi.listCondominiumMembers.mockResolvedValue([linkedMember]);
    managementApi.inactivateResident.mockResolvedValue({});
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);

    await screen.findByText("Maria Silva");
    await user.type(screen.getByLabelText("Buscar morador"), "1201");
    await waitFor(() =>
      expect(managementApi.listCondominiumMembers).toHaveBeenLastCalledWith(
        "condominium-id",
        "1201",
        "active",
      ),
    );
    await user.click(
      screen.getByRole("button", { name: /Ações de Maria Silva/i }),
    );
    await user.click(
      screen.getByRole("menuitem", { name: /Inativar Bloco 1.*1201/i }),
    );
    expect(
      screen.getByRole("heading", { name: "Inativar morador?" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Inativar" }));
    await waitFor(() =>
      expect(managementApi.inactivateResident).toHaveBeenCalledWith(
        "condominium-id",
        "user-id",
        "link-id",
      ),
    );
  });

  it("debounces rapid search and does not reload units", async () => {
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);
    await screen.findByText("Maria Silva");
    managementApi.listCondominiumMembers.mockClear();
    managementApi.listUnits.mockClear();

    await user.type(screen.getByLabelText("Buscar morador"), "Tatiana");
    expect(managementApi.listCondominiumMembers).not.toHaveBeenCalled();
    await waitFor(() => expect(managementApi.listCondominiumMembers)
      .toHaveBeenCalledTimes(1));
    expect(managementApi.listCondominiumMembers).toHaveBeenCalledWith(
      "condominium-id", "Tatiana", "active");
    expect(managementApi.listUnits).not.toHaveBeenCalled();
  });

  it("ignores an obsolete search response and clears immediately", async () => {
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);
    await screen.findByText("Maria Silva");
    let resolveOld!: (value: CondominiumMember[]) => void;
    const oldResult = new Promise<CondominiumMember[]>((resolve) => {
      resolveOld = resolve;
    });
    const tatiana = { ...member, userId: "tatiana", fullName: "Tatiana Lima" };
    managementApi.listCondominiumMembers.mockImplementation(
      (_id: string, query: string) => query === "Tati"
        ? oldResult
        : Promise.resolve(query === "Tatiana" ? [tatiana] : [member]),
    );

    const input = screen.getByLabelText("Buscar morador");
    await user.type(input, "Tati");
    await waitFor(() => expect(managementApi.listCondominiumMembers)
      .toHaveBeenCalledWith("condominium-id", "Tati", "active"));
    await user.type(input, "ana");
    expect(await screen.findByText("Tatiana Lima")).toBeInTheDocument();
    resolveOld([member]);
    await Promise.resolve();
    expect(screen.queryByText("Maria Silva")).not.toBeInTheDocument();

    await user.clear(input);
    await waitFor(() => expect(managementApi.listCondominiumMembers)
      .toHaveBeenLastCalledWith("condominium-id", "", "active"));
  });

  it("changes the active tab without waiting for search debounce", async () => {
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);
    await screen.findByText("Maria Silva");
    managementApi.listCondominiumMembers.mockClear();

    await user.click(screen.getByRole("tab", { name: "Inativos" }));
    await waitFor(() => expect(managementApi.listCondominiumMembers)
      .toHaveBeenCalledWith("condominium-id", "", "inactive"));
  });

  it("offers combined first access only when phone and deliverable email are available", async () => {
    const user = userEvent.setup();
    render(<ManagementPeoplePage />);
    await screen.findByText("Maria Silva");
    await user.click(screen.getByRole("button", { name: /adicionar pessoa/i }));

    await user.click(screen.getByLabelText("Enviar primeiro acesso"));
    expect(screen.getByRole("option", { name: "WhatsApp + E-mail" }))
      .toHaveAttribute("aria-disabled", "true");
    await user.keyboard("{Escape}");

    await user.type(screen.getByLabelText("Telefone / WhatsApp"), "+12125551234");
    await user.click(screen.getByLabelText("Enviar primeiro acesso"));
    expect(screen.getByRole("option", { name: "WhatsApp + E-mail" }))
      .not.toHaveAttribute("aria-disabled", "true");
  });
});
