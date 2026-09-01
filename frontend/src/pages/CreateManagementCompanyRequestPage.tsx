import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Alert,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Checkbox,
  FormControlLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { PageContainer } from "../components/PageContainer";
import { UnitAutocomplete } from "../management/components/UnitAutocomplete";
import { useManagementContext } from "../management/ManagementContext";
import { createRequest, getOptions } from "../managementCompanyRequests/api";
import { money, typeLabel } from "../managementCompanyRequests/presentation";
import type {
  CreationOptions,
  ManagementCompanyRequestType as Type,
} from "../managementCompanyRequests/types";
import { selectAttachmentFiles } from "../requests/attachments";
import { applyCurrencyShortcut, CurrencyField } from "../components/CurrencyField";
import { LocalAttachmentsPreview } from "../managementCompanyRequests/LocalAttachmentsPreview";
export function CreateManagementCompanyRequestPage() {
  const nav = useNavigate();
  const { activeCondominiumId, condominiums } = useManagementContext();
  const [condo, setCondo] = useState(activeCondominiumId ?? "");
  const [options, setOptions] = useState<CreationOptions>();
  const [type, setType] = useState<Type>();
  const [fields, setFields] = useState<Record<string, string>>({});
  const [files, setFiles] = useState<File[]>([]);
  const [boleto, setBoleto] = useState<File[]>([]);
  const [moneyValue, setMoneyValue] = useState<number | null>(null);
  const [undefinedValue, setUndefinedValue] = useState(false);
  const [reimbursement, setReimbursement] = useState(false);
  const [thirdPartyForm, setThirdPartyForm] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    if (!condo) {
      setOptions(undefined);
      return;
    }
    void getOptions(condo)
      .then(setOptions)
      .catch(() =>
        setError("Não foi possível carregar as opções desta administradora."),
      );
  }, [condo]);
  const set = (k: string, v: string) => setFields((x) => ({ ...x, [k]: v }));
  async function submit() {
    if (!type || !condo) return;
    const required=type==="Fine"?!fields.unitId?"Selecione uma unidade.":!fields.nature?.trim()?"Informe a natureza da infração.":!fields.description?.trim()?"Informe a descrição.":!fields.date?"Informe a data da ocorrência.":!undefinedValue&&moneyValue===null?"Informe o valor ou marque “Valor ainda não definido”.":"":type==="Payment"?!fields.nature?.trim()?"Informe a natureza da despesa.":!fields.date?"Informe a data da despesa.":!fields.dueDate?"Informe a data de vencimento.":moneyValue===null?"Informe um valor válido.":reimbursement&&!fields.beneficiaryId?"Selecione um beneficiário para o reembolso.":!reimbursement&&!fields.thirdPartyIdentification?.trim()?"Informe a identificação do terceiro.":!reimbursement&&!thirdPartyForm?"Informe a forma de pagamento.":!reimbursement&&thirdPartyForm==="Pix"&&!fields.thirdPartyPixKey?.trim()?"Informe a chave PIX.":!reimbursement&&thirdPartyForm==="Boleto"&&boleto.length===0?"Anexe o boleto.":!reimbursement&&thirdPartyForm==="DepositAccount"&&(!fields.thirdPartyBank?.trim()||!fields.thirdPartyAgency?.trim()||!fields.thirdPartyAccount?.trim())?"Informe banco, agência e conta.":"":!fields.theme?.trim()?"Informe o tema.":!fields.message?.trim()?"Informe a mensagem.":fields.message.length>2000?"A mensagem pode ter no máximo 2000 caracteres.":"";
    if(required){setError(required);return}
    setSaving(true);
    setError("");
    try {
      const categoryId = options?.categories.find((x) => x.type === type)?.id;
      let payload: any = { condominiumId: condo, categoryId };
      if (type === "Fine")
        payload = {
          ...payload,
          unitId: fields.unitId,
          nature: fields.nature,
          description: fields.description,
          occurrenceDate: fields.date,
          value: undefinedValue ? null : moneyValue,
          valueNotDefined: undefinedValue,
        };
      if (type === "Payment")
        payload = {
          ...payload,
          nature: fields.nature,
          value: moneyValue,
          eventDate: fields.date,
          dueDate: fields.dueDate,
          isReimbursement: reimbursement,
          beneficiaryUserId: reimbursement ? fields.beneficiaryId : null,
          notes: fields.notes || null,
          thirdPartyIdentification: reimbursement ? null : fields.thirdPartyIdentification || null,
          thirdPartyForm: reimbursement ? null : thirdPartyForm || null,
          thirdPartyPixKey: reimbursement ? null : fields.thirdPartyPixKey || null,
          thirdPartyBank: reimbursement ? null : fields.thirdPartyBank || null,
          thirdPartyAgency: reimbursement ? null : fields.thirdPartyAgency || null,
          thirdPartyAccount: reimbursement ? null : fields.thirdPartyAccount || null,
        };
      if (type === "GeneralQuestion")
        payload = { ...payload, theme: fields.theme, message: fields.message };
      const created = await createRequest(type, payload, type === "Payment" && thirdPartyForm === "Boleto" ? files : files, type === "Payment" && thirdPartyForm === "Boleto" ? boleto : []);
      nav(`/management/administrator/${created.id}`, { replace: true,state:{feedback:"Solicitação enviada à administradora."} });
    } catch {
      setError(
        "Não foi possível enviar. Revise os campos e confirme se a categoria continua disponível.",
      );
    } finally {
      setSaving(false);
    }
  }
  return (
    <PageContainer maxWidth={800}>
      <Stack spacing={2}>
        <Typography variant="h1">Nova solicitação</Typography>
        {error && <Alert severity="error">{error}</Alert>}{" "}
        {!activeCondominiumId && (
          <TextField
            select
            label="Condomínio"
            value={condo}
            onChange={(e) => {
              setCondo(e.target.value);
              setType(undefined);
            }}
          >
            {condominiums.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name}
              </MenuItem>
            ))}
          </TextField>
        )}
        {condo && options && !options.managementCompany && (
          <Alert severity="info">
            Este condomínio não possui uma administradora vinculada no momento.
          </Alert>
        )}
        {options?.managementCompany && options.categories.length === 0 && !type && (
          <Alert severity="info">
            Nenhuma categoria disponível para nova solicitação no momento. Fale com sua
            administradora.
          </Alert>
        )}
        {options?.managementCompany && options.categories.length > 0 && !type && (
          <Stack spacing={1}>
            {options.categories.map((c) => (
              <Card key={c.id} variant="outlined">
                <CardActionArea onClick={() => setType(c.type)}>
                  <CardContent>
                    <Typography fontWeight={800}>
                      {typeLabel[c.type]}
                    </Typography>
                    <Typography color="text.secondary">
                      {c.type === "Fine"
                        ? "Envie uma ocorrência para processamento de multa."
                        : c.type === "Payment"
                          ? "Solicite pagamento ou reembolso de uma despesa."
                          : "Envie uma dúvida para a administradora."}
                    </Typography>
                  </CardContent>
                </CardActionArea>
              </Card>
            ))}
          </Stack>
        )}
        {type && (
          <Stack spacing={2}>
            <Typography variant="h2">{typeLabel[type]}</Typography>
            {type === "Fine" && (
              <>
                <UnitAutocomplete
                  units={(options?.units ?? []).map((u) => ({
                    ...u,
                    condominiumId: condo,
                    floor: null,
                    description: null,
                    isActive: true,
                    createdAt: "",
                    updatedAt: "",
                  }))}
                  value={fields.unitId ?? null}
                  onChange={(v) => set("unitId", v ?? "")}
                  label="Unidade"
                />
                <TextField
                  required
                  label="Natureza da infração"
                  value={fields.nature ?? ""}
                  onChange={(e) => set("nature", e.target.value)}
                />
                <TextField
                  required
                  multiline
                  minRows={4}
                  label="Descrição / observações"
                  value={fields.description ?? ""}
                  onChange={(e) => set("description", e.target.value)}
                />
                <TextField
                  required
                  type="date"
                  label="Data da ocorrência"
                  value={fields.date ?? ""}
                  onChange={(e) => set("date", e.target.value)}
                  InputLabelProps={{ shrink: true }}
                />
                <RadioGroup
                  row
                  value={undefinedValue ? "undefined" : "defined"}
                  onChange={(e) =>
                    setUndefinedValue(e.target.value === "undefined")
                  }
                >
                  <FormControlLabel
                    value="defined"
                    control={<Radio />}
                    label="Informar valor"
                  />
                  <FormControlLabel
                    value="undefined"
                    control={<Radio />}
                    label="Valor ainda não definido"
                  />
                </RadioGroup>
                {!undefinedValue && (
                  <>
                    <CurrencyField required label="Valor da multa" value={moneyValue} onValueChange={setMoneyValue} />
                    <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                      {[100, 50, 25, -25, -50, -100].map(amount => (
                        <Button type="button" size="small" variant="outlined" key={amount}
                          onClick={() => setMoneyValue(applyCurrencyShortcut(moneyValue, amount))}>
                          {amount > 0 ? "+" : ""}{amount}
                        </Button>
                      ))}
                    </Stack>
                  </>
                )}
              </>
            )}
            {type === "Payment" && (
              <>
                <TextField
                  required
                  label="Natureza / descrição"
                  value={fields.nature ?? ""}
                  onChange={(e) => set("nature", e.target.value)}
                />
                <CurrencyField required label="Valor" value={moneyValue} onValueChange={setMoneyValue} />
                <TextField
                  required
                  type="date"
                  label="Data da despesa"
                  value={fields.date ?? ""}
                  onChange={(e) => set("date", e.target.value)}
                  InputLabelProps={{ shrink: true }}
                />
                <TextField required type="date" label="Data de vencimento" value={fields.dueDate ?? ""} onChange={e => set("dueDate", e.target.value)} InputLabelProps={{ shrink: true }} />
                <FormControlLabel control={<Checkbox checked={reimbursement} onChange={(e) => { setReimbursement(e.target.checked); if (e.target.checked) { setThirdPartyForm(""); setFiles([]); setBoleto([]); set("thirdPartyIdentification", ""); set("thirdPartyPixKey", ""); set("thirdPartyBank", ""); set("thirdPartyAgency", ""); set("thirdPartyAccount", ""); } else { set("beneficiaryId", ""); } }} />} label="É reembolso" />
                {reimbursement ? <TextField select required label="Beneficiário" value={fields.beneficiaryId ?? ""} onChange={(e) => set("beneficiaryId", e.target.value)}>{options?.beneficiaries.map((b) => (<MenuItem key={b.id} value={b.id} disabled={!b.pixKey}>{b.fullName} — {b.role === "Manager" ? "Síndico" : "Subsíndico"}{!b.pixKey ? " (sem PIX)" : ""}</MenuItem>))}</TextField> : <>
                  <TextField required label="Identificação do terceiro" value={fields.thirdPartyIdentification ?? ""} onChange={e => set("thirdPartyIdentification", e.target.value)} />
                  <TextField select required label="Forma de pagamento" value={thirdPartyForm} onChange={e => { const next = e.target.value; setThirdPartyForm(next); if (next !== "Boleto") setBoleto([]); if (next !== "Pix") set("thirdPartyPixKey", ""); if (next !== "DepositAccount") { set("thirdPartyBank", ""); set("thirdPartyAgency", ""); set("thirdPartyAccount", ""); } }}>
                    <MenuItem value="">Selecione</MenuItem>
                    <MenuItem value="Pix">PIX</MenuItem>
                    <MenuItem value="Boleto">Boleto</MenuItem>
                    <MenuItem value="DepositAccount">Conta para depósito</MenuItem>
                  </TextField>
                  {thirdPartyForm === "Pix" && <TextField required label="Chave PIX" value={fields.thirdPartyPixKey ?? ""} onChange={e => set("thirdPartyPixKey", e.target.value)} />}
                  {thirdPartyForm === "Boleto" && <><Button component="label" variant="outlined">Boleto (obrigatório)<input hidden type="file" onChange={e => { const selected = Array.from(e.target.files ?? []); setBoleto(selected.slice(0, 1)); e.target.value = ""; }} /></Button><LocalAttachmentsPreview files={boleto} onRemove={() => setBoleto([])} /></>}
                  {thirdPartyForm === "DepositAccount" && <Stack direction={{ xs: "column", sm: "row" }} spacing={2}><TextField required fullWidth label="Banco" value={fields.thirdPartyBank ?? ""} onChange={e => set("thirdPartyBank", e.target.value)} /><TextField required fullWidth label="Agência" value={fields.thirdPartyAgency ?? ""} onChange={e => set("thirdPartyAgency", e.target.value)} /><TextField required fullWidth label="Conta" value={fields.thirdPartyAccount ?? ""} onChange={e => set("thirdPartyAccount", e.target.value)} /></Stack>}
                  <TextField multiline minRows={3} label="Observações" value={fields.notes ?? ""} onChange={(e) => set("notes", e.target.value)} />
                </>}
              </>
            )}
            {type === "GeneralQuestion" && (
              <>
                <TextField
                  required
                  label="Tema"
                  value={fields.theme ?? ""}
                  onChange={(e) => set("theme", e.target.value)}
                />
                <TextField
                  required
                  multiline
                  minRows={5}
                  label="Mensagem"
                  inputProps={{ maxLength: 2000 }}
                  helperText={`${(fields.message ?? "").length}/2000`}
                  value={fields.message ?? ""}
                  onChange={(e) => set("message", e.target.value)}
                />
              </>
            )}
            <Button component="label" variant="outlined">
              Selecionar anexos
              <input
                hidden
                multiple
                type="file"
                onChange={(e) => {const result=selectAttachmentFiles(files,Array.from(e.target.files??[]));setFiles(result.files);setError(result.error??"")}}
              />
            </Button>
            <LocalAttachmentsPreview files={files} onRemove={index => setFiles(current => current.filter((_, i) => i !== index))} />
            <Stack direction="row" justifyContent="space-between">
              <Button onClick={() => (type ? setType(undefined) : nav(-1))}>
                Voltar
              </Button>
              <Button
                variant="contained"
                disabled={saving}
                onClick={() => void submit()}
              >
                {saving ? "Enviando…" : "Enviar solicitação"}
              </Button>
            </Stack>
          </Stack>
        )}
      </Stack>
    </PageContainer>
  );
}
