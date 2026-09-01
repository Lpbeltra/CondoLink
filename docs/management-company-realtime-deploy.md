# Gestão ↔ Administradora: realtime em produção

O frontend usa `VITE_API_URL` como base única para REST e SignalR. Quando vazio,
ambos usam `/api`; quando definido como `https://api.comvy.com.br`, ambos usam
esse domínio. O hub é sempre `/management-company-requests/realtime`, portanto
o endereço observado em produção (`wss://api.comvy.com.br/...`) indica que o
frontend está configurado para acessar a API diretamente; o nginx do frontend
não participa desse fluxo.

O backend mapeia o hub nessa rota, exige o mesmo JWT e aceita negotiate,
WebSocket e fallback do SignalR. O proxy que termina TLS em `api.comvy.com.br`
(Coolify/Traefik ou nginx da API) deve encaminhar `Connection: Upgrade` e
`Upgrade: websocket` para o container ASP.NET, preservar o query string
`access_token` usado pelo SignalR e permitir credentials/CORS para a origem do
portal. O nginx do frontend só participa quando `VITE_API_URL` estiver vazio;
nesse modo sua rota `/api/` encaminha REST e WebSocket para `api:8080`.

Checklist Coolify: habilitar WebSockets no serviço público da API, apontar o
upstream para a porta ASP.NET (`8080` no compose), manter HTTPS/WSS e definir
`VITE_API_URL` de forma consistente com esse domínio. Não configurar um proxy
duplicado no frontend se a API possuir domínio público próprio.
