import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import * as system from '../system'
import { OverwatchSystemPage } from './OverwatchSystemPage'

const response: system.SystemStatus = {
  generatedAt: '2026-08-18T12:00:00Z', globalStatus: 'Degraded',
  components: [
    { name:'API',status:'Healthy',detail:'respondendo' }, { name:'PostgreSQL',status:'Unhealthy',detail:'indisponível' },
    { name:'OpenAI',status:'Unknown',detail:'Sem atividade recente' }, { name:'E-mail',status:'Disabled',detail:'configuração' },
    { name:'Workers',status:'Degraded',detail:'heartbeat atrasado' },
  ],
  activity24h:{requestsCreated:2,whatsappReceived:3,whatsappSent:4,aiCalls:5,operationalErrors:1},
  workers:[{workerName:'WhatsAppOutboundWorker',instanceId:'node-a',status:'Degraded',enabled:true,lastHeartbeatAt:'2026-08-18T11:59:00Z',lastSucceeded:false,lastResultCode:'batch_failed'}],
  whatsapp:{status:'Degraded',queued:2,sending:1,failed:1,delivered:2,read:1,failed24h:1,sent24h:4,oldestQueuedAgeSeconds:37},
  ai:{status:'Unknown',configured:true,periods:[{period:'1h',metrics:{calls:0,failures:0,inputTokens:0,outputTokens:0,totalTokens:0}}],breakdown:[]},
  email:{status:'Disabled',enabled:false,configured:false,failures24h:0,successes24h:0},
  recentEvents:[{timestamp:'2026-08-18T11:58:00Z',component:'WhatsApp',category:'Outbound',severity:'Error',reasonCode:'batch_failed'}],
  performance:{periods:[{period:'24h',requests:120,averageMs:180,p95Ms:650,errors5xx:1,averageResponseBytes:2048,averageQueries:4.2,slowQueries:2}],topSlowest:[{method:'GET',route:'/requests/{requestId}',calls:20,averageMs:300,p95Ms:650,errors5xx:0,averageQueries:5,maximumQueries:7,slowQueries:1,averageResponseBytes:4096}]},
}
afterEach(()=>vi.restoreAllMocks())
describe('OverwatchSystemPage',()=>{
  it('renders states, workers, queue, inactive AI and recent errors',async()=>{
    vi.spyOn(system,'getSystemStatus').mockResolvedValue(response); render(<OverwatchSystemPage/>)
    expect(await screen.findByText('WhatsAppOutboundWorker')).toBeInTheDocument()
    expect(screen.getAllByText('Degradado').length).toBeGreaterThan(0)
    expect(screen.getByText('Indisponível')).toBeInTheDocument(); expect(screen.getAllByText('Desconhecido').length).toBeGreaterThan(0); expect(screen.getAllByText('Desabilitado').length).toBeGreaterThan(0); expect(screen.getByText('Item mais antigo: 37s · 1 falhas/24h')).toBeInTheDocument(); expect(screen.getByText('Sem atividade')).toBeInTheDocument(); expect(screen.getAllByText('batch_failed').length).toBeGreaterThan(0)
  })
  it('refreshes and exposes API errors',async()=>{
    const call=vi.spyOn(system,'getSystemStatus').mockResolvedValueOnce(response).mockRejectedValueOnce(new Error())
    render(<OverwatchSystemPage/>); await screen.findByText('WhatsAppOutboundWorker'); fireEvent.click(screen.getByRole('button',{name:'Atualizar'}))
    await waitFor(()=>expect(call).toHaveBeenCalledTimes(2)); expect(await screen.findByText('Não foi possível carregar a saúde do sistema.')).toBeInTheDocument()
  })
  it('exports with loading feedback and shows a safe error',async()=>{
    vi.spyOn(system,'getSystemStatus').mockResolvedValue(response)
    let finish!:()=>void
    const download=vi.spyOn(system,'downloadSystemDiagnostic').mockImplementation(()=>new Promise<void>(resolve=>{finish=resolve}))
    render(<OverwatchSystemPage/>); await screen.findByText('WhatsAppOutboundWorker')
    fireEvent.click(screen.getByRole('button',{name:'Exportar diagnóstico'}))
    expect(screen.getByRole('button',{name:'Gerando diagnóstico...'})).toBeDisabled()
    finish(); await waitFor(()=>expect(download).toHaveBeenCalledTimes(1))
    download.mockRejectedValueOnce(new Error('offline'))
    fireEvent.click(await screen.findByRole('button',{name:'Exportar diagnóstico'}))
    expect(await screen.findByText('Não foi possível gerar o diagnóstico.')).toBeInTheDocument()
  })
})
