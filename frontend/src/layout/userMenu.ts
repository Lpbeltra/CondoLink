import { hasPlatformAdminAccess } from "../auth/permissions";
import type { User } from "../auth/types";
import { managementEntryPath } from "./navigation";

export interface UserMenuAreaAction {
  label: "Ir para Overwatch" | "Voltar ao acesso de síndico";
  path: string;
  kind: "overwatch" | "management";
}

export function getUserMenuAreaAction(
  user: User | null,
  pathname: string,
  hasManagerAccess: boolean,
): UserMenuAreaAction | null {
  if (!hasPlatformAdminAccess(user)) return null;

  if (pathname.startsWith("/overwatch")) {
    return hasManagerAccess
      ? {
          label: "Voltar ao acesso de síndico",
          path: managementEntryPath,
          kind: "management",
        }
      : null;
  }

  return {
    label: "Ir para Overwatch",
    path: "/overwatch",
    kind: "overwatch",
  };
}

export function runUserMenuAreaAction(
  action: UserMenuAreaAction,
  closeMenu: () => void,
  navigate: (path: string) => void,
) {
  closeMenu();
  navigate(action.path);
}
