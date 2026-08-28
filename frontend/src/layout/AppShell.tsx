import { Suspense } from "react";
import { Box, Button, Skeleton, Stack, Toolbar } from "@mui/material";
import { Outlet } from "react-router-dom";
import { AppHeader } from "./AppHeader";
import { MobileBottomNavigation } from "./MobileBottomNavigation";
import { Sidebar } from "./Sidebar";
import { useCondominium } from "../condominiums/CondominiumContext";
import { EmptyState } from "../components/EmptyState";
import { PageContainer } from "../components/PageContainer";
import { useAuth } from "../auth/AuthContext";
import { hasPlatformAdminAccess } from "../auth/permissions";
import { useManagementContext } from "../management/ManagementContext";
import { PwaInstallBanner } from "../pwa/PwaInstallBanner";
import { useAdministrator } from "../administrator/AdministratorContext";

export function AppShell() {
  const { currentCondominium, isLoading, error, refreshCondominiums } =
    useCondominium();
  const { user } = useAuth();
  const { condominiumCount, isLoading: isManagementLoading } =
    useManagementContext();
  const hasManagementContext = condominiumCount > 0;
  const { value: administrator, loading: administratorLoading } =
    useAdministrator();
  const hasContext =
    Boolean(currentCondominium) ||
    hasManagementContext ||
    Boolean(administrator);
  const showNavigation = hasContext || hasPlatformAdminAccess(user);

  const content =
    isLoading || isManagementLoading || administratorLoading ? (
      <PageContainer>
        <Stack spacing={2}>
          <Skeleton variant="rounded" height={180} />
          <Skeleton width="55%" />
          <Skeleton width="35%" />
        </Stack>
      </PageContainer>
    ) : error ? (
      <PageContainer>
        <EmptyState
          title="Não foi possível carregar seus condomínios"
          description={error}
          action={
            <Button
              variant="contained"
              onClick={() => void refreshCondominiums()}
            >
              Tentar novamente
            </Button>
          }
        />
      </PageContainer>
    ) : !hasContext && !hasPlatformAdminAccess(user) ? (
      <PageContainer>
        <EmptyState
          title="Nenhum condomínio disponível"
          description="Sua conta ainda não possui acesso a um condomínio. Entre em contato com o responsável pela administração."
        />
      </PageContainer>
    ) : (
      <Suspense
        fallback={
          <PageContainer>
            <Skeleton variant="rounded" height={240} />
          </PageContainer>
        }
      >
        <Outlet />
      </Suspense>
    );

  return (
    <Box minHeight="100dvh" display="flex">
      <AppHeader />
      {showNavigation && <Sidebar />}
      <Box
        component="main"
        flex={1}
        minWidth={0}
        pb={{ xs: showNavigation ? 9 : 2, md: 0 }}
        sx={{ overflowX: "hidden" }}
      >
        <Toolbar
          sx={{ minHeight: { xs: "64px !important", md: "72px !important" } }}
        />
        <PwaInstallBanner />
        {content}
      </Box>
      {showNavigation && <MobileBottomNavigation />}
    </Box>
  );
}
