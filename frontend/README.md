# CondoLink Frontend

## Desenvolvimento local

```powershell
npm install
npm run dev
```

O frontend estará disponível em `http://localhost:5173` e encaminhará chamadas de `/api` para a API local na porta `8080`.

## Validação

```powershell
npm run lint
npm run build
```

## Docker

Na raiz do repositório:

```powershell
docker compose up --build
```

O produto estará disponível em `http://localhost:3000`.
