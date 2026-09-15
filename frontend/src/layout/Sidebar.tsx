import {
  Box,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
} from "@mui/material";
import { NavLink } from "react-router-dom";
import { Brand } from "../components/Brand";
import { useCondominium } from "../condominiums/CondominiumContext";
import { getNavigationItems } from "./navigation";
import { useAuth } from "../auth/AuthContext";
import { useManagementContext } from "../management/ManagementContext";
import { useAdministrator } from "../administrator/AdministratorContext";
import BusinessRoundedIcon from "@mui/icons-material/BusinessRounded";
import BadgeRoundedIcon from "@mui/icons-material/BadgeRounded";

export const drawerWidth = 232;

function navigationGroup(path: string) {
  if (path === "/management/dashboard" || path === "/requests" || path === "/management/requests" || path === "/management/agenda") return "OPERAÇÃO";
  if (path === "/management/service-providers" || path === "/management/assistant" || path === "/management/documents") return "RECURSOS";
  if (path === "/management/administrator" || path === "/management/units" || path.startsWith("/administrator/")) return "ADMINISTRAÇÃO";
  return "SISTEMA";
}

export function Sidebar() {
  const { currentCondominium } = useCondominium();
  const { user } = useAuth();
  const { condominiumCount, hasEligibleManagementCompany, subManagerPermissions, managementRoles } =
    useManagementContext();
  const { value: administrator } = useAdministrator();
  const roles = ((managementRoles ?? []).length ? managementRoles : currentCondominium?.roles ?? []) as never;
  let navigationItems = getNavigationItems(
    roles,
    user?.roles ?? [],
    subManagerPermissions,
  ).filter(
    (item) =>
      item.path !== "/management/administrator" || hasEligibleManagementCompany,
  );
  if (administrator && condominiumCount === 0 && !currentCondominium)
    navigationItems = [];
  if (administrator) {
    navigationItems.push({
      label: "Solicitações",
      path: "/administrator/requests",
      icon: BusinessRoundedIcon,
    });
    navigationItems.push({
      label: "Funcionários",
      path: administrator.hasEmployeeManagementAccess ? "/administrator/employees" : "",
      icon: BadgeRoundedIcon,
    });
  }
  navigationItems = navigationItems.filter((item) => item.path !== "");
  return (
    <Drawer
      variant="permanent"
      sx={{
        display: { xs: "none", md: "block" },
        width: drawerWidth,
        "& .MuiDrawer-paper": {
          width: drawerWidth,
          borderRight: "1px solid",
          borderColor: "divider",
          bgcolor: "background.default",
        },
      }}
    >
      <Toolbar sx={{ minHeight: "calc(68px + env(safe-area-inset-top)) !important", px: 2.5, pt: "env(safe-area-inset-top)" }}>
        <Box
          component={NavLink}
          to={
            administrator && condominiumCount === 0
              ? "/administrator/requests"
              : condominiumCount > 0
                ? "/management/dashboard"
                : "/"
          }
          aria-label="Ir para a página principal"
          sx={{ color: "inherit", textDecoration: "none" }}
        >
          <Brand />
        </Box>
      </Toolbar>
      <List
        component="nav"
        aria-label="Navegação principal"
        sx={{ px: 1.25, pt: 1.5 }}
      >
        {Array.from(new Set(navigationItems.map((item) => navigationGroup(item.path))).values()).map((group) => (
          <Box key={group} sx={{ mb: 1.5 }}>
            <Typography component="div" variant="overline" color="text.secondary" sx={{ px: 1.5, mb: .5, display: "block", fontSize: ".65rem", letterSpacing: ".12em", fontWeight: 800 }}>
              {group}
            </Typography>
            {navigationItems.filter((item) => navigationGroup(item.path) === group).map(({ label, path, icon: Icon }) => (
              <ListItemButton
                key={path}
                component={NavLink}
                to={path}
                sx={{
                  mb: 0.25,
                  color: "text.secondary",
                  position: "relative",
                  "&::before": { content: '""', position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, bgcolor: "transparent" },
                  "&.active": { bgcolor: "action.selected", color: "primary.main", "&::before": { bgcolor: "primary.main" } },
                  "&:hover": { bgcolor: "action.hover" },
                }}
              >
                <ListItemIcon sx={{ minWidth: 36, color: "inherit" }}><Icon fontSize="small" /></ListItemIcon>
                <ListItemText primary={label} primaryTypographyProps={{ fontWeight: 650, fontSize: ".875rem" }} />
              </ListItemButton>
            ))}
          </Box>
        ))}
      </List>
    </Drawer>
  );
}
