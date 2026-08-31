import { api } from "../services/api";
import type {
  CreationOptions,
  ManagementCompanyRequestStatus,
  ManagementCompanyRequestType,
  PageResult,
  RequestDetail,
  AdministratorContext,
  AdministratorOptions,
} from "./types";
export async function listRequests(p: {
  condominiumId?: string;
  type?: ManagementCompanyRequestType;
  status?: ManagementCompanyRequestStatus;
  search?: string;
  from?: string;
  to?: string;
  page: number;
}) {
  return (
    await api.get<PageResult>("/management-company-requests", {
      params: { ...p, pageSize: 20 },
    })
  ).data;
}
export async function getOptions(condominiumId: string) {
  return (
    await api.get<CreationOptions>("/management-company-requests/options", {
      params: { condominiumId },
    })
  ).data;
}
export async function getRequest(id: string) {
  return (await api.get<RequestDetail>(`/management-company-requests/${id}`))
    .data;
}
function form(payload: unknown, files: File[]) {
  const f = new FormData();
  f.append("payload", JSON.stringify(payload));
  files.forEach((x) => f.append("files", x));
  return f;
}
export async function createRequest(
  type: ManagementCompanyRequestType,
  payload: unknown,
  files: File[],
) {
  const route =
    type === "Fine" ? "fines" : type === "Payment" ? "payments" : "questions";
  return (
    await api.post<{ id: string; friendlyIdentifier: string }>(
      `/management-company-requests/${route}/multipart`,
      form(payload, files),
    )
  ).data;
}
export async function interact(id: string, content: string, files: File[]) {
  await api.post(
    `/management-company-requests/${id}/interactions`,
    form({ content, targetStatus: null }, files),
  );
}
export async function completePayment(
  id: string,
  files: File[],
) {
  await api.post(
    `/management-company-requests/${id}/complete-payment`,
    form({}, files),
  );
}
export async function cancelRequest(id: string, reason: string) {
  await api.post(`/management-company-requests/${id}/cancel`, { reason });
}
export async function attachmentBlob(id: string) {
  return (
    await api.get<Blob>(
      `/management-company-request-attachments/${id}/content`,
      { responseType: "blob" },
    )
  ).data;
}
export async function getAdministratorContext() {
  return (await api.get<AdministratorContext>("/administrator/context")).data;
}
export async function getAdministratorOptions() {
  return (
    await api.get<AdministratorOptions>("/administrator/requests/options")
  ).data;
}
export async function listAdministratorRequests(p: {
  condominiumId?: string;
  categoryId?: string;
  status?: ManagementCompanyRequestStatus;
  search?: string;
  from?: string;
  to?: string;
  includeCompleted?: boolean;
  includeCancelled?: boolean;
  page: number;
}) {
  return (
    await api.get<PageResult>("/administrator/requests", {
      params: { ...p, pageSize: 20 },
    })
  ).data;
}
export async function changeRequestStatus(
  id: string,
  status: ManagementCompanyRequestStatus,
  reason: string | null = null,
) {
  await api.post(`/management-company-requests/${id}/status`, {
    status,
    reason,
  });
}
export async function startRequestProcessing(id: string) {
  await api.post(`/management-company-requests/${id}/start-processing`);
}
export async function requestManagerInformation(
  id: string,
  content: string,
  files: File[],
) {
  await api.post(`/management-company-requests/${id}/interactions`, form({ content, targetStatus: null }, files));
}
