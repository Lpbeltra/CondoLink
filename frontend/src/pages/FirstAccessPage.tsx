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
import { Brand } from "../components/Brand";
import { api } from "../services/api";

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
    if (password !== confirmPassword) {
      setError("As senhas não coincidem.");
      return;
    }
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
    <Box minHeight="100vh" display="grid" sx={{ placeItems: "center", p: 2 }}>
      <Stack component="main" spacing={3} width="100%" maxWidth={440}>
        <Brand />
        {isInitializing && (
          <Box textAlign="center">
            <CircularProgress aria-label="Carregando sessão" />
          </Box>
        )}
        {!isInitializing && user && (
          <Stack spacing={2}>
            <Typography variant="h1">Você já está conectado</Typography>
            <Typography>
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
          <Box textAlign="center">
            <CircularProgress aria-label="Validando link" />
          </Box>
        )}
        {!isInitializing && !user && state === "invalid" && (
          <>
            <Alert severity="error">
              O link é inválido, expirou ou já foi utilizado.
            </Alert>
            <Button component={Link} to="/login">
              Ir para o login
            </Button>
          </>
        )}
        {!isInitializing && !user && state === "success" && (
          <>
            <Alert severity="success">Senha criada com sucesso.</Alert>
            <Button variant="contained" component={Link} to="/login">
              Entrar no Comvy
            </Button>
          </>
        )}
        {!isInitializing && !user && state === "valid" && (
          <Box component="form" onSubmit={submit}>
            <Stack spacing={2}>
              <Typography variant="h1">Crie sua senha</Typography>
              {error && <Alert severity="error">{error}</Alert>}
              <TextField
                label="Nova senha"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
                inputProps={{ minLength: 8 }}
              />
              <TextField
                label="Confirmar senha"
                type="password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                required
              />
              <Button type="submit" variant="contained" disabled={saving}>
                {saving ? "Criando..." : "Criar senha"}
              </Button>
            </Stack>
          </Box>
        )}
      </Stack>
    </Box>
  );
}
