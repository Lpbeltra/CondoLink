import {
  BrowserRouter,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { AuthProvider } from "../auth/AuthProvider";
import { useAuth } from "../auth/AuthContext";
import { LoadingScreen } from "../components/LoadingScreen";
import { AppShell } from "../layout/AppShell";
import { HomePage } from "../pages/HomePage";
import { LoginPage } from "../pages/LoginPage";
import { ChangePasswordPage } from "../pages/ChangePasswordPage";
import { FirstAccessPage } from "../pages/FirstAccessPage";
import { AppThemeProvider } from "../theme/AppThemeProvider";
import { CondominiumProvider } from "../condominiums/CondominiumProvider";
import { ManagementLayout } from "../management/components/ManagementLayout";
import { ManagementContextProvider } from "../management/ManagementContextProvider";
import { OverwatchGuard } from "../overwatch/OverwatchGuard";
import { OverwatchLayout } from "../overwatch/OverwatchLayout";
import { getProtectedRouteAccess } from "../auth/routeAccess";
import { lazyPage } from "./lazyPage";
import { AdministratorProvider } from "../administrator/AdministratorProvider";

const MyRequestsPage = lazyPage(
  () => import("../pages/MyRequestsPage"),
  "MyRequestsPage",
);
const CreateRequestPage = lazyPage(
  () => import("../pages/CreateRequestPage"),
  "CreateRequestPage",
);
const RequestDetailsPage = lazyPage(
  () => import("../pages/RequestDetailsPage"),
  "RequestDetailsPage",
);
const ManagementRequestDetailsPage = lazyPage(
  () => import("../pages/RequestDetailsPage"),
  "ManagementRequestDetailsPage",
);
const ManagementRequestsPage = lazyPage(
  () => import("../pages/ManagementRequestsPage"),
  "ManagementRequestsPage",
);
const ManagementUnitsPage = lazyPage(
  () => import("../pages/ManagementUnitsPage"),
  "ManagementUnitsPage",
);
const CreateUnitPage = lazyPage(
  () => import("../pages/CreateUnitPage"),
  "CreateUnitPage",
);
const UnitDetailsPage = lazyPage(
  () => import("../pages/UnitDetailsPage"),
  "UnitDetailsPage",
);
const ManagementCategoriesPage = lazyPage(
  () => import("../pages/ManagementCategoriesPage"),
  "ManagementCategoriesPage",
);
const MorePage = lazyPage(() => import("../pages/MorePage"), "MorePage");
const ManagementPeoplePage = lazyPage(
  () => import("../pages/ManagementPeoplePage"),
  "ManagementPeoplePage",
);
const ManagementReportsPage = lazyPage(
  () => import("../pages/ManagementReportsPage"),
  "ManagementReportsPage",
);
const ManagementAgendaPage = lazyPage(
  () => import("../pages/ManagementAgendaPage"),
  "ManagementAgendaPage",
);
const ManagementBlocksPage = lazyPage(
  () => import("../pages/ManagementBlocksPage"),
  "ManagementBlocksPage",
);
const CondominiumSetupPage = lazyPage(
  () => import("../pages/CondominiumSetupPage"),
  "CondominiumSetupPage",
);
const CondominiumAssistantPage = lazyPage(
  () => import("../pages/CondominiumAssistantPage"),
  "CondominiumAssistantPage",
);
const CondominiumDocumentsPage = lazyPage(
  () => import("../pages/CondominiumDocumentsPage"),
  "CondominiumDocumentsPage",
);
const ManagementCompanyRequestsPage = lazyPage(
  () => import("../pages/ManagementCompanyRequestsPage"),
  "ManagementCompanyRequestsPage",
);
const CreateManagementCompanyRequestPage = lazyPage(
  () => import("../pages/CreateManagementCompanyRequestPage"),
  "CreateManagementCompanyRequestPage",
);
const ManagementCompanyRequestDetailsPage = lazyPage(
  () => import("../pages/ManagementCompanyRequestDetailsPage"),
  "ManagementCompanyRequestDetailsPage",
);
const AdministratorRequestsPage = lazyPage(
  () => import("../administrator/AdministratorRequestsPage"),
  "AdministratorRequestsPage",
);
const AdministratorRequestDetailsPage = lazyPage(
  () => import("../administrator/AdministratorRequestDetailsPage"),
  "AdministratorRequestDetailsPage",
);
const OverwatchDashboardPage = lazyPage(
  () => import("../overwatch/pages/OverwatchDashboardPage"),
  "OverwatchDashboardPage",
);
const OverwatchCondominiumsPage = lazyPage(
  () => import("../overwatch/pages/OverwatchCondominiumsPage"),
  "OverwatchCondominiumsPage",
);
const OverwatchCondominiumDetailsPage = lazyPage(
  () => import("../overwatch/pages/OverwatchCondominiumDetailsPage"),
  "OverwatchCondominiumDetailsPage",
);
const OverwatchManagementCompaniesPage = lazyPage(
  () => import("../overwatch/pages/OverwatchManagementCompaniesPage"),
  "OverwatchManagementCompaniesPage",
);
const OverwatchManagementCompanyDetailsPage = lazyPage(
  () => import("../overwatch/pages/OverwatchManagementCompanyDetailsPage"),
  "OverwatchManagementCompanyDetailsPage",
);
const OverwatchManagersPage = lazyPage(
  () => import("../overwatch/pages/OverwatchManagersPage"),
  "OverwatchManagersPage",
);
const OverwatchSubManagersPage = lazyPage(
  () => import("../overwatch/pages/OverwatchSubManagersPage"),
  "OverwatchSubManagersPage",
);
const OverwatchManagerDetailsPage = lazyPage(
  () => import("../overwatch/pages/OverwatchManagerDetailsPage"),
  "OverwatchManagerDetailsPage",
);
const OverwatchSystemPage = lazyPage(
  () => import("../overwatch/pages/OverwatchSystemPage"),
  "OverwatchSystemPage",
);
const OverwatchMessagesPage = lazyPage(
  () => import("../overwatch/pages/OverwatchMessagesPage"),
  "OverwatchMessagesPage",
);

function ProtectedRoute() {
  const { user, isInitializing } = useAuth();
  const location = useLocation();
  const access = getProtectedRouteAccess(isInitializing, user);
  if (access === "loading") return <LoadingScreen />;
  return access === "authenticated" ? (
    <ManagementContextProvider>
      <AdministratorProvider>
        <Outlet />
      </AdministratorProvider>
    </ManagementContextProvider>
  ) : (
    <Navigate to="/login" replace state={{ from: location.pathname }} />
  );
}

export function App() {
  return (
    <AppThemeProvider>
      <BrowserRouter>
        <AuthProvider>
          <CondominiumProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/change-password" element={<ChangePasswordPage />} />
              <Route path="/primeiro-acesso" element={<FirstAccessPage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<AppShell />}>
                  <Route index element={<HomePage />} />
                  <Route path="requests" element={<MyRequestsPage />} />
                  <Route path="requests/new" element={<CreateRequestPage />} />
                  <Route
                    path="requests/:requestId"
                    element={<RequestDetailsPage />}
                  />
                  <Route path="more" element={<MorePage />} />
                  <Route
                    path="administrator/requests"
                    element={<AdministratorRequestsPage />}
                  />
                  <Route
                    path="administrator/requests/:id"
                    element={<AdministratorRequestDetailsPage />}
                  />
                  <Route path="management" element={<ManagementLayout />}>
                    <Route
                      index
                      element={<Navigate to="dashboard" replace />}
                    />
                    <Route
                      path="requests"
                      element={<ManagementRequestsPage />}
                    />
                    <Route
                      path="requests/:requestId"
                      element={<ManagementRequestDetailsPage />}
                    />
                    <Route path="agenda" element={<ManagementAgendaPage />} />
                    <Route
                      path="administrator"
                      element={<ManagementCompanyRequestsPage />}
                    />
                    <Route
                      path="administrator/new"
                      element={<CreateManagementCompanyRequestPage />}
                    />
                    <Route
                      path="administrator/:id"
                      element={<ManagementCompanyRequestDetailsPage />}
                    />
                    <Route path="units" element={<ManagementUnitsPage />} />
                    <Route path="units/new" element={<CreateUnitPage />} />
                    <Route path="units/:unitId" element={<UnitDetailsPage />} />
                    <Route path="blocks" element={<ManagementBlocksPage />} />
                    <Route path="setup" element={<CondominiumSetupPage />} />
                    <Route
                      path="categories"
                      element={<ManagementCategoriesPage />}
                    />
                    <Route path="people" element={<ManagementPeoplePage />} />
                    <Route
                      path="assistant"
                      element={<CondominiumAssistantPage />}
                    />
                    <Route
                      path="documents"
                      element={<CondominiumDocumentsPage />}
                    />
                    <Route
                      path="dashboard"
                      element={<ManagementReportsPage />}
                    />
                    <Route
                      path="reports"
                      element={<Navigate to="../dashboard" replace />}
                    />
                  </Route>
                </Route>
                <Route element={<OverwatchGuard />}>
                  <Route path="overwatch" element={<OverwatchLayout />}>
                    <Route index element={<OverwatchDashboardPage />} />
                    <Route
                      path="condominiums"
                      element={<OverwatchCondominiumsPage />}
                    />
                    <Route
                      path="condominiums/:condominiumId"
                      element={<OverwatchCondominiumDetailsPage />}
                    />
                    <Route
                      path="condominiums/:condominiumId/setup"
                      element={<CondominiumSetupPage />}
                    />
                    <Route
                      path="management-companies"
                      element={<OverwatchManagementCompaniesPage />}
                    />
                    <Route
                      path="management-companies/:managementCompanyId"
                      element={<OverwatchManagementCompanyDetailsPage />}
                    />
                    <Route
                      path="managers"
                      element={<OverwatchManagersPage />}
                    />
                    <Route
                      path="submanagers"
                      element={<OverwatchSubManagersPage />}
                    />
                    <Route
                      path="managers/:managerId"
                      element={<OverwatchManagerDetailsPage />}
                    />
                    <Route path="system" element={<OverwatchSystemPage />} />
                    <Route
                      path="messages"
                      element={<OverwatchMessagesPage />}
                    />
                  </Route>
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </CondominiumProvider>
        </AuthProvider>
      </BrowserRouter>
    </AppThemeProvider>
  );
}
