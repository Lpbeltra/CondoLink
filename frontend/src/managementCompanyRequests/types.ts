export type ManagementCompanyRequestType =
  | "Fine"
  | "Payment"
  | "GeneralQuestion";
export type ManagementCompanyRequestStatus =
  | "Submitted"
  | "Acknowledged"
  | "InProgress"
  | "WaitingManager"
  | "Completed"
  | "Cancelled";
export interface RequestItem {
  id: string;
  friendlyIdentifier: string;
  condominiumId: string;
  condominiumName: string;
  managementCompanyName: string;
  categoryId?: string;
  categoryName?: string;
  type: ManagementCompanyRequestType;
  status: ManagementCompanyRequestStatus;
  subject: string;
  unit?: string | null;
  block?: string | null;
  value?: number | null;
  beneficiaryName?: string | null;
  dueDate?: string | null;
  thirdPartyIdentification?: string | null;
  thirdPartyForm?: "Pix" | "Boleto" | "DepositAccount" | null;
  createdAt: string;
  updatedAt: string;
}
export interface PageResult {
  items: RequestItem[];
  page: number;
  pageSize: number;
  total: number;
  hasMore: boolean;
}
export interface CategoryOption {
  id: string;
  name: string;
  type: ManagementCompanyRequestType;
}
export interface UnitOption {
  id: string;
  identifier: string;
  blockId: string | null;
  block: string | null;
}
export interface BeneficiaryOption {
  id: string;
  fullName: string;
  role: "Manager" | "SubManager";
  pixKeyType: string | null;
  pixKey: string | null;
}
export interface CreationOptions {
  condominiumId: string;
  managementCompany: { id: string; name: string } | null;
  categories: CategoryOption[];
  units: UnitOption[];
  beneficiaries: BeneficiaryOption[];
}
export interface Attachment {
  id: string;
  messageId: string | null;
  purpose: "Request" | "Message" | "PaymentBoleto" | "PaymentReceipt";
  originalFileName: string;
  contentType: string;
  fileSize: number;
  createdAt: string;
}
export interface RequestDetail extends RequestItem {
  categoryId: string;
  cancellationReason: string | null;
  cancellationOrigin?: "ManagementCompany" | "Manager" | "SubManager" | null;
  requester: {
    id: string;
    fullName: string;
    role: "Manager" | "SubManager" | null;
  };
  fine?: {
    unitId: string;
    unit?: string | null;
    block?: string | null;
    nature: string;
    description: string;
    occurrenceDate: string;
    value: number | null;
    valueNotDefined: boolean;
  };
  payment?: {
    nature: string;
    value: number;
    eventDate: string;
    dueDate: string | null;
    isReimbursement: boolean;
    notes: string | null;
    beneficiaryUserId: string | null;
    beneficiaryName: string | null;
    pixKeyType: string | null;
    pixKey: string | null;
    thirdPartyIdentification: string | null;
    thirdPartyForm: "Pix" | "Boleto" | "DepositAccount" | null;
    thirdPartyPixKey: string | null;
    thirdPartyBank: string | null;
    thirdPartyAgency: string | null;
    thirdPartyAccount: string | null;
  };
  question?: { theme: string };
  messages: {
    id: string;
    authorUserId: string;
    authorName: string;
    authorRole: string;
    content: string;
    createdAt: string;
  }[];
  history: {
    id: string;
    eventType: string;
    previousStatus: ManagementCompanyRequestStatus | null;
    newStatus: ManagementCompanyRequestStatus;
    changedByUserId: string;
    changedByName?: string | null;
    reason: string | null;
    createdAt: string;
  }[];
  attachments: Attachment[];
  condominium?: {
    name: string;
    address: string | null;
    city: string | null;
    state: string | null;
    managers: {
      id: string;
      fullName: string;
      role: "Manager" | "SubManager";
    }[];
  };
}

export interface AdministratorContext {
  managementCompanyId: string;
  managementCompanyName: string;
  jobTitle: string;
  accessType: "Person" | "Department";
  categories: CategoryOption[];
}
export interface AdministratorOptions {
  condominiums: { condominiumId: string; name: string }[];
  categories: { id: string; name: string }[];
}
