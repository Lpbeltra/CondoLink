import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { Sidebar } from "./Sidebar";
import { useAuth } from "../auth/AuthContext";
import { useCondominium } from "../condominiums/CondominiumContext";
import { useManagementContext } from "../management/ManagementContext";
import { useAdministrator } from "../administrator/AdministratorContext";

vi.mock("../auth/AuthContext", () => ({ useAuth: vi.fn() }));
vi.mock("../condominiums/CondominiumContext", () => ({
  useCondominium: vi.fn(),
}));
vi.mock("../management/ManagementContext", () => ({
  useManagementContext: vi.fn(),
}));
vi.mock("../administrator/AdministratorContext", () => ({
  useAdministrator: vi.fn(),
}));

function mockContexts({
  administrator = null,
  condominiumCount = 0,
  hasEligibleManagementCompany = false,
  currentCondominium = null,
}: {
  administrator?: unknown;
  condominiumCount?: number;
  hasEligibleManagementCompany?: boolean;
  currentCondominium?: { roles: string[] } | null;
}) {
  vi.mocked(useAuth).mockReturnValue({ user: { roles: [] } } as never);
  vi.mocked(useCondominium).mockReturnValue({
    currentCondominium,
  } as never);
  vi.mocked(useManagementContext).mockReturnValue({
    condominiumCount,
    hasEligibleManagementCompany,
  } as never);
  vi.mocked(useAdministrator).mockReturnValue({
    value: administrator,
    loading: false,
  } as never);
}

function renderSidebar() {
  return render(
    <MemoryRouter>
      <Sidebar />
    </MemoryRouter>,
  );
}

describe("Sidebar multi-role navigation", () => {
  it("shows only the administrator queue for a pure administrator access", () => {
    mockContexts({ administrator: { managementCompanyId: "mc1" } });
    renderSidebar();
    expect(
      screen.getByText("Solicitações da administradora"),
    ).toBeInTheDocument();
    expect(screen.queryByText("Solicitações")).not.toBeInTheDocument();
    expect(screen.queryByText("Dashboard")).not.toBeInTheDocument();
  });

  it("keeps both experiences visible for a user with management and administrator access", () => {
    mockContexts({
      administrator: { managementCompanyId: "mc1" },
      condominiumCount: 1,
      hasEligibleManagementCompany: true,
      currentCondominium: { roles: ["Manager"] },
    });
    renderSidebar();
    expect(screen.getByText("Dashboard")).toBeInTheDocument();
    expect(screen.getByText("Solicitações")).toBeInTheDocument();
    expect(
      screen.getByText("Solicitações da administradora"),
    ).toBeInTheDocument();
  });

  it("hides the administrator item for a pure management user", () => {
    mockContexts({
      administrator: null,
      condominiumCount: 1,
      hasEligibleManagementCompany: true,
      currentCondominium: { roles: ["Manager"] },
    });
    renderSidebar();
    expect(screen.getByText("Dashboard")).toBeInTheDocument();
    expect(
      screen.queryByText("Solicitações da administradora"),
    ).not.toBeInTheDocument();
  });
});
