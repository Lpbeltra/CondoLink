import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CondominiumDocumentsPage } from './CondominiumDocumentsPage'
const api=vi.hoisted(()=>({listDocuments:vi.fn(),uploadDocument:vi.fn(),deleteDocument:vi.fn(),downloadDocument:vi.fn(),reprocessDocument:vi.fn(),setDocumentActive:vi.fn()}))
vi.mock('../assistant/api',async original=>({...await original<typeof import('../assistant/api')>(),...api}))
vi.mock('../management/ManagementContext',()=>({useManagementContext:()=>({activeCondominiumId:'condo-1'})}))
const renderPage=()=>render(<MemoryRouter><CondominiumDocumentsPage/></MemoryRouter>)
describe('CondominiumDocumentsPage batch upload',()=>{
 beforeEach(()=>{Object.values(api).forEach(x=>x.mockReset());api.listDocuments.mockResolvedValue([])})
 it('selects many files, defaults titles, applies a type to all, changes one and removes one',async()=>{
  const user=userEvent.setup();const {container}=renderPage();const input=container.querySelector<HTMLInputElement>('input[type=file]')!
  await user.upload(input,[new File(['a'],'Ata A.pdf'),new File(['b'],'Ata B.docx')])
  expect(screen.getByRole('textbox',{name:'Nome Ata A.pdf'})).toHaveValue('Ata A');expect(screen.getByRole('textbox',{name:'Nome Ata B.docx'})).toHaveValue('Ata B')
  await user.click(screen.getByRole('combobox',{name:'Tipo para todos'}));await user.click(screen.getByRole('option',{name:'Ata'}));await user.click(screen.getByRole('button',{name:'Aplicar aos arquivos'}))
  expect(screen.getAllByRole('combobox',{name:/Tipo Ata/}).every(x=>x.textContent==='Ata')).toBe(true)
  await user.click(screen.getByRole('combobox',{name:'Tipo Ata B.docx'}));await user.click(screen.getByRole('option',{name:'Contrato'}));expect(screen.getByRole('combobox',{name:'Tipo Ata B.docx'})).toHaveTextContent('Contrato')
  await user.click(screen.getByRole('button',{name:'Remover Ata A.pdf'}));expect(screen.queryByText('Ata A.pdf')).not.toBeInTheDocument()
 })
 it('keeps per-file limits and partial failures independent',async()=>{
  const user=userEvent.setup();api.uploadDocument.mockResolvedValueOnce({processingStatus:'Ready'}).mockRejectedValueOnce(new Error('internal'));const {container}=renderPage();const large=new File(['x'],'grande.pdf');Object.defineProperty(large,'size',{value:25*1024*1024+1});fireEvent.change(container.querySelector('input[type=file]')!,{target:{files:[new File(['a'],'ok.pdf'),new File(['b'],'falha.pdf'),large]}})
  expect(await screen.findByText('O arquivo excede o limite de 25 MB.')).toBeInTheDocument();await user.click(screen.getByRole('button',{name:'Enviar documentos'}));await waitFor(()=>expect(api.uploadDocument).toHaveBeenCalledTimes(2));expect(await screen.findByText(/3 documentos enviados · 1 processados · 0 não suportados · 2 falharam/)).toBeInTheDocument();expect(screen.getByText('ok.pdf').closest('.MuiCard-root')).toHaveTextContent('Pronto');expect(screen.getByText('falha.pdf').closest('.MuiCard-root')).toHaveTextContent('Falhou')
 })
 it('shows Portuguese type/status labels and filters by name and type',async()=>{
  const user=userEvent.setup();api.listDocuments.mockResolvedValue([{id:'1',name:'Ata Março',documentType:'Minutes',documentDate:'2026-03-15',originalFileName:'ata.pdf',version:1,isActive:true,processingStatus:'Ready',processingError:null},{id:'2',name:'Convenção',documentType:'Convention',originalFileName:'conv.pdf',version:1,isActive:false,processingStatus:'Ready',processingError:null}]);renderPage();expect(await screen.findByText('Ata Março')).toBeInTheDocument();expect(screen.getByText(/Ata · 2026-03-15/)).toBeInTheDocument();expect(screen.getByText('Pronto')).toBeInTheDocument();expect(screen.getByText('Inativo')).toBeInTheDocument();await user.type(screen.getByRole('textbox',{name:'Buscar documentos'}),'Março');expect(screen.queryByText('Convenção')).not.toBeInTheDocument();await user.clear(screen.getByRole('textbox',{name:'Buscar documentos'}));await user.click(screen.getByRole('combobox',{name:'Filtrar por tipo'}));const list=screen.getByRole('listbox');await user.click(within(list).getByRole('option',{name:'Convenção'}));expect(screen.queryByText('Ata Março')).not.toBeInTheDocument()
 })
})
