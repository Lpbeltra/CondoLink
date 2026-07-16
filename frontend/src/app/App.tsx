import { CssBaseline, ThemeProvider } from '@mui/material'
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider } from '../auth/AuthProvider'
import { useAuth } from '../auth/AuthContext'
import { LoadingScreen } from '../components/LoadingScreen'
import { AppShell } from '../layout/AppShell'
import { HomePage } from '../pages/HomePage'
import { LoginPage } from '../pages/LoginPage'
import { theme } from '../theme'
import { CondominiumProvider } from '../condominiums/CondominiumProvider'
import { MyRequestsPage } from '../pages/MyRequestsPage'
import { CreateRequestPage } from '../pages/CreateRequestPage'
import { RequestDetailsPage } from '../pages/RequestDetailsPage'
import { ManagementRequestsPage } from '../pages/ManagementRequestsPage'

function ProtectedRoute() {
  const { user, isInitializing } = useAuth()
  const location = useLocation()
  if (isInitializing) return <LoadingScreen />
  return user ? <Outlet /> : <Navigate to="/login" replace state={{ from: location.pathname }} />
}

export function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <AuthProvider>
          <CondominiumProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<AppShell />}>
                  <Route index element={<HomePage />} />
                  <Route path="requests" element={<MyRequestsPage />} />
                  <Route path="requests/new" element={<CreateRequestPage />} />
                  <Route path="requests/:requestId" element={<RequestDetailsPage />} />
                  <Route path="management/requests" element={<ManagementRequestsPage />} />
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </CondominiumProvider>
        </AuthProvider>
      </BrowserRouter>
    </ThemeProvider>
  )
}
