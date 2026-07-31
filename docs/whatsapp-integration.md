# Integração WhatsApp

## Finalidade e arquitetura

Esta integração usa a WhatsApp Cloud API oficial da Meta como uma porta de
entrada controlada para moradores. Ela não usa WhatsApp Web, QR Code, número
pessoal do síndico ou autenticação dos endpoints comuns da API.

`Request` continua sendo a conversa oficial de atendimento. Eventos recebidos
antes da existência de uma solicitação são guardados em
`whatsapp_inbound_messages`, que fornece auditoria e idempotência.
`whatsapp_sessions` mantém o estado retomável por telefone. Nenhum payload
integral ou token é persistido.

O envio é isolado por `IWhatsAppClient`. A implementação atual usa o Graph API
por `HttpClient`; testes usam um cliente fake. Futuras notificações devem ser
decididas centralmente junto ao `NotificationService`, e não espalhadas pelos
endpoints.

## Configuração

Use variáveis de ambiente ou um secret store:

| Variável | Uso |
| --- | --- |
| `WhatsApp__Enabled` | Ativa os endpoints e o cliente |
| `WhatsApp__ApiVersion` | Versão do Graph API, por exemplo `v23.0` |
| `WhatsApp__PhoneNumberId` | Identificador do número remetente |
| `WhatsApp__BusinessAccountId` | Identificador da conta empresarial |
| `WhatsApp__AccessToken` | Token de acesso da Meta |
| `WhatsApp__VerifyToken` | Token escolhido para verificar o webhook |
| `WhatsApp__AppSecret` | App Secret usado no HMAC SHA-256 |
| `WhatsApp__SessionExpirationMinutes` | Validade da sessão, padrão 30 minutos |
| `WhatsApp__OutboundWorkerEnabled` | Processa a fila persistida; mantenha `false` até o teste |
| `WhatsApp__OutboundBatchSize` | Quantidade por lote, padrão 10 |
| `WhatsApp__OutboundPollingSeconds` | Intervalo de polling, padrão 10 segundos |
| `WhatsApp__OutboundMaxAttempts` | Máximo de tentativas automáticas, padrão 5 |
| `WhatsApp__OutboundInitialRetrySeconds` | Base do backoff, padrão 30 segundos |
| `WhatsApp__Templates__<Evento>__Name` | Nome do template aprovado para notificações fora da janela |
| `WhatsApp__Templates__<Evento>__Language` | Idioma do template, normalmente `pt_BR` |
| `DataProtection__KeysPath` | Diretório persistente das chaves que protegem mensagens de verificação |

Não coloque valores reais no repositório, frontend, logs ou imagens Docker.
Com `WhatsApp__Enabled=false`, a API inicia normalmente e as rotas retornam 404.

## Webhook

- Verificação: `GET /integrations/whatsapp/webhook`
- Recebimento: `POST /integrations/whatsapp/webhook`
- URL pública geral: `https://<tunnel>/integrations/whatsapp/webhook`
- URL no Coolify: `https://<domínio-público-da-api>/integrations/whatsapp/webhook`

Na configuração do aplicativo Meta, informe a URL pública e o mesmo valor de
`WhatsApp__VerifyToken`. Assine o campo `messages`. A Meta enviará
`X-Hub-Signature-256=sha256=<hex>`; o backend calcula HMAC SHA-256 sobre os
bytes exatos do corpo usando `WhatsApp__AppSecret` e compara em tempo constante.

Para desenvolvimento, inicie API e banco normalmente, exponha a porta HTTPS
com o Cloudflare Tunnel e use a URL geral acima. O tunnel deve encaminhar o
corpo e o cabeçalho de assinatura sem alterações.

## Comportamento atual

São suportados texto, `button_reply`, `list_reply` e eventos técnicos sem
mensagem. Outros tipos recebem orientação para usar texto. O telefone é
normalizado para E.164 brasileiro; não há comparação por sufixo.

Um usuário ativo com um único condomínio ativo entra diretamente no menu. Com
mais de um, deve escolher explicitamente. Telefone desconhecido, ambíguo,
usuário inativo ou sem vínculo ativo não ganha acesso. Nunca são solicitados
senha ou CPF completo.

Comandos globais: `Menu`, `Início`, `Voltar`, `Cancelar`, `Ajuda`, `Sair` e
`0` quando exibido. Sessões expiradas reiniciam com explicação.

O menu permite:

1. iniciar o estado `StartingNewRequest` e coletar uma descrição, sem criar a
   solicitação ainda;
2. consultar até cinco solicitações abertas do próprio usuário e condomínio;
3. encerrar a sessão.

## Segurança e operação

