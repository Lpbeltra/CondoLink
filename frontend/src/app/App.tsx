import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider } from '../auth/AuthProvider'
import { useAuth } from '../auth/AuthContext'
import { LoadingScreen } from '../components/LoadingScreen'
import { AppShell } from '../layout/AppShell'
import { HomePage } from '../pages/HomePage'
import { LoginPage } from '../pages/LoginPage'
import { AppThemeProvider } from '../theme/AppThemeProvider'
import { CondominiumProvider } from '../condominiums/CondominiumProvider'
import { MyRequestsPage } from '../pages/MyRequestsPage'
import { CreateRequestPage } from '../pages/CreateRequestPage'
import { ManagementRequestDetailsPage, RequestDetailsPage } from '../pages/RequestDetailsPage'
import { ManagementRequestsPage } from '../pages/ManagementRequestsPage'
import { ManagementLayout } from '../management/components/ManagementLayout'
import { ManagementUnitsPage } from '../pages/ManagementUnitsPage'
import { CreateUnitPage } from '../pages/CreateUnitPage'
import { UnitDetailsPage } from '../pages/UnitDetailsPage'
import { ManagementCategoriesPage } from '../pages/ManagementCategoriesPage'
import { MorePage } from '../pages/MorePage'
import { ManagementPeoplePage } from '../pages/ManagementPeoplePage'
import { ManagementReportsPage } from '../pages/ManagementReportsPage'
import { ManagementBlocksPage } from '../pages/ManagementBlocksPage'
import { ManagementContextProvider } from '../management/ManagementContextProvider'
import { OverwatchGuard } from '../overwatch/OverwatchGuard'
import { OverwatchLayout } from '../overwatch/OverwatchLayout'
import { OverwatchDashboardPage } from '../overwatch/pages/OverwatchDashboardPage'
import { OverwatchCondominiumsPage } from '../overwatch/pages/OverwatchCondominiumsPage'
import { OverwatchCondominiumDetailsPage } from '../overwatch/pages/OverwatchCondominiumDetailsPage'
import { OverwatchManagementCompaniesPage } from '../overwatch/pages/OverwatchManagementCompaniesPage'
import { OverwatchManagementCompanyDetailsPage } from '../overwatch/pages/OverwatchManagementCompanyDetailsPage'
import { OverwatchManagersPage } from '../overwatch/pages/OverwatchManagersPage'
import { OverwatchManagerDetailsPage } from '../overwatch/pages/OverwatchManagerDetailsPage'
import { getProtectedRouteAccess } from '../auth/routeAccess'

function ProtectedRoute() {
  const { user, isInitializing } = useAuth()
  const location = useLocation()
  const access = getProtectedRouteAccess(isInitializing, user)
  if (access === 'loading') return <LoadingScreen />
  return access === 'authenticated'
    ? <Outlet />
    : <Navigate to="/login" replace state={{ from: location.pathname }} />
}

export function App() {
  return (
    <AppThemeProvider>
      <BrowserRouter>
        <AuthProvider>
          <CondominiumProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={
                  <ManagementContextProvider>
                    <AppShell />
                  </ManagementContextProvider>
                }>
                  <Route index element={<HomePage />} />
                  <Route path="requests" element={<MyRequestsPage />} />
                  <Route path="requests/new" element={<CreateRequestPage />} />
                  <Route path="requests/:requestId" element={<RequestDetailsPage />} />
                  <Route path="more" element={<MorePage />} />
                  <Route path="management" element={<ManagementLayout />}>
                    <Route index element={<Navigate to="units" replace />} />
                    <Route path="requests" element={<ManagementRequestsPage />} />
                    <Route path="requests/:requestId" element={<ManagementRequestDetailsPage />} />
                    <Route path="units" element={<ManagementUnitsPage />} />
                    <Route path="units/new" element={<CreateUnitPage />} />
                    <Route path="units/:unitId" element={<UnitDetailsPage />} />
                    <Route path="blocks" element={<ManagementBlocksPage />} />
                    <Route path="categories" element={<ManagementCategoriesPage />} />
                    <Route path="people" element={<ManagementPeoplePage />} />
                    <Route path="reports" element={<ManagementReportsPage />} />
                  </Route>
                </Route>
                <Route element={<OverwatchGuard />}>
                  <Route path="overwatch" element={<OverwatchLayout />}>
                    <Route index element={<OverwatchDashboardPage />} />
                    <Route path="condominiums" element={<OverwatchCondominiumsPage />} />
                    <Route path="condominiums/:condominiumId" element={<OverwatchCondominiumDetailsPage />} />
                    <Route path="management-companies" element={<OverwatchManagementCompaniesPage />} />
                    <Route path="management-companies/:managementCompanyId" element={<OverwatchManagementCompanyDetailsPage />} />
                    <Route path="managers" element={<OverwatchManagersPage />} />
                    <Route path="managers/:managerId" element={<OverwatchManagerDetailsPage />} />
                  </Route>
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </CondominiumProvider>
        </AuthProvider>
      </BrowserRouter>
    </AppThemeProvider>
  )
}
