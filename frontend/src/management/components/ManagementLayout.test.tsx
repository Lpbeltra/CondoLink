import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { ManagementLayout } from "./ManagementLayout";
import {
  ManagementReactContext,
  type ManagementContextValue,
} from "../ManagementContext";
import type { ManagementCondominium } from "../types";

const condominium: ManagementCondominium = {
  id: "c1",
  name: "Cond A",
  isActive: true,
};

function contextValue(
  overrides: Partial<ManagementContextValue> = {},
): ManagementContextValue {
  return {
    condominiums: [condominium],
    activeCondominiumId: "c1",
    activeCondominium: condominium,
    condominiumCount: 1,
    usesConsolidatedManagementScope: false,
    hasEligibleManagementCompany: true,
    isLoading: false,
    isSwitching: false,
    error: null,
    refresh: async () => {},
    selectCondominium: async () => {},
    ...overrides,
  };
}

function renderAt(path: string, value = contextValue()) {
  return render(
    <ManagementReactContext.Provider value={value}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/management" element={<ManagementLayout />}>
            <Route
              path="administrator"
              element={<div>Fila da administradora</div>}
            />
            <Route
              path="administrator/new"
              element={<div>Formulário de nova solicitação</div>}
            />
            <Route
              path="administrator/:id"
              element={<div>Detalhe da solicitação</div>}
            />
            <Route path="units" element={<div>Lista de unidades</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </ManagementReactContext.Provider>,
  );
}

const gestaoChrome = ["Gestão", "Unidades", "Blocos", "Configuração", "Categorias", "Pessoas"];

describe("ManagementLayout composition", () => {
  it("renders the administrator queue without the Gestão chrome", () => {
    renderAt("/management/administrator");
    expect(screen.getByText("Fila da administradora")).toBeInTheDocument();
    gestaoChrome.forEach((label) =>
      expect(screen.queryByText(label)).not.toBeInTheDocument(),
    );
  });

  it("renders the new administrator request form without the Gestão chrome", () => {
    renderAt("/management/administrator/new");
    expect(
      screen.getByText("Formulário de nova solicitação"),
    ).toBeInTheDocument();
    gestaoChrome.forEach((label) =>
      expect(screen.queryByText(label)).not.toBeInTheDocument(),
    );
  });

  it("renders the administrator request detail without the Gestão chrome", () => {
    renderAt("/management/administrator/req-1");
    expect(screen.getByText("Detalhe da solicitação")).toBeInTheDocument();
    gestaoChrome.forEach((label) =>
      expect(screen.queryByText(label)).not.toBeInTheDocument(),
    );
  });

  it("still shows the Gestão title and tabs for real management routes", () => {
    renderAt("/management/units");
    expect(screen.getByText("Lista de unidades")).toBeInTheDocument();
    expect(screen.getByText("Gestão")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Unidades" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Blocos" })).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: "Configuração" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: "Categorias" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Pessoas" })).toBeInTheDocument();
  });
});
