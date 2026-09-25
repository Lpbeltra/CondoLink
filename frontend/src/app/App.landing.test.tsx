import type { PropsWithChildren } from "react";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

const session = vi.hoisted(() => ({ user: null as null | { roles: string[] }, isInitializing: false }));
vi.mock("../auth/AuthContext", () => ({ useAuth: () => session }));
vi.mock("../theme/AppThemeProvider", () => ({ AppThemeProvider: ({ children }: PropsWithChildren) => children }));
vi.mock("../auth/AuthProvider", () => ({ AuthProvider: ({ children }: PropsWithChildren) => children }));
vi.mock("../condominiums/CondominiumProvider", () => ({ CondominiumProvider: ({ children }: PropsWithChildren) => children }));
vi.mock("../management/ManagementContextProvider", () => ({ ManagementContextProvider: ({ children }: PropsWithChildren) => children }));
vi.mock("../administrator/AdministratorProvider", () => ({ AdministratorProvider: ({ children }: PropsWithChildren) => children }));
vi.mock("../layout/AppShell", async () => {
  const { Outlet } = await import("react-router-dom");
  return { AppShell: () => <Outlet /> };
});
vi.mock("../components/LoadingScreen", () => ({ LoadingScreen: () => <div>Restoring session</div> }));
vi.mock("../pages/HomePage", () => ({ HomePage: () => <div>Existing profile entry</div> }));
vi.mock("../pages/LoginPage", () => ({ LoginPage: () => <div>Existing login</div> }));

describe("landing route boundaries", () => {
  beforeEach(() => {
    session.user = null;
    session.isInitializing = false;
    window.history.replaceState({}, "", "/");
  });

  it("renders the real public landing at root", () => {
    render(<App />);
    expect(screen.getByRole("heading", { level: 1, name: /Seu condomínio já se comunica/ })).toBeVisible();
  });

  it("preserves the login route", () => {
    window.history.replaceState({}, "", "/login");
    render(<App />);
    expect(screen.getByText("Existing login")).toBeVisible();
  });

  it.each(["Manager", "SubManager", "Resident", "Administrator"])("hands %s sessions to the existing app entry", async (role) => {
    session.user = { roles: [role] };
    render(<App />);
    expect(await screen.findByText("Existing profile entry")).toBeVisible();
    expect(window.location.pathname).toBe("/app");
    expect(screen.queryByRole("heading", { name: /Seu condomínio já se comunica/ })).not.toBeInTheDocument();
  });

  it("waits for session restoration before showing the public page", () => {
    session.isInitializing = true;
    render(<App />);
    expect(screen.getByText("Restoring session")).toBeVisible();
    expect(screen.queryByRole("heading", { name: /Seu condomínio já se comunica/ })).not.toBeInTheDocument();
  });

  it("keeps the app entry protected", async () => {
    window.history.replaceState({}, "", "/app");
    render(<App />);
    expect(await screen.findByText("Existing login")).toBeVisible();
    expect(window.location.pathname).toBe("/login");
  });
});
