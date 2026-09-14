import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { Sidebar } from "./Sidebar";
import { useAuth } from "../auth/AuthContext";
import { useCondominium } from "../condominiums/CondominiumContext";
import { useManagementContext } from "../management/ManagementContext";
import { useAdministrator } from "../administrator/AdministratorContext";
import { useCondominiumModules } from "../modules/useCondominiumModules";

vi.mock("../auth/AuthContext", () => ({ useAuth: vi.fn() }));
vi.mock("../condominiums/CondominiumContext", () => ({ useCondominium: vi.fn() }));
vi.mock("../management/ManagementContext", () => ({ useManagementContext: vi.fn() }));
vi.mock("../administrator/AdministratorContext", () => ({ useAdministrator: vi.fn() }));
vi.mock("../modules/useCondominiumModules", () => ({ useCondominiumModules: vi.fn() }));

function mockContexts(administrator: unknown = null, currentCondominium: { roles: string[] } | null = null) {
  vi.mocked(useAuth).mockReturnValue({ user: { roles: [] } } as never);
  vi.mocked(useCondominium).mockReturnValue({ currentCondominium } as never);
  vi.mocked(useManagementContext).mockReturnValue({ condominiumCount: currentCondominium ? 1 : 0, hasEligibleManagementCompany: false } as never);
  vi.mocked(useAdministrator).mockReturnValue({ value: administrator, loading: false } as never);
  vi.mocked(useCondominiumModules).mockReturnValue({ isModuleEnabled: () => true } as never);
}

function renderSidebar() {
  return render(<MemoryRouter><Sidebar /></MemoryRouter>);
}

describe("Sidebar multi-role navigation", () => {
  it("shows administrator Employee Management only with effective access", () => {
    mockContexts({ managementCompanyId: "mc1", hasEmployeeManagementAccess: true });
    renderSidebar();
    expect(document.querySelector('a[href="/administrator/employees"]')).toBeInTheDocument();
  });

  it("hides administrator Employee Management without effective access", () => {
    mockContexts({ managementCompanyId: "mc1", hasEmployeeManagementAccess: false });
    renderSidebar();
    expect(document.querySelector('a[href="/administrator/employees"]')).not.toBeInTheDocument();
  });

  it("never exposes the old condominium Employee Management route", () => {
    mockContexts(null, { roles: ["Manager"] });
    renderSidebar();
    expect(document.querySelector('a[href="/management/employees"]')).not.toBeInTheDocument();
    expect(screen.getByText("Dashboard")).toBeInTheDocument();
  });
});
