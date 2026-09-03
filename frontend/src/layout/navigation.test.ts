import { describe, expect, it } from "vitest";
import {
  getMobileNavigationItems,
  getMoreNavigationItems,
  getMobileSelectedPath,
  getNavigationItems,
  shouldShowGeneralCondominiumSwitcher,
} from "./navigation";
import type { CondominiumContext } from "../condominiums/types";

describe("role-based navigation", () => {
  it("shows only common items to residents", () => {
    expect(getNavigationItems(["Resident"]).map((item) => item.label)).toEqual([
      "Solicitações",
    ]);
    expect(
      getMobileNavigationItems(["Resident"]).map((item) => item.label),
    ).toEqual(["Solicitações"]);
  });

  it("shows management resources to managers", () => {
    expect(
      getNavigationItems(["Manager", "Resident"]).map((item) => item.label),
    ).toEqual([
      "Dashboard",
      "Solicitações",
      "Atendimento",
      "Administradora",
      "Agenda",
      "Assistente",
      "Documentos",
      "Gestão",
    ]);
  });

  it("gives contextual management navigation to submanagers", () => {
    expect(
      getNavigationItems(["SubManager"]).map((item) => item.label),
    ).toContain("Administradora");
  });

  it("shows every granted module, including Assistant, without relying on array position", () => {
    const permissions = ["Documents", "Assistant", "Attendance", "Agenda", "Requests", "ManagementCompany", "Management"];
    expect(getNavigationItems(["SubManager"], [], permissions).map(item => item.label)).toEqual([
      "Solicitações", "Atendimento", "Administradora", "Agenda", "Assistente", "Documentos", "Gestão",
    ]);
    expect(getNavigationItems(["SubManager"], [], ["Attendance"]).map(item => item.label)).toEqual(["Atendimento"]);
    expect(getNavigationItems(["SubManager"], [], []).map(item => item.label)).toEqual([]);
    expect(getMobileNavigationItems(["SubManager"], [], permissions).map(item => item.label)).toEqual(["Solicitações", "Mais"]);
    expect(getMoreNavigationItems(["SubManager"], [], permissions).map(item => item.label)).toContain("Assistente");
  });

  it("keeps Manager unrestricted and Resident administrative modules blocked", () => {
    expect(getNavigationItems(["Manager"], [], []).map(item => item.label)).toContain("Assistente");
    expect(getNavigationItems(["Resident"], [], ["Assistant"]).map(item => item.label)).toEqual(["Solicitações"]);
  });

  it("shows Overwatch only to PlatformAdmin", () => {
    expect(
      getNavigationItems(["Resident"], ["PlatformAdmin"]).map(
        (item) => item.label,
      ),
    ).toContain("Overwatch");
    expect(
      getMobileNavigationItems(["Resident"], ["PlatformAdmin"]).map(
        (item) => item.label,
      ),
    ).toContain("Overwatch");
    expect(
      getNavigationItems(["Resident"]).map((item) => item.label),
    ).not.toContain("Overwatch");
    expect(
      getMobileNavigationItems(["Resident"]).map((item) => item.label),
    ).not.toContain("Overwatch");
  });

  it("keeps manager mobile navigation compact", () => {
    expect(
      getMobileNavigationItems(["Manager"]).map((item) => item.label),
    ).toEqual(["Dashboard", "Solicitações", "Mais"]);
  });

  it("marks the correct mobile destination for nested routes", () => {
    expect(getMobileSelectedPath("/requests/new")).toBe("/requests");
    expect(getMobileSelectedPath("/management/people")).toBe("/more");
    expect(getMobileSelectedPath("/more")).toBe("/more");
    expect(getMobileSelectedPath("/")).toBe("/");
    expect(getMobileSelectedPath("/overwatch/managers")).toBe("/overwatch");
    expect(getMobileSelectedPath("/management/reports")).toBe(
      "/management/dashboard",
    );
    expect(getMobileSelectedPath("/administrator/requests/abc")).toBe(
      "/administrator/requests",
    );
  });

  it("hides the general condominium switcher in management and for manager-only users", () => {
    const resident: CondominiumContext = {
      membershipId: "1",
      condominium: { id: "c1", name: "A", isActive: true },
      roles: ["Resident"],
      joinedAt: "",
      membershipActive: true,
    };
    const manager: CondominiumContext = {
      membershipId: "2",
      condominium: { id: "c2", name: "B", isActive: true },
      roles: ["Manager"],
      joinedAt: "",
      membershipActive: true,
    };
    expect(shouldShowGeneralCondominiumSwitcher("/", [resident])).toBe(true);
    expect(shouldShowGeneralCondominiumSwitcher("/", [manager])).toBe(false);
    expect(
      shouldShowGeneralCondominiumSwitcher("/management/units", [
        resident,
        manager,
      ]),
    ).toBe(false);
    expect(shouldShowGeneralCondominiumSwitcher("/overwatch", [resident])).toBe(
      false,
    );
  });
});
