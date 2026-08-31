import type {
  ManagementCompanyRequestStatus as Status,
  ManagementCompanyRequestType as Type,
} from "./types";
export const typeLabel: Record<Type, string> = {
  Fine: "Multa",
  Payment: "Solicitação de pagamento",
  GeneralQuestion: "Dúvida geral",
};
export function statusLabel(status: Status, type: Type) {
  if (status === "Completed")
    return type === "Fine"
      ? "Processada"
      : type === "Payment"
        ? "Pagamento efetuado"
        : "Respondida";
  return {
    Submitted: "Enviada à administradora",
    Acknowledged: "Administradora ciente",
    InProgress: "Em processamento",
    WaitingManager: "Em processamento",
    Cancelled: "Cancelada",
  }[status];
}
export const money = (value: number) =>
  new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(
    value,
  );
export const date = (value: string) =>
  new Intl.DateTimeFormat("pt-BR", { dateStyle: "medium" }).format(
    new Date(`${value.slice(0, 10)}T12:00:00`),
  );
