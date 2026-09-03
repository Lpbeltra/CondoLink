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
import type { SubManagerModule } from "../overwatch/submanagers/api";

export interface NavigationItem {
  label: string;
  path: string;
  icon: SvgIconComponent;
  requiredRole?: CondominiumRole;
  requiredModule?: SubManagerModule;
  platformAdminOnly?: boolean;
  mobilePrimary?: boolean;
  mobilePriority?: number;
  residentAllowed?: boolean;
}

export interface MobileNavigationParts {
  allowed: NavigationItem[];
  bottom: NavigationItem[];
  more: NavigationItem[];
}

export const managementEntryPath = "/management/dashboard";

const commonItems: NavigationItem[] = [
  { label: "Dashboard", path: "/management/dashboard", icon: AssessmentRoundedIcon, requiredRole: "Manager", mobilePrimary: true, mobilePriority: 10 },
  { label: "Solicitações", path: "/requests", icon: ForumRoundedIcon, requiredModule: "Requests", mobilePrimary: true, mobilePriority: 20, residentAllowed: true },
  { label: "Atendimento", path: "/management/requests", icon: SupportAgentRoundedIcon, requiredRole: "Manager", requiredModule: "Attendance", mobilePriority: 40 },
  { label: "Administradora", path: "/management/administrator", icon: BusinessRoundedIcon, requiredRole: "Manager", requiredModule: "ManagementCompany", mobilePriority: 70 },
  { label: "Agenda", path: "/management/agenda", icon: EventNoteRoundedIcon, requiredRole: "Manager", requiredModule: "Agenda", mobilePriority: 50 },
  { label: "Assistente", path: "/management/assistant", icon: AutoAwesomeRoundedIcon, requiredRole: "Manager", requiredModule: "Assistant", mobilePrimary: true, mobilePriority: 30 },
  { label: "Documentos", path: "/management/documents", icon: DescriptionRoundedIcon, requiredRole: "Manager", requiredModule: "Documents", mobilePriority: 60 },
  { label: "Gestão", path: "/management/units", icon: ApartmentRoundedIcon, requiredRole: "Manager", requiredModule: "Management", mobilePriority: 80 },
  { label: "Overwatch", path: "/overwatch", icon: AdminPanelSettingsRoundedIcon, platformAdminOnly: true, mobilePrimary: true, mobilePriority: 5 },
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

export function getMobileNavigationParts(roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]): MobileNavigationParts {
  const allowed = getNavigationItems(roles, userRoles, subManagerPermissions);
  if (!roles.includes("Manager") && !roles.includes("SubManager")) return { allowed, bottom: allowed, more: [] };
  const candidates = allowed.filter(item => item.mobilePrimary || item.mobilePriority !== undefined)
    .sort((left, right) => (left.mobilePriority ?? Number.MAX_SAFE_INTEGER) - (right.mobilePriority ?? Number.MAX_SAFE_INTEGER));
  const bottom = candidates.slice(0, 3);
  const bottomSet = new Set(bottom);
  const more = allowed.filter(item => !bottomSet.has(item) && !item.platformAdminOnly);
  return { allowed, bottom, more };
}

export function getMoreNavigationItems(roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]) {
  return getMobileNavigationParts(roles, userRoles, subManagerPermissions).more;
}

export function getNavigationItemForPath(pathname: string) {
  return [...commonItems]
    .filter(item => pathname === item.path || pathname.startsWith(`${item.path}/`))
    .sort((left, right) => right.path.length - left.path.length)[0];
}

export function getMobileNavigationItems(roles: CondominiumRole[], userRoles: string[] = [], subManagerPermissions?: string[]) {
  const { bottom, more } = getMobileNavigationParts(roles, userRoles, subManagerPermissions);
  return more.length ? [...bottom, { label: "Mais", path: "/more", icon: MoreHorizRoundedIcon }] : bottom;
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
