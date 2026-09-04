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
export const moneyInput = (value: number | null | undefined) =>
  value == null ? "" : new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
export function parseMoneyInput(value: string): number | null {
  const raw = value.replace(/R\$\s*/gi, "").replace(/\s/g, "").trim();
  if (!raw) return null;
  const normalized = raw.includes(",") ? raw.replace(/\./g, "").replace(",", ".") : raw;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}
export const date = (value: string) =>
  new Intl.DateTimeFormat("pt-BR", { dateStyle: "medium" }).format(
    new Date(`${value.slice(0, 10)}T12:00:00`),
  );
