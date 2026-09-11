import { Alert } from '@mui/material'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'
import { useCondominiumModules } from '../modules/useCondominiumModules'
import { ModuleUnavailable } from '../modules/ModuleUnavailable'
import { EmployeesManager } from './EmployeesManager'

export function ManagementEmployeesPage() {
  const { activeCondominiumId } = useManagementContext()
  const { isModuleEnabled, loading } = useCondominiumModules()

  if (!activeCondominiumId) return <PageContainer><Alert severity="info">Selecione um condomínio.</Alert></PageContainer>
  if (loading) return <PageContainer><Alert severity="info">Carregando…</Alert></PageContainer>
  if (!isModuleEnabled('EmployeeManagement')) return <PageContainer><ModuleUnavailable /></PageContainer>

  return (
    <PageContainer maxWidth={1200}>
      <EmployeesManager condominiumId={activeCondominiumId} />
    </PageContainer>
  )
}
