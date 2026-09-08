import { api } from '../services/api'
export type PixKeyType = 'Cpf'|'Cnpj'|'Email'|'Phone'|'Random'
export interface ServiceProvider { id:string; name:string; companyName:string|null; specialty:string; contactName:string|null; phone:string; email:string|null; pixKey:string|null; pixKeyType:PixKeyType|null; notes:string|null; isActive:boolean; isMine:boolean; condominiums:{condominiumId:string;name:string}[] }
export interface ServiceProviderInput { name:string; companyName:string; specialty:string; contactName:string; phone:string; email:string; pixKey:string; pixKeyType:PixKeyType|null; notes:string; isMine:boolean; condominiumIds:string[]; isActive:boolean }
export const listServiceProviders=async(params:Record<string,string|undefined>)=>(await api.get<ServiceProvider[]>('/management/service-providers',{params})).data
export const createServiceProvider=async(input:ServiceProviderInput)=>(await api.post<ServiceProvider>('/management/service-providers',input)).data
export const updateServiceProvider=async(id:string,input:ServiceProviderInput)=>(await api.put<ServiceProvider>(`/management/service-providers/${id}`,input)).data
export const setServiceProviderActive=async(id:string,active:boolean)=>api.post(`/management/service-providers/${id}/${active?'activate':'deactivate'}`)
