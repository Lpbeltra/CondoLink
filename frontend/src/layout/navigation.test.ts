import { describe, expect, it } from "vitest";
import { getMobileNavigationItems, getMobileNavigationParts, getMobileSelectedPath, getMoreNavigationItems, getNavigationItems, shouldShowGeneralCondominiumSwitcher } from "./navigation";
import type { CondominiumContext } from "../condominiums/types";

const allPermissions = ["Attendance", "ManagementCompany", "Agenda", "Assistant", "Documents", "Management"];

describe("role-based navigation", () => {
  it("keeps Resident navigation free of administrative modules", () => {
    expect(getNavigationItems(["Resident"], [], allPermissions).map(item => item.label)).toEqual(["Solicitações"]);
    expect(getMobileNavigationItems(["Resident"], [], allPermissions).map(item => item.label)).toEqual(["Solicitações"]);
  });

  it("gives Manager the complete catalog", () => {
    expect(getNavigationItems(["Manager", "Resident"]).map(item => item.label)).toEqual(["Dashboard", "Atendimento", "Administradora", "Agenda", "Prestadores", "Assistente", "Documentos", "Gestão"]);
    expect(getMobileNavigationItems(["Manager"]).map(item => item.label)).toEqual(["Dashboard", "Atendimento", "Assistente", "Mais"]);
  });

  it("maps six configurable SubManager permissions, including Assistant", () => {
    expect(getNavigationItems(["SubManager"], [], allPermissions).map(item => item.path)).toEqual([
      "/management/requests", "/management/administrator", "/management/agenda", "/management/service-providers", "/management/assistant", "/management/documents", "/management/units",
    ]);
    expect(getNavigationItems(["SubManager"], [], ["Assistant"]).map(item => item.label)).toEqual(["Assistente"]);
    expect(getNavigationItems(["SubManager"], [], []).map(item => item.label)).toEqual([]);
  });

  it("uses union permissions for consolidated SubManager navigation", () => {
    const union = [...new Set(["Attendance", "Documents"])]
    expect(getNavigationItems(["SubManager"], [], union).map(item => item.label))
      .toEqual(["Atendimento", "Documentos"])
  });

  it("partitions authorized modules without loss or duplication", () => {
    const parts = getMobileNavigationParts(["SubManager"], [], allPermissions);
    expect(parts.allowed.map(item => item.label)).toEqual(["Atendimento", "Administradora", "Agenda", "Prestadores", "Assistente", "Documentos", "Gestão"]);
    expect(parts.bottom.map(item => item.label)).toEqual(["Atendimento", "Assistente", "Agenda"]);
    expect(parts.more.map(item => item.label)).toEqual(["Administradora", "Prestadores", "Documentos", "Gestão"]);
    expect(new Set([...parts.bottom, ...parts.more]).size).toBe(parts.allowed.length);
    expect(getMoreNavigationItems(["SubManager"], [], allPermissions)).toEqual(parts.more);
  });

  it("preserves legacy full access when permission payload is absent", () => {
    expect(getNavigationItems(["SubManager"]).map(item => item.label)).toContain("Administradora");
  });

  it("shows Overwatch only to PlatformAdmin", () => {
    expect(getNavigationItems(["Resident"], ["PlatformAdmin"]).map(item => item.label)).toContain("Overwatch");
    expect(getNavigationItems(["Resident"]).map(item => item.label)).not.toContain("Overwatch");
    expect(getMobileNavigationItems(["Manager"], ["PlatformAdmin"]).map(item => item.label))
      .toEqual(["Dashboard", "Atendimento", "Assistente", "Mais"]);
    expect(getMoreNavigationItems(["Manager"], ["PlatformAdmin"]).map(item => item.label))
      .toContain("Overwatch");
  });

  it("marks the correct mobile destination for nested routes", () => {
    expect(getMobileSelectedPath("/requests/new")).toBe("/requests");
    expect(getMobileSelectedPath("/management/people")).toBe("/more");
    expect(getMobileSelectedPath("/more")).toBe("/more");
    expect(getMobileSelectedPath("/")).toBe("/");
    expect(getMobileSelectedPath("/overwatch/managers")).toBe("/more");
    expect(getMobileSelectedPath("/management/reports")).toBe("/management/dashboard");
    expect(getMobileSelectedPath("/administrator/requests/abc")).toBe("/administrator/requests");
  });

  it("hides the general condominium switcher in management and for manager-only users", () => {
    const resident: CondominiumContext = { membershipId: "1", condominium: { id: "c1", name: "A", isActive: true }, roles: ["Resident"], joinedAt: "", membershipActive: true };
    const manager: CondominiumContext = { membershipId: "2", condominium: { id: "c2", name: "B", isActive: true }, roles: ["Manager"], joinedAt: "", membershipActive: true };
    expect(shouldShowGeneralCondominiumSwitcher("/", [resident])).toBe(true);
    expect(shouldShowGeneralCondominiumSwitcher("/", [manager])).toBe(false);
    expect(shouldShowGeneralCondominiumSwitcher("/management/units", [resident, manager])).toBe(false);
    expect(shouldShowGeneralCondominiumSwitcher("/overwatch", [resident])).toBe(false);
  });
});
