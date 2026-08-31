import { TextField, type TextFieldProps } from "@mui/material";

export function parseBrlCurrency(digits: string) {
  const normalized = digits.replace(/\D/g, "");
  return normalized ? Number(normalized) / 100 : null;
}

export function formatBrlCurrency(value: number | null) {
  return value === null ? "" : new Intl.NumberFormat("pt-BR", {
    style: "currency", currency: "BRL", minimumFractionDigits: 2,
  }).format(Math.max(0, value));
}

export const applyCurrencyShortcut = (value: number | null, amount: number) =>
  Math.max(0, (value ?? 0) + amount);

export function CurrencyField({ value, onValueChange, ...props }: Omit<TextFieldProps, "value" | "onChange"> & {
  value: number | null;
  onValueChange: (value: number | null) => void;
}) {
  return <TextField {...props} value={formatBrlCurrency(value)} inputMode="numeric"
    onChange={event => onValueChange(parseBrlCurrency(event.target.value))} />;
}
