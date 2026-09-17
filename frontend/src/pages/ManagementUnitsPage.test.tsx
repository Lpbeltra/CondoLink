import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Unit } from "../management/types";

const managementApi = vi.hoisted(() => ({ listUnits: vi.fn() }));

vi.mock("../management/api", () => managementApi);
vi.mock("../management/ManagementContext", () => ({
  useManagementContext: () => ({ activeCondominiumId: "condominium-id" }),
}));

import { ManagementUnitsPage } from "./ManagementUnitsPage";

const unit = (id: string, identifier: string, block: string | null, peopleCount = 0): Unit => ({
  id,
  condominiumId: "condominium-id",
  identifier,
  blockId: block ? `block-${block}` : null,
  block,
  floor: null,
  description: null,
  isActive: true,
  peopleCount,
  createdAt: "",
  updatedAt: "",
});

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/management/units"]}>
      <Routes>
        <Route path="/management/units" element={<ManagementUnitsPage />} />
        <Route path="/management/units/:unitId" element={<div>Detalhe da unidade</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("ManagementUnitsPage", () => {
  beforeEach(() => {
    managementApi.listUnits.mockReset();
  });

  it("groups units by naturally sorted block and preserves natural unit order", async () => {
    managementApi.listUnits.mockResolvedValue([
      unit("unit-10", "10", "2"),
      unit("unit-101", "101", "1", 2),
      unit("unit-2", "2", "1", 1),
      unit("unit-standalone", "50", null),
    ]);
    renderPage();

    expect(await screen.findByText("Bloco 1")).toBeInTheDocument();
    expect(screen.getByText("Bloco 2")).toBeInTheDocument();
    expect(screen.getByText("Sem bloco")).toBeInTheDocument();
    expect(screen.getAllByRole("link").map((link) => link.textContent)).toEqual([
      expect.stringContaining("2"),
      expect.stringContaining("101"),
      expect.stringContaining("10"),
      expect.stringContaining("50"),
    ]);
    expect(screen.getAllByText("Sem moradores")).toHaveLength(2);
    expect(screen.queryByRole("button", { name: /criar|editar|excluir/i })).not.toBeInTheDocument();
  });

  it("renders a direct directory without an artificial group when there are no blocks", async () => {
    managementApi.listUnits.mockResolvedValue([
      unit("unit-10", "10", null),
      unit("unit-2", "2", null),
      unit("unit-1", "1", null),
    ]);
    renderPage();

    await screen.findByText("1");
    expect(screen.queryByText("Sem bloco")).not.toBeInTheDocument();
    expect(screen.getAllByRole("link").map((link) => link.textContent)).toEqual([
      expect.stringContaining("1"),
      expect.stringContaining("2"),
      expect.stringContaining("10"),
    ]);
  });

  it("filters by unit or block and opens the unit detail", async () => {
    managementApi.listUnits.mockResolvedValue([
      unit("unit-101", "101", "1"),
      unit("unit-201", "201", "2"),
    ]);
    const user = userEvent.setup();
    renderPage();

    await user.type(await screen.findByLabelText("Buscar unidade"), "Bloco 2");
    expect(screen.queryByRole("link", { name: /101/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: /201/ }));
    expect(await screen.findByText("Detalhe da unidade")).toBeInTheDocument();
  });
});
