# Desenvolvimento local do Comvy

Ambiente local isolado para homologação antes de push/deploy. A stack usa os Dockerfiles versionados, mas possui compose, volumes, banco, chaves e secrets próprios.

## Pré-requisitos

- Docker Desktop com Docker Compose;
- Git.

.NET e Node são opcionais quando a execução ocorrer dentro dos containers.

## Primeira execução

```powershell
Copy-Item .env.local.example .env.local
docker compose -f docker-compose.local.yml up -d --build
```

O `api` aplica o bundle de migrations no entrypoint e, em `Development`, confirma o schema com `MigrateAsync`. O seed local roda somente quando `DevelopmentSeed__Enabled=true` e é idempotente.

## URLs e credenciais

- Frontend: http://localhost:3000
- API/Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health
- PostgreSQL: `localhost:5433`, database `comvy_local`, user `comvy`

Seed local:

| Perfil | E-mail | Senha |
| --- | --- | --- |
| PlatformAdmin | `admin@local.comvy` | `ComvyLocal123!` |
| Manager | `manager@local.comvy` | `ComvyLocal123!` |
| SubManager | `submanager@local.comvy` | `ComvyLocal123!` |
| Resident | `resident@local.comvy` | `ComvyLocal123!` |
| Funcionário | `employee@local.comvy` | `ComvyLocal123!` |

O condomínio seed é `Residencial Comvy Local`, unidade `101`, com administradora e categoria de atendimento.

## Operação

Manter dados:

```powershell
docker compose -f docker-compose.local.yml down
docker compose -f docker-compose.local.yml up -d
```

Reconstruir imagens:

```powershell
docker compose -f docker-compose.local.yml up -d --build
```

Reset total — remove somente volumes da stack local:

```powershell
docker compose -f docker-compose.local.yml down -v
docker compose -f docker-compose.local.yml up -d --build
```

Logs:

```powershell
docker compose -f docker-compose.local.yml logs -f
docker compose -f docker-compose.local.yml logs -f api
```

## Integrações externas

WhatsApp, envio SMTP/e-mail e recursos OpenAI ficam desabilitados por padrão. O compose local não recebe secrets de produção nem aponta para Meta, SMTP, OpenAI, domínio, banco ou storage da VPS. Workers internos continuam executando; workers de integração respeitam as flags desligadas.

Para experimentar uma integração, altere apenas `.env.local` e faça uma decisão explícita de risco. Nunca salve secret real em arquivo versionado. O modo local não oferece proteção para habilitar credenciais reais acidentalmente.

## Persistência e isolamento

Os volumes Docker exclusivos são `comvy_local_db`, `comvy_local_attachments` e `comvy_local_dataprotection`. Attachments ficam em `/app/data/attachments`; DataProtection em `/app/data-protection-keys`. Não montar volumes de produção.

Frontend é compilado com `VITE_API_URL=http://localhost:8080`; chamadas do navegador usam a API local e CORS aceita somente `http://localhost:3000`.

## Smoke test manual

Após subir: acessar `/health`, entrar como PlatformAdmin, abrir Overwatch, testar logins dos cinco usuários, criar uma solicitação como Resident, consultar/operar atendimento com Manager/SubManager, consultar acesso da administradora, testar upload/download/preview, executar hard delete apenas em registros fake elegíveis e confirmar bloqueio de registros com histórico. Reiniciar a stack sem `-v` e verificar banco/attachments.

Não há envio externo esperado: WhatsApp e e-mail estão desligados; OpenAI não é requisito de inicialização.

Validar a configuração sem iniciar:

```powershell
docker compose -f docker-compose.local.yml config
```
