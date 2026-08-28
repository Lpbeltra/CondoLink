import { BottomNavigation, BottomNavigationAction, Paper } from "@mui/material";
import { useLocation, useNavigate } from "react-router-dom";
import { getMobileNavigationItems, getMobileSelectedPath } from "./navigation";
import { useCondominium } from "../condominiums/CondominiumContext";
import { useAuth } from "../auth/AuthContext";
import { useManagementContext } from "../management/ManagementContext";
import { useAdministrator } from "../administrator/AdministratorContext";
import BusinessRoundedIcon from "@mui/icons-material/BusinessRounded";

export function MobileBottomNavigation() {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentCondominium } = useCondominium();
  const { user } = useAuth();
  const { condominiumCount, hasEligibleManagementCompany } =
    useManagementContext();
  const { value: administrator } = useAdministrator();
  const roles = currentCondominium?.roles ?? [];
  let navigationItems = getMobileNavigationItems(
    condominiumCount > 0 && !roles.includes("Manager")
      ? [...roles, "Manager"]
      : roles,
    user?.roles ?? [],
  ).filter(
    (item) =>
      item.path !== "/management/administrator" || hasEligibleManagementCompany,
  );
  if (administrator && condominiumCount === 0 && !currentCondominium)
    navigationItems = [];
  if (administrator)
    navigationItems.push({
      label: "Solicitações",
      path: "/administrator/requests",
      icon: BusinessRoundedIcon,
    });
  const selectedPath = getMobileSelectedPath(location.pathname);
  return (
    <Paper
      elevation={0}
      sx={{
        display: { md: "none" },
        position: "fixed",
        zIndex: 1200,
        bottom: 0,
        left: 0,
        right: 0,
        borderTop: "1px solid",
        borderColor: "divider",
        pb: "env(safe-area-inset-bottom)",
        borderRadius: 0,
      }}
    >
      <BottomNavigation
        aria-label="Navegação principal"
        value={selectedPath}
        onChange={(_, value) => navigate(value)}
        showLabels
      >
        {navigationItems.map(({ label, path, icon: Icon }) => (
          <BottomNavigationAction
            key={path}
            label={label}
            value={path}
            icon={<Icon />}
          />
        ))}
      </BottomNavigation>
    </Paper>
  );
}