O corpo é limitado a 256 KiB e profundidade JSON 32. Eventos duplicados usam
índice único pelo ID externo e retornam sucesso sem nova resposta. Logs contêm
ID externo, resultado e telefone mascarado; segredos e payload integral não são
registrados.

Monitore respostas HTTP 503 e falhas de envio. Não há retry infinito nem fila
externa neste lote. A Meta pode reenviar webhooks; a idempotência do recebimento
protege o processamento.

## Fluxo completo

```text
Menu
├─ Nova solicitação
│  └─ condomínio → unidade → categoria → descrição → anexos → revisão
│     └─ confirmação → Request + histórico + anexos + notificação
└─ Solicitações abertas
   └─ seleção → detalhe
      ├─ mensagem → RequestMessage
      ├─ anexo → RequestAttachment
      └─ histórico resumido
```

Estados adicionais: `SelectingUnit`, `SelectingCategory`,
`CollectingDescription`, `CollectingNewRequestAttachments`,
`ReviewingNewRequest`, `ViewingRequest`, `ReplyingToRequest`,
`CollectingExistingRequestAttachment`, `ViewingRequestHistory` e
`ConfirmingResume`.

Solicitações e mensagens possuem origem/canal (`Portal`, `WhatsApp` ou
`System`) para futura apresentação na timeline. Registros anteriores recebem
`Portal` como padrão.

### Mídia

Imagens JPG/JPEG, PNG, WebP e documentos PDF são aceitos, até 15 MB cada e seis
por rascunho/interação. O backend consulta os metadados da mídia no Graph API,
faz download autenticado, valida MIME e extensão e sanitiza o nome.

Anexos de uma nova solicitação ficam em `whatsapp-drafts/{sessionId}` e possuem
metadados persistidos. Na confirmação, são copiados para `requests/{requestId}`
e associados na mesma operação da solicitação. Em falha, as cópias são
removidas e o rascunho é preservado. Cancelamento e expiração removem os
temporários oportunisticamente quando a sessão volta a ser processada.

### Retomada e expiração

Descrição, categoria, unidade, solicitação e anexos ficam persistidos. `Ajuda`
não altera o estado. `Menu` solicita confirmação antes de descartar um
rascunho; `Cancelar` descarta diretamente. Sessões expiradas nunca criam
solicitações e removem anexos temporários.

### Consulta e conversa

Somente solicitações do usuário e condomínio em contexto são listadas. O
detalhe mostra categoria, unidade, status, datas e última mensagem. Solicitações
resolvidas ou canceladas são consultivas. Mensagens usam `RequestMessage`,
anexos usam `RequestAttachment` e notificações internas continuam passando
pelo `NotificationService`.

## Limitações

Listas de condomínios, unidades e categorias são limitadas às dez primeiras;
solicitações e histórico usam páginas de cinco itens. Não há áudio,
transcrição, IA, classificação, cadastro, campanhas, templates de marketing,
notificações espontâneas ou ações administrativas.

Validações manuais recomendadas: download real de imagem e PDF na Meta,
cancelamento após mídia, retomada depois de reiniciar a API, concorrência de
duas mensagens rápidas e visualização da origem no banco/timeline futura.

## Atualizações transacionais de solicitações (lote 9C)

O `NotificationService` continua sendo o ponto de entrada dos eventos internos.
Depois de persistir a notificação do portal, ele grava uma entrega na tabela
`whatsapp_outbound_messages`. A fila é persistente, idempotente por evento de
origem e processada em segundo plano; indisponibilidade da Meta não desfaz a
mensagem, o histórico ou a alteração de status da solicitação.

São elegíveis: mensagem da administração, pedido de informação, mudança de
status, resolução, cancelamento e reabertura. A entrega exige simultaneamente:
integração global ativa, worker ativo, condomínio ativo para atualizações,
morador ativo com vínculo ao condomínio, telefone válido e não ambíguo e
preferência individual ativa. Os comandos `ativar atualizações` e
`parar atualizações` alteram a preferência individual.

Quando o último contato recebido daquele telefone ocorreu nas últimas 24 horas,
o worker envia texto de sessão. Fora da janela, envia exclusivamente o template
aprovado configurado para o evento. Template ausente causa `Skipped`, nunca
fallback para texto livre.

Configuração administrativa protegida:

- `PUT /management/condominiums/{id}/whatsapp/settings`;
- `GET /management/condominiums/{id}/whatsapp/outbound`;
- `POST /management/condominiums/{id}/whatsapp/outbound/{messageId}/retry`.

Para diagnóstico global, inclusive das mensagens de confirmação que não
pertencem a um condomínio, um `PlatformAdmin` pode usar:

- `GET /overwatch/whatsapp/outbound?status=Pending&take=50`;
- `POST /overwatch/whatsapp/outbound/{messageId}/retry`.

