import { Button, Card, CardContent, Stack, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { PageContainer } from '../components/PageContainer'
import { useManagementContext } from '../management/ManagementContext'

export function MorePage() { const navigate = useNavigate(); const { condominiumCount } = useManagementContext(); const hasManagementAccess = condominiumCount > 0; return <PageContainer><Typography variant="h1">Mais</Typography><Card elevation={0} sx={{ mt: 2 }}><CardContent><Stack gap={1.5}>{hasManagementAccess && <><Button variant="outlined" onClick={() => navigate('/management/requests')}>Atendimento</Button><Button variant="outlined" onClick={() => navigate('/management/units')}>Gestão</Button></>}</Stack></CardContent></Card></PageContainer> }
