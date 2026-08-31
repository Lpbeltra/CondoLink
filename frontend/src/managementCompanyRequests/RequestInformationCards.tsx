import { Alert, Box, Card, CardContent, Stack, Typography } from "@mui/material";
import { AttachmentsPreview } from "./AttachmentsPreview";
import { date, money } from "./presentation";
import type { RequestDetail } from "./types";

export function RequestInformationCards({ request, showRequester = false }: { request: RequestDetail; showRequester?: boolean }) {
  const paymentBoleto = request.attachments.filter(item => item.purpose === "PaymentBoleto");
  const paymentReceipt = request.attachments.filter(item => item.purpose === "PaymentReceipt");
  return <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: showRequester ? "minmax(0, 1fr) minmax(0, 1.35fr)" : "1fr" }, gap: 2, alignItems: "stretch" }}>
    {showRequester && <Card variant="outlined"><CardContent><Typography variant="h2" mb={1}>Informações do solicitante</Typography><Typography fontWeight={700}>{request.requester.role === "Manager" ? "Síndico" : request.requester.role === "SubManager" ? "Subsíndico" : "Solicitante"}: {request.requester.fullName}</Typography><Typography>{request.condominium?.name ?? request.condominiumName}</Typography>{request.condominium?.address && <Typography color="text.secondary">{request.condominium.address}{request.condominium.city ? ` · ${request.condominium.city}/${request.condominium.state}` : ""}</Typography>}</CardContent></Card>}
    <Card variant="outlined"><CardContent><Typography variant="h2" mb={1}>Dados da solicitação</Typography>
      {request.fine && <Stack><Typography><b>Unidade:</b> {request.fine.block ? `${request.fine.block} / ` : ""}{request.fine.unit ?? request.fine.unitId}</Typography><Typography><b>Natureza:</b> {request.fine.nature}</Typography><Typography><b>Descrição:</b> {request.fine.description}</Typography><Typography><b>Data:</b> {date(request.fine.occurrenceDate)}</Typography><Typography><b>Valor:</b> {request.fine.valueNotDefined ? "Valor ainda não definido" : money(request.fine.value!)}</Typography></Stack>}
      {request.payment && <Stack spacing={1.25}>
        <Typography><b>Natureza:</b> {request.payment.nature}</Typography>
        <Typography><b>Valor:</b> {money(request.payment.value)}</Typography>
        <Typography><b>Data:</b> {date(request.payment.eventDate)}</Typography>
        {request.payment.dueDate && <Typography><b>Vencimento:</b> {date(request.payment.dueDate)}</Typography>}
        {request.payment.notes && <Typography><b>Observações:</b> {request.payment.notes}</Typography>}
        {request.payment.isReimbursement && <Alert severity="info">Reembolso: {request.payment.beneficiaryName} · {request.payment.pixKeyType} · {request.payment.pixKey}</Alert>}
        {!request.payment.isReimbursement && <Stack spacing={1}>
          <Typography><b>Terceiro:</b> {request.payment.thirdPartyIdentification}</Typography>
          <Typography><b>Forma de pagamento:</b> {request.payment.thirdPartyForm === "Pix" ? "PIX" : request.payment.thirdPartyForm === "Boleto" ? "Boleto" : "Conta para depósito"}</Typography>
          {request.payment.thirdPartyForm === "Pix" && <Typography><b>Chave PIX:</b> {request.payment.thirdPartyPixKey}</Typography>}
          {request.payment.thirdPartyForm === "Boleto" && paymentBoleto.length > 0 && <Box><Typography variant="subtitle2" mb={1}>Boleto</Typography><AttachmentsPreview items={paymentBoleto} /></Box>}
          {request.payment.thirdPartyForm === "DepositAccount" && <Stack><Typography><b>Banco:</b> {request.payment.thirdPartyBank}</Typography><Typography><b>Agência:</b> {request.payment.thirdPartyAgency}</Typography><Typography><b>Conta:</b> {request.payment.thirdPartyAccount}</Typography></Stack>}
        </Stack>}
        {paymentReceipt.length > 0 && <Box><Typography variant="subtitle2" mb={1}>Comprovante de pagamento</Typography><AttachmentsPreview items={paymentReceipt} /></Box>}
      </Stack>}
      {request.question && <Typography><b>Tema:</b> {request.question.theme}</Typography>}
    </CardContent></Card>
  </Box>;
}
