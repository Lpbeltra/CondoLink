import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type MouseEvent,
} from "react";
import AddRoundedIcon from "@mui/icons-material/AddRounded";
import ContentCopyRoundedIcon from "@mui/icons-material/ContentCopyRounded";
import LockResetRoundedIcon from "@mui/icons-material/LockResetRounded";
import EditRoundedIcon from "@mui/icons-material/EditRounded";
import MoreVertRoundedIcon from "@mui/icons-material/MoreVertRounded";
import SearchRoundedIcon from "@mui/icons-material/SearchRounded";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  InputAdornment,
  LinearProgress,
  Menu,
  MenuItem,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import { EmptyState } from "../components/EmptyState";
import { PageContainer } from "../components/PageContainer";
import { useManagementContext } from "../management/ManagementContext";
import {
  createFirstAccessLink,
  deleteResident,
  exportActiveResidentsPdf,
  inactivateResident,
  listCondominiumMembers,
  listUnits,
  onboardMember,
  reactivateResident,
  resendFirstAccess,
  resetMemberTemporaryPassword,
  updateCondominiumMember,
} from "../management/api";
import { managementError } from "../management/errors";
import type {
  CondominiumMember,
  RelationshipType,
  Unit,
} from "../management/types";
import { formatDateTime } from "../requests/presentation";
import { getPersonBadges } from "../management/peoplePresentation";
import { temporaryCredentialsWhatsAppText } from "../auth/temporaryCredentials";
import { UnitAutocomplete } from "../management/components/UnitAutocomplete";

interface CredentialResult {
  fullName: string;
  email: string;
  temporaryPassword: string;
  reset: boolean;
}