As respostas mostram estado, tentativas, datas e último código/descrição de
falha. Não retornam telefone, código de confirmação nem conteúdo protegido.

O retry automático usa backoff exponencial, limite configurável e classificação
de 408, 429 e 5xx como transitórios. Retry manual é limitado a três e não é
permitido depois de `Sent`, `Delivered` ou `Read`. Itens interrompidos em
`Processing` voltam para a fila após dez minutos.

O webhook correlaciona o ID externo e registra `sent`, `delivered`, `read` e
`failed`, preservando estados mais avançados diante de eventos atrasados. IDs
desconhecidos são confirmados e ignorados.

Variáveis operacionais estão em `.env.example`. Comece com
`WhatsApp__OutboundWorkerEnabled=false`, cadastre e obtenha aprovação dos
templates na Meta, habilite um condomínio, habilite explicitamente o usuário e
somente então ligue o worker. Não registre tokens, payloads integrais ou
conteúdo sensível em logs.

Referências oficiais consultadas em 2026-07-28:

- WhatsApp Business Messaging Policy:
  https://business.whatsapp.com/policy
- Meta WhatsApp Cloud API, webhook payload reference:
  https://www.postman.com/meta/whatsapp-business-platform/folder/tduohwq/webhook-payload-reference

## Teste real da integração no Coolify

### 1. Variáveis

Cadastre como variáveis/segredos do serviço da API, sem colocá-las no Git:

```text
WhatsApp__Enabled=true
WhatsApp__OutboundWorkerEnabled=true
WhatsApp__ApiVersion=v23.0
WhatsApp__PhoneNumberId=<id do número temporário>
WhatsApp__AccessToken=<segredo>
WhatsApp__AppSecret=<segredo>
WhatsApp__VerifyToken=<segredo escolhido por você>
WhatsApp__OutboundPollingSeconds=10
DataProtection__KeysPath=/app/data-protection-keys
```

`WhatsApp__BusinessAccountId` pode ser mantido para referência operacional,
mas o envio e o webhook atuais não dependem dele. Os templates e seus idiomas
são necessários para notificações de solicitações fora da janela de 24 horas.

As configurações versionadas continuam com integração e worker desativados.

### 2. Volume do Data Protection

Monte um volume persistente do Coolify em:

```text
/app/data-protection-keys
```

O valor deve coincidir com `DataProtection__KeysPath`. Não apague nem substitua
as chaves durante redeploys. O `docker-compose.yml` local usa o volume nomeado
`condolink_data_protection_keys`. Sem o volume, mensagens protegidas já
enfileiradas podem ficar impossíveis de descriptografar após recriar o container.

### 3. Meta

1. No app da Meta, configure a callback como
   `https://<domínio-público-da-api>/integrations/whatsapp/webhook`.
2. Informe exatamente o valor de `WhatsApp__VerifyToken`.
3. Assine o evento `messages`.
4. Confirme que o App Secret do ambiente pertence ao mesmo app; o POST exige
   `X-Hub-Signature-256` válido e não possui bypass.
5. No painel do número temporário, adicione e valide o telefone destinatário
   usado pelo usuário do Comvy.

### 4. Execução

1. Cadastre no usuário o mesmo telefone autorizado na Meta.
2. Do telefone, envie uma mensagem ao número temporário para abrir a janela de
   24 horas.
3. Confirme que o webhook recebeu a mensagem e identificou o usuário pelo
   telefone cadastrado.
4. Percorra o menu de atendimento e, se aplicável, crie uma solicitação.
5. Confirme que respostas e notificações são enviadas pela fila e pelo worker.

### 5. Diagnóstico e encerramento

- Consulte `GET /overwatch/whatsapp/outbound` como `PlatformAdmin`, filtrando
  por `Pending`, `Sent` ou `PermanentlyFailed`.
- Verifique logs pelos IDs técnicos das mensagens. Eles não contêm token,
  App Secret, telefone completo ou conteúdo sensível.
- Para falha elegível, use o endpoint global de retry no máximo conforme o
  limite já implementado.
- Confirme que o volume contém arquivos de chave e permanece montado depois de
  um redeploy.
- Após o teste, se não quiser receber novos eventos/envios, volte
  `WhatsApp__Enabled` e `WhatsApp__OutboundWorkerEnabled` para `false` no
  Coolify. Não remova o volume de chaves enquanto houver mensagens pendentes.

O webhook limita o corpo a 256 KiB, valida JSON e assinatura antes de
processar, audita IDs externos para idempotência e retorna `200` após processar
os eventos aceitos. Falhas internas retornam `503`, permitindo nova tentativa
pela Meta.
