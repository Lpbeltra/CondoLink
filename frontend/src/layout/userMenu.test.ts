import { describe, expect, it, vi } from "vitest";
import { getUserMenuAreaAction, runUserMenuAreaAction } from "./userMenu";
import type { User } from "../auth/types";

function user(roles: string[]): User {
  return {
    id: "user-id",
    fullName: "Test User",
    email: "test@example.com",
    isActive: true,
    roles,
  };
}

describe("user profile area switcher", () => {
  it("sends PlatformAdmin outside Overwatch to Overwatch", () => {
    expect(getUserMenuAreaAction(user(["PlatformAdmin"]), "/", false)).toEqual({
      label: "Ir para Overwatch",
      path: "/overwatch",
      kind: "overwatch",
    });
  });

  it("sends PlatformAdmin with Manager access back to management", () => {
    const action = getUserMenuAreaAction(
      user(["PlatformAdmin"]),
      "/overwatch/management-companies",
      true,
    );

    expect(action).toEqual({
      label: "Voltar ao acesso de síndico",
      path: "/management/dashboard",
      kind: "management",
    });
    expect(action?.label).not.toBe("Ir para Overwatch");
  });

  it("does not offer management to PlatformAdmin without Manager access", () => {
    expect(
      getUserMenuAreaAction(user(["PlatformAdmin"]), "/overwatch", false),
    ).toBeNull();
  });

  it("does not offer Overwatch to Manager without PlatformAdmin", () => {
    expect(getUserMenuAreaAction(user([]), "/", true)).toBeNull();
  });

  it("closes the menu before navigating", () => {
    const closeMenu = vi.fn();
    const navigate = vi.fn();
    const action = getUserMenuAreaAction(user(["PlatformAdmin"]), "/", false);

    runUserMenuAreaAction(action!, closeMenu, navigate);

    expect(closeMenu).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith("/overwatch");
    expect(closeMenu.mock.invocationCallOrder[0]).toBeLessThan(
      navigate.mock.invocationCallOrder[0],
    );
  });

  it("does not mutate the authenticated session while switching areas", () => {
    const authenticatedUser = user(["PlatformAdmin"]);
    const originalRoles = [...authenticatedUser.roles!];
    const token = "stored-token";
    const closeMenu = vi.fn();
    const navigate = vi.fn();
    const action = getUserMenuAreaAction(authenticatedUser, "/", true);

    runUserMenuAreaAction(action!, closeMenu, navigate);

    expect(authenticatedUser.roles).toEqual(originalRoles);
    expect(token).toBe("stored-token");
  });
});
