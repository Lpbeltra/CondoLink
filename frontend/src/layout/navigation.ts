import ForumRoundedIcon from "@mui/icons-material/ForumRounded";
import SupportAgentRoundedIcon from "@mui/icons-material/SupportAgentRounded";
import ApartmentRoundedIcon from "@mui/icons-material/ApartmentRounded";
import AssessmentRoundedIcon from "@mui/icons-material/AssessmentRounded";
import MoreHorizRoundedIcon from "@mui/icons-material/MoreHorizRounded";
import AdminPanelSettingsRoundedIcon from "@mui/icons-material/AdminPanelSettingsRounded";
import AutoAwesomeRoundedIcon from "@mui/icons-material/AutoAwesomeRounded";
import DescriptionRoundedIcon from "@mui/icons-material/DescriptionRounded";
import EventNoteRoundedIcon from "@mui/icons-material/EventNoteRounded";
import BusinessRoundedIcon from "@mui/icons-material/BusinessRounded";
import type { SvgIconComponent } from "@mui/icons-material";
import type { CondominiumRole, CondominiumContext } from "../condominiums/types";

export interface NavigationItem {
  label: string;
  path: string;
  icon: SvgIconComponent;
  requiredRole?: CondominiumRole;
  requiredModule?: string;
  platformAdminOnly?: boolean;
  mobilePrimary?: boolean;
  residentAllowed?: boolean;
}

export const managementEntryPath = "/management/dashboard";

const commonItems: NavigationItem[] = [
  { label: "Dashboard", path: "/management/dashboard", icon: AssessmentRoundedIcon, requiredRole: "Manager", mobilePrimary: true },
  { label: "Solicitações", path: "/requests", icon: ForumRoundedIcon, requiredModule: "Requests", mobilePrimary: true, residentAllowed: true },
  { label: "Atendimento", path: "/management/requests", icon: SupportAgentRoundedIcon, requiredRole: "Manager", requiredModule: "Attendance" },
  { label: "Administradora", path: "/management/administrator", icon: BusinessRoundedIcon, requiredRole: "Manager", requiredModule: "ManagementCompany" },
  { label: "Agenda", path: "/management/agenda", icon: EventNoteRoundedIcon, requiredRole: "Manager", requiredModule: "Agenda" },
  { label: "Assistente", path: "/management/assistant", icon: AutoAwesomeRoundedIcon, requiredRole: "Manager", requiredModule: "Assistant" },
  { label: "Documentos", path: "/management/documents", icon: DescriptionRoundedIcon, requiredRole: "Manager", requiredModule: "Documents" },
  { label: "Gestão", path: "/management/units", icon: ApartmentRoundedIcon, requiredRole: "Manager", requiredModule: "Management" },
  { label: "Overwatch", path: "/overwatch", icon: AdminPanelSettingsRoundedIcon, platformAdminOnly: true, mobilePrimary: true },
];

export function canAccessNavigationItem(item: NavigationItem, roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]) {
  if (item.platformAdminOnly && !userRoles.includes("PlatformAdmin")) return false;
  if (!item.requiredRole && !item.requiredModule) return true;
  if (item.residentAllowed && roles.includes("Resident")) return true;
  if (roles.includes("Manager")) return true;
  return roles.includes("SubManager") && Boolean(item.requiredModule)
    && (subManagerPermissions === undefined || subManagerPermissions.includes(item.requiredModule!));
}

export function getNavigationItems(roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]) {
  return commonItems.filter(item => canAccessNavigationItem(item, roles, userRoles, subManagerPermissions));
}

export function getMoreNavigationItems(roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]) {
  return getNavigationItems(roles, userRoles, subManagerPermissions)
    .filter(item => !item.mobilePrimary && !item.platformAdminOnly);
}

export function getNavigationItemForPath(pathname: string) {
  return [...commonItems]
    .filter(item => pathname === item.path || pathname.startsWith(`${item.path}/`))
    .sort((left, right) => right.path.length - left.path.length)[0];
}

export function getMobileNavigationItems(roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]) {
  const items = getNavigationItems(roles, userRoles, subManagerPermissions);
  if (!roles.includes("Manager") && !roles.includes("SubManager")) return items;
  const primary = items.filter(item => item.mobilePrimary).slice(0, 3);
  return items.some(item => !primary.includes(item))
    ? [...primary, { label: "Mais", path: "/more", icon: MoreHorizRoundedIcon }]
    : primary;
}

export function getMobileSelectedPath(pathname: string) {
  if (pathname.startsWith("/administrator")) return "/administrator/requests";
  if (pathname.startsWith("/overwatch")) return "/overwatch";
  if (pathname.startsWith("/management/dashboard") || pathname.startsWith("/management/reports")) return "/management/dashboard";
  if (pathname === "/more" || pathname.startsWith("/management")) return "/more";
  if (pathname.startsWith("/requests")) return "/requests";
  return "/";
}

export function shouldShowGeneralCondominiumSwitcher(pathname: string, condominiums: CondominiumContext[]) {
  if (pathname.startsWith("/management") || pathname.startsWith("/overwatch")) return false;
  return condominiums.some(item => item.roles.includes("Resident"));
}
