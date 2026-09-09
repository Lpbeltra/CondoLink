import HandymanRoundedIcon from '@mui/icons-material/HandymanRounded'
import { Card, CardActionArea, CardContent, Stack, Typography } from '@mui/material'
import { Link } from 'react-router-dom'
import { PageContainer } from '../../components/PageContainer'
export function OverwatchToolsPage() { return <PageContainer><Typography variant="h1">Ferramentas administrativas</Typography><Typography color="text.secondary" mt={1}>Operações globais de suporte e manutenção da plataforma.</Typography><Stack mt={3} maxWidth={520}><Card elevation={0}><CardActionArea component={Link} to="service-providers"><CardContent><Stack direction="row" gap={2} alignItems="center"><HandymanRoundedIcon color="primary"/><div><Typography variant="h2">Prestadores de serviço</Typography><Typography color="text.secondary">Consulta global e exclusão permanente segura.</Typography></div></Stack></CardContent></CardActionArea></Card></Stack></PageContainer> }
