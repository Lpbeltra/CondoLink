import { api } from '../services/api'

export interface EmployeeDocumentBatch { id:string; managementCompanyId?:string|null; documentType:string; competenceMonth:number; competenceYear:number; status:string; createdAt:string; createdByName:string; failureReason:string|null; processingStage:string|null; processedItems:number; totalItems:number|null; progressPercentage:number|null; documentCount:number; identifiedCount:number; needsReviewCount:number; unidentifiedCount:number; ignoredCount:number; confirmedCount:number }
export interface EmployeeDocument { id:string; batchId:string; employeeId:string|null; employeeName:string|null; condominiumId:string|null; condominiumName:string|null; cpf:string|null; documentType:string; competenceMonth:number; competenceYear:number; originalFileName:string; pageStart:number; pageEnd:number; identificationStatus:'Unidentified'|'NeedsReview'|'Identified'|'Confirmed'|'Ignored'; identificationConfidence:string; identificationMethod:string; createdAt:string; updatedAt:string; confirmedAt:string|null; possibleDuplicate?:boolean }
export interface EmployeeDocumentBatchDetail { batch:EmployeeDocumentBatch & {selectedCount?:number;condominiumCount?:number}; documents:EmployeeDocument[] }
export interface DistributionSummary { batchId:string;totalConfirmed:number;ready:number;noPhone:number;invalidPhone:number;alreadyQueuedOrSent:number }
export interface EmployeeDocumentDelivery { employeeDocumentId:string;employeeId:string;employeeName:string;status:string;attemptCount:number;queuedAt:string;sentAt:string|null;deliveredAt:string|null;readAt:string|null;failedAt:string|null;lastErrorCode:string|null;lastErrorDescription:string|null }
const root='/administrator/employees/documents/batches'
export const listBatches=async()=> (await api.get<EmployeeDocumentBatch[]>(root)).data
export const getBatch=async(batchId:string)=> (await api.get<EmployeeDocumentBatchDetail>(`${root}/${batchId}`)).data
export async function uploadBatch(files:File[],competenceMonth:number,competenceYear:number,employeeIds:string[]){const f=new FormData();f.append('competenceMonth',String(competenceMonth));f.append('competenceYear',String(competenceYear));files.forEach(x=>f.append('files',x));employeeIds.forEach(x=>f.append('employeeIds',x));return (await api.post<{id:string;status:string}>(root,f)).data}
export type DocumentAction='Assign'|'Clear'|'Ignore'|'Confirm'
export const updateDocumentAssociation=async(batchId:string,documentId:string,action:DocumentAction,employeeId?:string)=>(await api.patch<EmployeeDocument>(`${root}/${batchId}/documents/${documentId}`,{action,employeeId:employeeId??null})).data
export async function replaceDocumentFile(batchId:string,documentId:string,file:File){const f=new FormData();f.append('file',file);await api.put(`${root}/${batchId}/documents/${documentId}/file`,f)}
export const confirmBatch=async(batchId:string)=>{await api.post(`${root}/${batchId}/confirm`)}
export const reopenBatch=async(batchId:string)=>{await api.post(`${root}/${batchId}/reopen`)}
export const getDistributionSummary=async(batchId:string)=>(await api.get<DistributionSummary>(`${root}/${batchId}/distribution-summary`)).data
export const distributeBatch=async(batchId:string)=>(await api.post<{queued:number}>(`${root}/${batchId}/distribute`)).data
export const listDeliveries=async(batchId:string)=>(await api.get<EmployeeDocumentDelivery[]>(`${root}/${batchId}/deliveries`)).data
export const resendDocument=async(batchId:string,documentId:string)=>{await api.post(`${root}/${batchId}/documents/${documentId}/resend`)}
export async function previewDocumentUrl(batchId:string,documentId:string){const r=await api.get(`${root}/${batchId}/documents/${documentId}/preview`,{responseType:'blob'});return URL.createObjectURL(r.data as Blob)}
