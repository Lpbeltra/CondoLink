import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { AppShell } from "./AppShell";
import { AppThemeProvider } from "../theme/AppThemeProvider";
import { useAuth } from "../auth/AuthContext";
import { useCondominium } from "../condominiums/CondominiumContext";
import {
  useManagementContext,
  useOptionalManagementContext,
} from "../management/ManagementContext";
import { useAdministrator } from "../administrator/AdministratorContext";

vi.mock("../auth/AuthContext", () => ({ useAuth: vi.fn() }));
vi.mock("../condominiums/CondominiumContext", () => ({
  useCondominium: vi.fn(),
}));
vi.mock("../management/ManagementContext", () => ({
  useManagementContext: vi.fn(),
  useOptionalManagementContext: vi.fn(),
}));
vi.mock("../administrator/AdministratorContext", () => ({
  useAdministrator: vi.fn(),
}));
vi.mock("../notifications/NotificationBell", () => ({
  NotificationBell: () => null,
}));

function mockContexts({
  condominiumCount = 0,
  isManagementLoading = false,
  administrator = null,
  administratorLoading = false,
  hasEligibleManagementCompany = false,
  isPlatformAdmin = false,
}: {
  condominiumCount?: number;
  isManagementLoading?: boolean;
  administrator?: unknown;
  administratorLoading?: boolean;
  hasEligibleManagementCompany?: boolean;
  isPlatformAdmin?: boolean;
}) {
  vi.mocked(useAuth).mockReturnValue({
    user: { roles: isPlatformAdmin ? ["PlatformAdmin"] : [], fullName: "Teste" },
    logout: vi.fn(),
  } as never);
  vi.mocked(useCondominium).mockReturnValue({
    currentCondominium: null,
    isLoading: false,
    error: null,
    refreshCondominiums: vi.fn(),
    condominiums: [],
  } as never);
  vi.mocked(useManagementContext).mockReturnValue({
    condominiumCount,
    isLoading: isManagementLoading,
    hasEligibleManagementCompany,
  } as never);
  vi.mocked(useOptionalManagementContext).mockReturnValue({
    condominiumCount,
  } as never);
  vi.mocked(useAdministrator).mockReturnValue({
    value: administrator,
    loading: administratorLoading,
  } as never);
}

function renderShellAt(path: string) {
  return render(
    <AppThemeProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<AppShell />}>
            <Route
              path="management/administrator"
              element={<div>Solicitações enviadas à administradora</div>}
            />
            <Route
              path="management/administrator/new"
              element={<div>Formulário de nova solicitação</div>}
            />
            <Route
              path="management/administrator/:id"
              element={<div>Detalhe da solicitação da Gestão</div>}
            />
            <Route
              path="administrator/requests"
              element={<div>Fila da administradora</div>}
            />
          </Route>
        </Routes>
      </MemoryRouter>
    </AppThemeProvider>,
  );
}

describe("AppShell content gating vs. the administrator portal context", () => {
  const gestaoAdministratorRoutes: [string, string][] = [
    ["/management/administrator", "Solicitações enviadas à administradora"],
    ["/management/administrator/new", "Formulário de nova solicitação"],
    ["/management/administrator/req-1", "Detalhe da solicitação da Gestão"],
  ];

  it.each(gestaoAdministratorRoutes)(
    "renders %s immediately for a pure Manager while /administrator/context is still resolving (403 pending)",
    (path, expectedText) => {
      mockContexts({
        condominiumCount: 1,
        administrator: null,
        administratorLoading: true,
      });
      renderShellAt(path);
      expect(screen.getByText(expectedText)).toBeInTheDocument();
    },
  );

  it.each(gestaoAdministratorRoutes)(
    "keeps rendering %s for a pure Manager after /administrator/context resolves to 403/null",
    (path, expectedText) => {
      mockContexts({
        condominiumCount: 1,
        administrator: null,
        administratorLoading: false,
      });
      renderShellAt(path);
      expect(screen.getByText(expectedText)).toBeInTheDocument();
    },
  );

  it.each(gestaoAdministratorRoutes)(
    "renders %s for a pure SubManager the same way, without waiting on /administrator/context",
    (path, expectedText) => {
      // SubManager gets the exact same signal as Manager here: AppShell only
      // reads condominiumCount from ManagementContext, which is populated
      // identically for Manager and SubManager scope.
      mockContexts({
        condominiumCount: 1,
        administrator: null,
        administratorLoading: true,
      });
      renderShellAt(path);
      expect(screen.getByText(expectedText)).toBeInTheDocument();
    },
  );

  it("renders the Gestão form and the administrator queue independently for a multi-role user, without either blocking the other", () => {
    mockContexts({
      condominiumCount: 1,
      administrator: { managementCompanyId: "mc1" },
      administratorLoading: false,
    });
    const gestao = renderShellAt("/management/administrator/new");
    expect(
      screen.getByText("Formulário de nova solicitação"),
    ).toBeInTheDocument();
    gestao.unmount();

    const portal = renderShellAt("/administrator/requests");
    expect(screen.getByText("Fila da administradora")).toBeInTheDocument();
    portal.unmount();
  });

  it("shows a loading state (not the empty state) for a pure administrator-portal user while resolving", () => {
    mockContexts({
      condominiumCount: 0,
      administrator: null,
      administratorLoading: true,
    });
    renderShellAt("/administrator/requests");
    expect(
      screen.queryByText("Nenhum condomínio disponível"),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText("Fila da administradora"),
    ).not.toBeInTheDocument();
  });

  it("renders the administrator queue once the administrator context resolves", () => {
    mockContexts({
      condominiumCount: 0,
      administrator: { managementCompanyId: "mc1" },
      administratorLoading: false,
    });
    renderShellAt("/administrator/requests");
    expect(screen.getByText("Fila da administradora")).toBeInTheDocument();
  });

  it("shows the empty state only once every context has resolved to nothing", () => {
    mockContexts({
      condominiumCount: 0,
      administrator: null,
      administratorLoading: false,
    });
    renderShellAt("/administrator/requests");
    expect(
      screen.getByText("Nenhum condomínio disponível"),
    ).toBeInTheDocument();
  });
});