const relationshipLabels: Record<RelationshipType, string> = {
  Owner: "Proprietário",
  Tenant: "Inquilino",
  AuthorizedOccupant: "Ocupante autorizado",
};
const roleLabels: Record<string, string> = {
  Manager: "Síndico / Gestão",
  Resident: "Morador",
};
export function ManagementPeoplePage() {
  const { activeCondominiumId } = useManagementContext();
  const [people, setPeople] = useState<CondominiumMember[]>([]);
  const [units, setUnits] = useState<Unit[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [open, setOpen] = useState(false);
  const [result, setResult] = useState<CredentialResult | null>(null);
  const [resetTarget, setResetTarget] = useState<CondominiumMember | null>(
    null,
  );
  const [resetting, setResetting] = useState(false);
  const [copyFeedback, setCopyFeedback] = useState<{
    message: string;
    error: boolean;
  } | null>(null);
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [unitId, setUnitId] = useState("");
  const [type, setType] = useState<RelationshipType>("Owner");
  const [resident, setResident] = useState(false);
  const [primary, setPrimary] = useState(false);
  const [saving, setSaving] = useState(false);
  const [emailOnlyLogin, setEmailOnlyLogin] = useState(false);
  const [firstAccessChannel, setFirstAccessChannel] = useState<
    "WhatsApp" | "Email" | "WhatsAppAndEmail" | "None"
  >("Email");
  const [editing, setEditing] = useState<CondominiumMember | null>(null);
  const [cpf, setCpf] = useState("");
  const [cnpj, setCnpj] = useState("");
  const [address, setAddress] = useState("");
  const [city, setCity] = useState("");
  const [state, setState] = useState("");
  const [membershipActive, setMembershipActive] = useState(true);
  const [unitMembershipId, setUnitMembershipId] = useState<string | null>(null);
  const [success, setSuccess] = useState("");
  const [search, setSearch] = useState("");
  const [effectiveSearch, setEffectiveSearch] = useState("");
  const [status, setStatus] = useState<"active" | "inactive">("active");
  const [actionAnchor, setActionAnchor] = useState<HTMLElement | null>(null);
  const [actionTarget, setActionTarget] = useState<CondominiumMember | null>(
    null,
  );
  const [lifecycleTarget, setLifecycleTarget] = useState<{
    person: CondominiumMember;
    linkId?: string;
    operation: "inactivate" | "reactivate" | "delete";
  } | null>(null);
  const [lifecycleSaving, setLifecycleSaving] = useState(false);
  const [resendingUserId, setResendingUserId] = useState<string | null>(null);
  const [exportingPdf, setExportingPdf] = useState(false);
  const loadVersion = useRef(0);
  const activeIdRef = useRef(activeCondominiumId);
  activeIdRef.current = activeCondominiumId;

  useEffect(() => {
    if (!search) {
      setEffectiveSearch("");
      return;
    }
    const timer = window.setTimeout(() => setEffectiveSearch(search), 350);
    return () => window.clearTimeout(timer);
  }, [search]);

  const load = useCallback(async () => {
    const version = ++loadVersion.current;
    setOpen(false);
    setUnitId("");
    setError("");
    setSaving(false);
    if (!activeCondominiumId) {
      setPeople([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");

    try {
      const peopleData = await listCondominiumMembers(
        activeCondominiumId,
        effectiveSearch,
        status,
      );

      if (version !== loadVersion.current) return;
      setPeople(peopleData);
    } catch (requestError) {
      if (version === loadVersion.current)
        setError(managementError(requestError));
    } finally {
      if (version === loadVersion.current) setLoading(false);
    }
  }, [activeCondominiumId, effectiveSearch, status]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    let current = true;
    if (!activeCondominiumId) {
      setUnits([]);
      return;
    }
    void listUnits(activeCondominiumId)
      .then((items) => {
        if (current) setUnits(items.filter((unit) => unit.isActive));
      })
      .catch((requestError) => {
        if (current) setError(managementError(requestError));
      });
    return () => {
      current = false;
    };
  }, [activeCondominiumId]);

  useEffect(() => {
    setResult(null);
    setCopyFeedback(null);
  }, [activeCondominiumId]);

  if (!activeCondominiumId && !loading) {
    return (
      <PageContainer>
        <Alert severity="info">
          Selecione um condomínio para gerenciar as pessoas.
        </Alert>
      </PageContainer>
    );
  }

  const closeResult = () => {
    setResult(null);
    setCopyFeedback(null);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();

    if (!activeCondominiumId || saving || !fullName.trim() || !email.trim()) {
      return;
    }

    setSaving(true);
    setError("");

    const operationId = activeCondominiumId;
    try {
      if (editing) {
        const updated = await updateCondominiumMember(
          activeCondominiumId,
          editing.userId,
          {
            fullName: fullName.trim(),
            email: email.trim(),
            phoneNumber: phone.trim() || null,
            cpf: cpf.trim() || null,
            cnpj: cnpj.trim() || null,
            address: address.trim() || null,
            city: city.trim() || null,
            state: state.trim() || null,
            membershipActive,
            unitMembershipId,
            unitId: unitId || null,
            relationshipType: unitId ? type : null,
            isResident: unitId ? resident : false,
            isPrimaryResidence: unitId ? primary : false,
          },
        );
        if (activeIdRef.current !== operationId) return;
        setPeople((current) =>
          current.map((person) =>
            person.userId === updated.userId
              ? {
                  ...person,
                  fullName: updated.fullName,
                  email: updated.email,
                  phoneNumber: updated.phoneNumber,
                  cpf: updated.cpf,
                  cnpj: updated.cnpj,
                  address: updated.address,
                  city: updated.city,
                  state: updated.state,
                  membershipActive: updated.membershipActive,
                  unitLinks: updated.unitLink ? [updated.unitLink] : [],
                }
              : person,
          ),
        );
        setOpen(false);
        setEditing(null);
        setSuccess("Pessoa atualizada com sucesso.");
        return;
      }
      const created = await onboardMember(activeCondominiumId, {
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phone.trim() || null,
        unitId: unitId || null,
        relationshipType: unitId ? type : null,
        isResident: unitId ? resident : false,
        isPrimaryResidence: unitId ? primary : false,
        firstAccessChannel,
        emailDeliveryEnabled: !emailOnlyLogin,
        invitationOperationId: crypto.randomUUID(),
      });

      if (activeIdRef.current !== operationId) return;

      setOpen(false);
      setFullName("");
      setEmail("");
      setPhone("");
      setUnitId("");
      setResident(false);
      setPrimary(false);
      setResult(null);
      setSuccess(
        created.firstAccessStatus === "InviteSent"
          ? "Pessoa cadastrada e convite enviado por e-mail."
          : created.firstAccessStatus === "InviteQueued"
            ? "Pessoa cadastrada e convite enfileirado para o WhatsApp."
            : "Pessoa cadastrada. O primeiro acesso está pendente.",
      );

      await load();
    } catch (requestError) {
      if (activeIdRef.current === operationId)
        setError(managementError(requestError));
    } finally {
      if (activeIdRef.current === operationId) setSaving(false);
    }
  };

  const beginAdd = () => {
    setError("");
    setSuccess("");
    setEditing(null);
    setFullName("");
    setEmail("");
    setPhone("");
    setEmailOnlyLogin(false);
    setFirstAccessChannel("Email");
    setCpf("");
    setCnpj("");
    setAddress("");
    setCity("");
    setState("");
    setMembershipActive(true);
    setUnitMembershipId(null);
    setUnitId("");
    setType("Owner");
    setResident(false);
    setPrimary(false);
    setOpen(true);
  };
  const beginEdit = (person: CondominiumMember) => {
    const link = person.unitLinks[0] ?? null;
    setError("");
    setSuccess("");
    setEditing(person);
    setFullName(person.fullName);
    setEmail(person.email);
    setPhone(person.phoneNumber ?? "");
    setCpf(person.cpf ?? "");
    setCnpj(person.cnpj ?? "");
    setAddress(person.address ?? "");
    setCity(person.city ?? "");
    setState(person.state ?? "");
    setMembershipActive(person.membershipActive);
    setUnitMembershipId(link?.unitMembershipId ?? null);
    setUnitId(link?.unitId ?? "");
    setType(link?.relationshipType ?? "Owner");
    setResident(link?.isResident ?? false);
    setPrimary(link?.isPrimaryResidence ?? false);
    setOpen(true);
  };

  const copy = async (value: string, message: string) => {
    if (!result) return;
    try {
      await navigator.clipboard.writeText(value);
      setCopyFeedback({ message, error: false });
    } catch {
      setCopyFeedback({
        message: "Não foi possível copiar. Selecione o conteúdo manualmente.",
        error: true,
      });
    }
  };

  const resetPassword = async () => {
    if (!activeCondominiumId || !resetTarget || resetting) return;
    setResetting(true);
    setError("");
    try {
      const reset = await resetMemberTemporaryPassword(
        activeCondominiumId,
        resetTarget.userId,
      );
      setResetTarget(null);
      setResult({
        fullName: reset.fullName,
        email: reset.email,
        temporaryPassword: reset.temporaryPassword,
        reset: true,
      });
      setPeople((current) =>
        current.map((person) =>
          person.userId === reset.userId
            ? { ...person, mustChangePassword: true }
            : person,
        ),
      );
    } catch (requestError) {
      setError(managementError(requestError));
    } finally {
      setResetting(false);
    }
  };
  const resendAccess = async (person: CondominiumMember) => {
    if (!activeCondominiumId || resendingUserId) return;
    setResendingUserId(person.userId);
    try {
      const result = await resendFirstAccess(activeCondominiumId, person.userId);
      if (result.channel === "WhatsAppAndEmail")
        setSuccess(result.emailSent && result.whatsappQueued
          ? "Primeiro acesso enviado por e-mail e enfileirado no WhatsApp."
          : result.emailSent
            ? "O e-mail foi enviado, mas não foi possível enfileirar o WhatsApp."
            : result.whatsappQueued
              ? "O primeiro acesso foi enfileirado no WhatsApp, mas o envio por e-mail falhou."
              : "Não foi possível enviar o primeiro acesso pelos canais disponíveis.");
      else if (result.channel === "WhatsApp")
        setSuccess("Primeiro acesso enfileirado no WhatsApp.");
      else setSuccess("Primeiro acesso enviado por e-mail.");
      await load();
    } catch (requestError) {
      setError(managementError(requestError));
      await load();
    } finally {
      setResendingUserId(null);
    }
  };
  const exportPdf = async () => {
    if (!activeCondominiumId || exportingPdf) return;
    setExportingPdf(true);
    setError("");
    try {
      await exportActiveResidentsPdf(activeCondominiumId);
    } catch {
      setError("Não foi possível gerar a relação de moradores. Tente novamente.");
    } finally {
      setExportingPdf(false);
    }
  };
  const copyAccessLink = async (person: CondominiumMember) => {
    if (!activeCondominiumId) return;
    try {
      const { link } = await createFirstAccessLink(
        activeCondominiumId,
        person.userId,
      );
      await navigator.clipboard.writeText(link);
      setSuccess("Link copiado. Ele expira em 24 horas.");
    } catch (requestError) {
      setError(managementError(requestError));
    }
  };
  const openActions = (
    event: MouseEvent<HTMLElement>,
    person: CondominiumMember,
  ) => {
    setActionAnchor(event.currentTarget);
    setActionTarget(person);
  };
  const closeActions = () => {
    setActionAnchor(null);
    setActionTarget(null);
  };
  const confirmLifecycle = async () => {
    if (!activeCondominiumId || !lifecycleTarget || lifecycleSaving) return;
    setLifecycleSaving(true);
    setError("");
    try {
      const { person, linkId, operation } = lifecycleTarget;
      if (operation === "delete")
        await deleteResident(activeCondominiumId, person.userId);
      else if (operation === "inactivate" && linkId)
        await inactivateResident(activeCondominiumId, person.userId, linkId);
      else if (operation === "reactivate" && linkId)
        await reactivateResident(activeCondominiumId, person.userId, linkId);
      setSuccess(
        operation === "delete"
          ? "Morador excluído definitivamente."
          : operation === "inactivate"
            ? "Vínculo residencial inativado."
            : "Vínculo residencial reativado.",
      );
      setLifecycleTarget(null);
      await load();
    } catch (requestError) {
      setError(managementError(requestError));
      setLifecycleTarget(null);
      await load();
    } finally {
      setLifecycleSaving(false);
    }
  };
  return (
    <PageContainer>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        justifyContent="space-between"
        gap={2}
      >
        <Box>
          <Typography variant="h1">Pessoas</Typography>
          <Typography color="text.secondary">
            Gerencie quem possui acesso ao condomínio.
          </Typography>
        </Box>
        <Stack direction={{ xs: "column", sm: "row" }} gap={1}>
          <Button variant="outlined" disabled={exportingPdf}
            onClick={() => void exportPdf()}>
            {exportingPdf ? "Gerando PDF..." : "Exportar moradores em PDF"}
          </Button>
          <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={beginAdd}>
            Adicionar pessoa
          </Button>
        </Stack>
      </Stack>
      {success && (
        <Alert severity="success" sx={{ mt: 2 }}>
          {success}
        </Alert>
      )}
      {error && !open && (
        <Alert
          severity="error"
          sx={{ mt: 2 }}
          action={<Button onClick={() => void load()}>Tentar novamente</Button>}
        >
          {error}
        </Alert>
      )}
      <Stack mt={3} gap={2}>
        <TextField
          label="Buscar morador"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Nome, e-mail, telefone, unidade ou bloco"
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchRoundedIcon />
                </InputAdornment>
              ),
            },
          }}
        />
        <Tabs
          value={status}
          onChange={(_, value: "active" | "inactive") => setStatus(value)}
          aria-label="Status dos moradores"
        >
          <Tab value="active" label="Ativos" />
          <Tab value="inactive" label="Inativos" />
        </Tabs>
      </Stack>
      {loading && people.length > 0 && (
        <LinearProgress aria-label="Atualizando pessoas" sx={{ mt: 2 }} />
      )}
      {loading && people.length === 0 ? (
        <Skeleton variant="rounded" height={220} sx={{ mt: 3 }} />
      ) : people.length === 0 ? (
        <EmptyState
          title="Nenhuma pessoa cadastrada"
          description="Adicione moradores e responsáveis para que possam acessar o Comvy."
          actionLabel="Adicionar pessoa"
          onAction={beginAdd}
        />
      ) : (
        <Box
          display="grid"
          gridTemplateColumns={{ xs: "1fr", lg: "repeat(2,minmax(0,1fr))" }}
          gap={2}
          mt={3}
        >
          {people.map((person) => (
            <Card key={person.membershipId} elevation={0}>
              <CardContent>
                <Stack direction="row" justifyContent="space-between" gap={1}>
                  <Typography variant="h3">{person.fullName}</Typography>
                  <IconButton
                    aria-label={`Ações de ${person.fullName}`}
                    onClick={(event) => openActions(event, person)}
                  >
                    <MoreVertRoundedIcon />
                  </IconButton>
                </Stack>
                <Typography color="text.secondary">
                  {person.email}
                  {person.phoneNumber ? ` · ${person.phoneNumber}` : ""}
                </Typography>
                <Stack direction="row" gap={0.5} flexWrap="wrap" mt={1}>
                  <Chip
                    size="small"
                    label={
                      (
                        {
                          Pending: "Acesso pendente",
                          InviteSent: "Convite enviado",
                          Completed: "Acesso concluído",
                          DeliveryFailed: "Falha no envio",
                        } as const
                      )[person.firstAccessStatus]
                    }
                  />
                  {getPersonBadges(person).map((badge) => (
                    <Chip
                      key={badge.label}
                      size="small"
                      label={badge.label}
                      color={badge.color}
                    />
                  ))}
                  {person.roles.map((role) => (
                    <Chip
                      key={role}
                      size="small"
                      label={roleLabels[role] ?? role}
                    />
                  ))}
                </Stack>
                {person.unitLinks.map((link) => (
                  <Typography
                    key={link.unitMembershipId}
                    color="text.secondary"
                    fontSize=".8rem"
                    mt={1}
                  >
                    {link.block ? `Bloco ${link.block} · ` : ""}
                    {link.unitIdentifier} ·{" "}
                    {relationshipLabels[link.relationshipType]}
                  </Typography>
                ))}
                <Typography color="text.secondary" fontSize=".78rem" mt={1}>
                  Entrada: {formatDateTime(person.joinedAt)}
                </Typography>
                <Stack direction={{ xs: "column", sm: "row" }} gap={1} mt={2}>
                  <Button
                    size="small"
                    variant="outlined"
                    startIcon={<LockResetRoundedIcon />}
                    disabled={!person.userActive}
                    onClick={() => setResetTarget(person)}
                  >
                    Redefinir senha temporária
                  </Button>
                  {person.mustChangePassword && (
                    <Button
                      size="small"
                      variant="outlined"
                      disabled={resendingUserId !== null}
                      onClick={() => void resendAccess(person)}
                    >
                      {resendingUserId === person.userId
                        ? "Reenviando..." : "Reenviar primeiro acesso"}
                    </Button>
                  )}
                  {person.mustChangePassword && (
                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={<ContentCopyRoundedIcon />}
                      onClick={() => void copyAccessLink(person)}
                    >
                      Copiar link de primeiro acesso
                    </Button>
                  )}
                </Stack>
              </CardContent>
            </Card>
          ))}
        </Box>
      )}
      <Menu
        anchorEl={actionAnchor}
        open={Boolean(actionAnchor)}
        onClose={closeActions}
      >
        {actionTarget && (
          <MenuItem
            onClick={() => {
              const person = actionTarget;
              closeActions();
              beginEdit(person);
            }}
          >
            <EditRoundedIcon fontSize="small" sx={{ mr: 1 }} />
            Editar
          </MenuItem>
        )}
        {actionTarget?.unitLinks
          .filter((link) => link.isActive !== false)
          .map((link) => (
            <MenuItem
              key={`end-${link.unitMembershipId}`}
              onClick={() => {
                const person = actionTarget;
                closeActions();
                setLifecycleTarget({
                  person,
                  linkId: link.unitMembershipId,
                  operation: "inactivate",
                });
              }}
            >
              Inativar {link.block ? `Bloco ${link.block} · ` : ""}
              {link.unitIdentifier}
            </MenuItem>
          ))}
        {actionTarget?.unitLinks
          .filter((link) => !link.isActive)
          .map((link) => (
            <MenuItem
              key={`start-${link.unitMembershipId}`}
              onClick={() => {
                const person = actionTarget;
                closeActions();
                setLifecycleTarget({
                  person,
                  linkId: link.unitMembershipId,
                  operation: "reactivate",
                });
              }}
            >
              Reativar {link.block ? `Bloco ${link.block} · ` : ""}
              {link.unitIdentifier}
            </MenuItem>
          ))}
        {actionTarget?.canDelete && (
          <MenuItem
            sx={{ color: "error.main" }}
            onClick={() => {
              const person = actionTarget;
              closeActions();
              setLifecycleTarget({ person, operation: "delete" });
            }}
          >
            Excluir definitivamente
          </MenuItem>
        )}
      </Menu>
      <Dialog
        open={Boolean(lifecycleTarget)}
        onClose={() => !lifecycleSaving && setLifecycleTarget(null)}
      >
        <DialogTitle>
          {lifecycleTarget?.operation === "delete"
            ? "Excluir morador definitivamente?"
            : lifecycleTarget?.operation === "inactivate"
              ? "Inativar morador?"
              : "Reativar morador?"}
        </DialogTitle>
        <DialogContent>
          <Typography>
            {lifecycleTarget?.operation === "delete"
              ? "Este cadastro ainda não possui histórico e será removido permanentemente. Esta ação não pode ser desfeita."
              : lifecycleTarget?.operation === "inactivate"
                ? `${lifecycleTarget.person.fullName} deixará de aparecer entre os moradores ativos desta unidade, mas seu histórico será preservado.`
                : `O vínculo residencial de ${lifecycleTarget?.person.fullName ?? "morador"} será reativado na mesma unidade.`}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setLifecycleTarget(null)}
            disabled={lifecycleSaving}
          >
            Cancelar
          </Button>
          <Button
            color={
              lifecycleTarget?.operation === "delete" ? "error" : "primary"
            }
            variant="contained"
            onClick={() => void confirmLifecycle()}
            disabled={lifecycleSaving}
          >
            {lifecycleSaving
              ? "Salvando..."
              : lifecycleTarget?.operation === "delete"
                ? "Excluir definitivamente"
                : lifecycleTarget?.operation === "inactivate"
                  ? "Inativar"
                  : "Reativar"}
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog
        open={open}
        onClose={() => !saving && setOpen(false)}
        fullWidth
        maxWidth="sm"
      >
        <Box component="form" onSubmit={(e) => void submit(e)}>
          <DialogTitle>
            {editing ? "Editar pessoa" : "Adicionar pessoa"}
          </DialogTitle>
          <DialogContent>
            <Stack gap={2} pt={1}>
              {error && <Alert severity="error">{error}</Alert>}
              {editing &&
                email.trim().toLowerCase() !== editing.email.toLowerCase() && (
                  <Alert severity="warning">
                    Alterar o e-mail também altera a credencial usada para
                    entrar no Comvy.
                  </Alert>
                )}
              <TextField
                required
                label="Nome completo"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                slotProps={{ htmlInput: { maxLength: 200 } }}
              />
              <TextField
                required
                type="email"
                label="E-mail"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                slotProps={{ htmlInput: { maxLength: 254 } }}
              />
              {!editing && (
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={emailOnlyLogin}
                      onChange={(e) => {
                        setEmailOnlyLogin(e.target.checked);
                        if (e.target.checked && firstAccessChannel !== "WhatsApp"
                            && firstAccessChannel !== "None")
                          setFirstAccessChannel("None");
                      }}
                    />
                  }
                  label="Este e-mail é apenas para acesso ao sistema"
                />
              )}
              <TextField
                label="Telefone / WhatsApp"
                value={phone}
                onChange={(e) => {
                  setPhone(e.target.value);
                  if (!e.target.value.trim()
                      && (firstAccessChannel === "WhatsApp"
                        || firstAccessChannel === "WhatsAppAndEmail"))
                    setFirstAccessChannel("None");
                }}
                slotProps={{ htmlInput: { maxLength: 30 } }}
              />
              {!editing && (
                <TextField
                  select
                  label="Enviar primeiro acesso"
                  value={firstAccessChannel}
                  onChange={(event) =>
                    setFirstAccessChannel(
                      event.target.value as
                        | "WhatsApp"
                        | "Email"
                        | "WhatsAppAndEmail"
                        | "None",
                    )
                  }
                >
                  <MenuItem
                    value="WhatsApp"
                    disabled={!phone.trim()}
                  >
                    WhatsApp
                  </MenuItem>
                  <MenuItem value="Email" disabled={emailOnlyLogin}>
                    E-mail
                  </MenuItem>
                  <MenuItem
                    value="WhatsAppAndEmail"
                    disabled={!phone.trim() || emailOnlyLogin}
                  >
                    WhatsApp + E-mail
                  </MenuItem>
                  <MenuItem value="None">Não enviar agora</MenuItem>
                </TextField>
              )}
              {editing && (
                <>
                  <TextField
                    label="CPF"
                    value={cpf}
                    onChange={(e) => setCpf(e.target.value)}
                  />
                  <TextField
                    label="CNPJ"
                    value={cnpj}
                    onChange={(e) => setCnpj(e.target.value)}
                  />
                  <TextField
                    label="Endereço"
                    value={address}
                    onChange={(e) => setAddress(e.target.value)}
                    slotProps={{ htmlInput: { maxLength: 300 } }}
                  />
                  <Stack direction={{ xs: "column", sm: "row" }} gap={2}>
                    <TextField
                      fullWidth
                      label="Cidade"
                      value={city}
                      onChange={(e) => setCity(e.target.value)}
                      slotProps={{ htmlInput: { maxLength: 100 } }}
                    />
                    <TextField
                      label="UF"
                      value={state}
                      onChange={(e) => setState(e.target.value.toUpperCase())}
                      slotProps={{ htmlInput: { maxLength: 2 } }}
                      sx={{ width: { xs: "100%", sm: 120 } }}
                    />
                  </Stack>
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={membershipActive}
                        onChange={(e) => setMembershipActive(e.target.checked)}
                      />
                    }
                    label="Pessoa ativa neste condomínio"
                  />
                </>
              )}
              <UnitAutocomplete
                units={units}
                value={unitId}
                onChange={(nextUnitId) => {
                  setUnitId(nextUnitId);
                  if (!nextUnitId) {
                    setResident(false);
                    setPrimary(false);
                  }
                }}
              />
              {unitId && (
                <>
                  <TextField
                    select
                    label="Tipo de vínculo"
                    value={type}
                    onChange={(e) =>
                      setType(e.target.value as RelationshipType)
                    }
                  >
                    {Object.entries(relationshipLabels).map(([v, l]) => (
                      <MenuItem key={v} value={v}>
                        {l}
                      </MenuItem>
                    ))}
                  </TextField>
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={resident}
                        onChange={(e) => {
                          setResident(e.target.checked);
                          if (!e.target.checked) setPrimary(false);
                        }}
                      />
                    }
                    label="Reside na unidade"
                  />
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={primary}
                        onChange={(e) => {
                          setPrimary(e.target.checked);
                          if (e.target.checked) setResident(true);
                        }}
                      />
                    }
                    label="Residência principal"
                  />
                </>
              )}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpen(false)} disabled={saving}>
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="contained"
              disabled={saving || !fullName.trim() || !email.trim()}
            >
              {saving
                ? "Salvando..."
                : editing
                  ? "Salvar alterações"
                  : "Criar conta"}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>
      <Dialog
        open={Boolean(resetTarget)}
        onClose={() => !resetting && setResetTarget(null)}
      >
        <DialogTitle>Redefinir senha temporária?</DialogTitle>
        <DialogContent>
          <Typography>
            Uma nova senha será gerada para {resetTarget?.fullName}. A senha
            anterior deixará de funcionar imediatamente.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetTarget(null)} disabled={resetting}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            onClick={() => void resetPassword()}
            disabled={resetting}
          >
            {resetting ? "Gerando..." : "Gerar nova senha"}
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog
        open={Boolean(result)}
        onClose={closeResult}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>
          {result?.reset
            ? "Senha temporária regenerada"
            : "Conta criada com sucesso"}
        </DialogTitle>
        <DialogContent>
          {result?.reset && (
            <Alert severity="success" sx={{ mb: 2 }}>
              Senha temporária regenerada.
            </Alert>
          )}
          <Typography>
            Compartilhe estas credenciais de forma segura. A senha é exibida
            somente agora.
          </Typography>
          {result && (
            <Card variant="outlined" sx={{ mt: 2 }}>
              <CardContent>
                <Typography fontWeight={800}>{result.fullName}</Typography>
                <Typography>E-mail: {result.email}</Typography>
                <Typography sx={{ fontFamily: "monospace", mt: 1 }}>
                  Senha temporária: {result.temporaryPassword}
                </Typography>
              </CardContent>
            </Card>
          )}
          {copyFeedback && (
            <Alert
              severity={copyFeedback.error ? "error" : "success"}
              sx={{ mt: 2 }}
            >
              {copyFeedback.message}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          {result && (
            <>
              <Button
                startIcon={<ContentCopyRoundedIcon />}
                onClick={() =>
                  void copy(result.temporaryPassword, "Senha copiada.")
                }
              >
                Copiar senha
              </Button>
              <Button
                startIcon={<ContentCopyRoundedIcon />}
                onClick={() =>
                  void copy(
                    temporaryCredentialsWhatsAppText(result),
                    "Mensagem copiada.",
                  )
                }
              >
                Copiar mensagem para WhatsApp
              </Button>
            </>
          )}
          <Button variant="contained" onClick={closeResult}>
            Concluir
          </Button>
        </DialogActions>
      </Dialog>
    </PageContainer>
  );
}
