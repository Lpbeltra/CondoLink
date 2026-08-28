import { useState } from "react";
import LogoutRoundedIcon from "@mui/icons-material/LogoutRounded";
import {
  AppBar,
  Avatar,
  Box,
  ButtonBase,
  IconButton,
  ListItemIcon,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
  alpha,
} from "@mui/material";
import { Brand } from "../components/Brand";
import { NotificationBell } from "../notifications/NotificationBell";
import { ThemeModeToggle } from "../theme/ThemeModeToggle";
import { useAuth } from "../auth/AuthContext";
import { CondominiumSwitcher } from "./CondominiumSwitcher";
import { useCondominium } from "../condominiums/CondominiumContext";
import { useLocation, useNavigate } from "react-router-dom";
import { shouldShowGeneralCondominiumSwitcher } from "./navigation";
import { useOptionalManagementContext } from "../management/ManagementContext";
import { useAdministrator } from "../administrator/AdministratorContext";

export function AppHeader() {
  const { user, logout } = useAuth();
  const { condominiums } = useCondominium();
  const location = useLocation();
  const navigate = useNavigate();
  const managementContext = useOptionalManagementContext();
  const { value: administrator } = useAdministrator();
  const showSwitcher = shouldShowGeneralCondominiumSwitcher(
    location.pathname,
    condominiums,
  );
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const brandPath = location.pathname.startsWith("/overwatch")
    ? "/overwatch"
    : (managementContext?.condominiumCount ?? 0) > 0
      ? "/management/dashboard"
      : administrator
        ? "/administrator/requests"
        : "/";
  const initials = user?.fullName
    .split(" ")
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
  return (
    <AppBar
      position="fixed"
      color="inherit"
      elevation={0}
      sx={(theme) => ({
        zIndex: theme.zIndex.drawer + 1,
        borderBottom: "1px solid",
        borderColor: "divider",
        bgcolor: alpha(theme.palette.background.paper, 0.9),
        backdropFilter: "blur(16px)",
      })}
    >
      <Toolbar
        sx={{
          minHeight: { xs: 64, md: 72 },
          px: { xs: 1.5, sm: 2, md: 3 },
          minWidth: 0,
          gap: { xs: 1, sm: 1.5 },
        }}
      >
        <ButtonBase
          aria-label="Ir para a página principal"
          onClick={() => navigate(brandPath)}
          sx={{ display: { xs: "flex", sm: "none" }, flexShrink: 0 }}
        >
          <Brand compact />
        </ButtonBase>
        <ButtonBase
          aria-label="Ir para a página principal"
          onClick={() => navigate(brandPath)}
          sx={{ display: { xs: "none", sm: "flex" }, flexShrink: 0 }}
        >
          <Brand />
        </ButtonBase>
        <Box minWidth={0} flex="1 1 auto" overflow="hidden">
          {showSwitcher && <CondominiumSwitcher />}
        </Box>
        <Typography
          color="text.secondary"
          fontSize=".875rem"
          noWrap
          sx={{
            display: { xs: "none", xl: "block" },
            maxWidth: 220,
            flexShrink: 1,
          }}
        >
          {administrator
            ? `${administrator.jobTitle} · ${administrator.managementCompanyName}`
            : user?.fullName}
        </Typography>
        {(!administrator ||
          condominiums.length > 0 ||
          (managementContext?.condominiumCount ?? 0) > 0) && (
          <NotificationBell />
        )}
        <ThemeModeToggle />
        <Tooltip title="Conta e sair">
          <IconButton
            aria-label="Abrir menu do usuário"
            aria-haspopup="menu"
            aria-controls={anchor ? "user-menu" : undefined}
            aria-expanded={anchor ? "true" : undefined}
            onClick={(event) => setAnchor(event.currentTarget)}
            sx={{ p: 0.5, flex: "0 0 auto", minWidth: 44, minHeight: 44 }}
          >
            <Avatar
              sx={{
                width: 36,
                height: 36,
                bgcolor: "primary.main",
                fontSize: ".8rem",
                fontWeight: 750,
              }}
            >
              {initials}
            </Avatar>
          </IconButton>
        </Tooltip>
      </Toolbar>
      <Menu
        id="user-menu"
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Box px={2} py={1} maxWidth={280}>
          <Typography fontWeight={750} noWrap>
            {user?.fullName}
          </Typography>
          <Typography color="text.secondary" fontSize=".8rem" noWrap>
            {user?.email}
          </Typography>
        </Box>
        <MenuItem
          onClick={() => {
            setAnchor(null);
            logout();
          }}
          sx={{ minHeight: 44 }}
        >
          <ListItemIcon>
            <LogoutRoundedIcon fontSize="small" />
          </ListItemIcon>
          Sair
        </MenuItem>
      </Menu>
    </AppBar>
  );
}
