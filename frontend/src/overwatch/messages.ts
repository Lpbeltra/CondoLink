import { api } from '../services/api'

export interface OperationalMessageTemplate {
  key: string; title: string; description: string; prefix: string; suffix: string
  structuralSuffix: string; dynamicContent: string; mode: string; modeLabel: string
  metaTemplateName: string | null; metaTemplateLanguage: string | null
  isOverride: boolean; updatedAt: string | null; updatedByUserId: string | null
  partMaximumLength: number; outboundMaximumLength: number
}
export async function listOperationalMessages() { return (await api.get<OperationalMessageTemplate[]>('/overwatch/messages')).data }
export async function updateOperationalMessage(key: string, prefix: string, suffix: string) { return (await api.put<OperationalMessageTemplate>(`/overwatch/messages/${key}`, { prefix, suffix })).data }
export async function restoreOperationalMessage(key: string) { return (await api.delete<OperationalMessageTemplate>(`/overwatch/messages/${key}`)).data }
export function previewMessage(template: OperationalMessageTemplate) {
  const replace = (value: string) => value.replaceAll('{PrimeiroNome}', 'Maria').replaceAll('{NomeCondominio}', 'Residencial Exemplo')
  return [replace(template.prefix), '[Mensagem escrita pelo síndico]', replace(template.suffix), template.structuralSuffix].filter((part) => part.trim()).join('\n\n')
}
