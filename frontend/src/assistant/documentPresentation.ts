export const documentTypes = ['Convention','InternalRules','Minutes','Contract','Manual','Notice','Other'] as const
export type DocumentType = typeof documentTypes[number]
export const documentTypeLabels: Record<DocumentType,string> = {
  Convention:'Convenção', InternalRules:'Regimento interno', Minutes:'Ata', Contract:'Contrato',
  Manual:'Manual', Notice:'Comunicado', Other:'Outro',
}
export const documentStatusLabels: Record<string,string> = {
  Pending:'Aguardando', Processing:'Processando', Ready:'Pronto', Failed:'Falhou',
  Unsupported:'Não suportado', ReindexRequired:'Reindexação necessária', Inactive:'Inativo',
}
export function documentTypeLabel(value:string) { return documentTypeLabels[value as DocumentType] ?? value }
export function documentVisualStatus(document:{isActive:boolean;processingStatus:string;needsReindexing?:boolean}) {
  if (!document.isActive) return 'Inactive'; if (document.needsReindexing) return 'ReindexRequired'; return document.processingStatus
}
