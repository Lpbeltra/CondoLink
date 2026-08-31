import type {
  ManagementCompanyRequestStatus as Status,
  ManagementCompanyRequestType as Type,
} from "../managementCompanyRequests/types";
export const administratorStatusLabel: Record<Status, string> = {
  Submitted: "Nova",
  Acknowledged: "Ciente",
  InProgress: "Em processamento",
  WaitingManager: "Em processamento",
  Completed: "Concluída",
  Cancelled: "Cancelada",
};
export function administratorRequestStatusLabel(status: Status, type: Type) {
  return status === "Completed"
    ? type === "Fine"
      ? "Processada"
      : type === "Payment"
        ? "Pagamento efetuado"
        : "Respondida"
    : administratorStatusLabel[status];
}
export function administratorActions(status: Status) {
  return {
    canStart: false,
    canInteract: status === "Acknowledged" || status === "InProgress",
    canRequestInformation: false,
    canComplete: status === "InProgress",
    readOnly:
      status === "Completed" ||
      status === "Cancelled",
  };
}
export const completionAction = (type: Type) =>
  type === "Fine"
    ? "Marcar como processada"
    : type === "Payment"
      ? "Confirmar pagamento efetuado"
      : "Marcar como respondida";
