import { useEffect, useState, type FormEvent } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {
  Link,
  useLocation,
  useNavigate,
  useSearchParams,
} from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { api } from "../services/api";
import { AuthShell } from "../layout/AuthShell";
import { PasswordVisibilityAdornment } from "../components/PasswordVisibilityAdornment";

export function FirstAccessPage() {
  const { user, isInitializing, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const userId = params.get("userId") ?? "";
  const token = params.get("token") ?? "";
  const [state, setState] = useState<
    "loading" | "valid" | "invalid" | "success"
  >("loading");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);

  const validationError = password.length > 0 && password.length < 8
    ? "A nova senha deve possuir ao menos 8 caracteres."
    : confirmPassword.length > 0 && password !== confirmPassword
      ? "As senhas não coincidem."
      : "";

  useEffect(() => {
    if (isInitializing || user) return;
    if (!userId || !token) {
      setState("invalid");
      return;
    }
    setState("loading");
    void api
      .post("/auth/first-access/validate", { userId, token })
      .then(() => setState("valid"))
      .catch(() => setState("invalid"));
  }, [isInitializing, token, user, userId]);

  function signOutAndContinue() {
    const invitationUrl = `${location.pathname}${location.search}${location.hash}`;
    logout();
    navigate(invitationUrl, { replace: true });
  }

  function cancel() {
    if (window.history.length > 1) navigate(-1);
    else navigate("/", { replace: true });
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (validationError) return;
    setSaving(true);
    try {
      await api.post("/auth/first-access/complete", {
        userId,
        token,
        password,
        confirmPassword,
      });
      setState("success");
    } catch {
      setError("O link é inválido, expirou ou já foi utilizado.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <AuthShell>
      <Stack spacing={3}>
        {isInitializing && (
          <Box textAlign="center">
            <CircularProgress aria-label="Carregando sessão" />
          </Box>
        )}
        {!isInitializing && user && (
          <Stack spacing={2}>
            <Typography variant="h1">Você já está conectado</Typography>
            <Typography color="text.secondary">
              Para concluir este primeiro acesso, é necessário sair da conta
              atual.
            </Typography>
            {user.email && (
              <Alert severity="info">
                Você está conectado como {user.email}.
              </Alert>
            )}
            <Button variant="contained" onClick={signOutAndContinue}>
              Sair e continuar
            </Button>
            <Button color="inherit" onClick={cancel}>
              Cancelar
            </Button>
          </Stack>
        )}
        {!isInitializing && !user && state === "loading" && (
          <Stack spacing={2} aria-live="polite">
            <CircularProgress aria-label="Validando link" />
            <Typography color="text.secondary">Validando seu convite…</Typography>
          </Stack>
        )}
        {!isInitializing && !user && state === "invalid" && (
          <Stack spacing={2}>
            <Typography variant="h1">Link indisponível</Typography>
            <Alert severity="error">
              O link é inválido, expirou ou já foi utilizado.
            </Alert>
            <Button component={Link} to="/login">
              Ir para o login
            </Button>
          </Stack>
        )}
        {!isInitializing && !user && state === "success" && (
          <Stack spacing={2}>
            <Typography variant="h1">Senha criada</Typography>
            <Alert severity="success">Senha criada com sucesso.</Alert>
            <Button variant="contained" component={Link} to="/login">
              Entrar no Comvy
            </Button>
          </Stack>
        )}
        {!isInitializing && !user && state === "valid" && (
          <Box component="form" onSubmit={submit}>
            <Stack spacing={2}>
              <Typography variant="h1">Crie sua senha</Typography>
              <Typography color="text.secondary">
                Ela permitirá seu acesso ao Comvy.
              </Typography>
              {error && <Alert severity="error">{error}</Alert>}
              <TextField
                label="Nova senha"
                type={showPassword ? "text" : "password"}
                autoComplete="new-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
                inputProps={{ minLength: 8 }}
                disabled={saving}
                helperText="Ao menos 8 caracteres, com maiúscula, minúscula e número."
                slotProps={{ input: { endAdornment: (
                  <PasswordVisibilityAdornment
                    visible={showPassword}
                    onToggle={() => setShowPassword((value) => !value)}
                  />
                ) } }}
              />
              <TextField
                label="Confirmar senha"
                type={showConfirmation ? "text" : "password"}
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                required
                disabled={saving}
                error={Boolean(validationError)}
                helperText={validationError}
                slotProps={{ input: { endAdornment: (
                  <PasswordVisibilityAdornment
                    visible={showConfirmation}
                    onToggle={() => setShowConfirmation((value) => !value)}
                  />
                ) } }}
              />
              <Button
                type="submit"
                variant="contained"
                disabled={saving || Boolean(validationError)}
                startIcon={saving ? <CircularProgress size={18} color="inherit" /> : undefined}
              >
                {saving ? "Criando..." : "Criar senha"}
              </Button>
            </Stack>
          </Box>
        )}
      </Stack>
    </AuthShell>
  );
}
