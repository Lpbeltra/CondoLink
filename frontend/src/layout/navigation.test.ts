import { describe, expect, it } from "vitest";
import { getMobileNavigationItems, getMobileNavigationParts, getMobileSelectedPath, getMoreNavigationItems, getNavigationItems, shouldShowGeneralCondominiumSwitcher } from "./navigation";
import type { CondominiumContext } from "../condominiums/types";

const allPermissions = ["Attendance", "ManagementCompany", "Agenda", "Assistant", "Documents", "Management"];

describe("role-based navigation", () => {
  it("keeps resident navigation free of administrative modules", () => {
    expect(getNavigationItems(["Resident"], [], allPermissions)).toHaveLength(1);
  });

  it("does not expose Employee Management to condominium roles", () => {
    expect(getNavigationItems(["Manager", "Resident"]).map(item => item.path)).not.toContain("/management/employees");
    expect(getNavigationItems(["SubManager"], [], [...allPermissions, "EmployeeManagement"]).map(item => item.path)).not.toContain("/management/employees");
  });

  it("keeps configurable SubManager navigation and union permissions", () => {
    const paths = getNavigationItems(["SubManager"], [], allPermissions).map(item => item.path);
    expect(paths).toContain("/management/requests");
    expect(paths).toContain("/management/documents");
    expect(getNavigationItems(["SubManager"], [], []).map(item => item.path)).toEqual([]);
  });

  it("keeps platform navigation and nested mobile selection", () => {
    expect(getNavigationItems(["Resident"], ["PlatformAdmin"]).map(item => item.path)).toContain("/overwatch");
    expect(getMobileSelectedPath("/administrator/requests/abc")).toBe("/administrator/requests");
    expect(getMobileSelectedPath("/management/reports")).toBe("/management/dashboard");
    expect(getMobileNavigationItems(["Manager"], ["PlatformAdmin"]).length).toBeGreaterThan(0);
    const parts = getMobileNavigationParts(["SubManager"], [], allPermissions);
    expect(new Set([...parts.bottom, ...parts.more]).size).toBe(parts.allowed.length);
    expect(getMoreNavigationItems(["SubManager"], [], allPermissions)).toEqual(parts.more);
  });

  it("hides the general condominium switcher in management contexts", () => {
    const resident: CondominiumContext = { membershipId: "1", condominium: { id: "c1", name: "A", isActive: true }, roles: ["Resident"], joinedAt: "", membershipActive: true };
    const manager: CondominiumContext = { membershipId: "2", condominium: { id: "c2", name: "B", isActive: true }, roles: ["Manager"], joinedAt: "", membershipActive: true };
    expect(shouldShowGeneralCondominiumSwitcher("/", [resident])).toBe(true);
    expect(shouldShowGeneralCondominiumSwitcher("/", [manager])).toBe(false);
    expect(shouldShowGeneralCondominiumSwitcher("/management/units", [resident, manager])).toBe(false);
  });
});
