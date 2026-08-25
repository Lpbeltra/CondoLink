# Integração WhatsApp

## Finalidade e arquitetura

## Agenda do Manager

Lembretes resolvem o Manager contextual ativo no disparo. Dentro da janela de 24
horas o worker enfileira `SessionText` próprio. Fora da janela nenhum template de
morador ou `manager_new_request` é reutilizado: o canal fica `Skipped` até o
contrato posicional real de `manager_agenda_reminder · pt_BR` ser aprovado e
alinhado. O e-mail da ocorrência continua independente.

```text
WhatsApp__Templates__ManagerAgendaReminder__Name=manager_agenda_reminder
WhatsApp__Templates__ManagerAgendaReminder__Language=pt_BR
Agenda__OperationalTimeZone=America/Sao_Paulo
Agenda__WorkerIntervalSeconds=60
Agenda__WorkerBatchSize=20
```

Nome e idioma são configuráveis, mas a configuração isolada não habilita envio
antes da confirmação dos parâmetros no Meta Manager.

Esta integração usa a WhatsApp Cloud API oficial da Meta como uma porta de
entrada controlada para moradores. Ela não usa WhatsApp Web, QR Code, número
pessoal do síndico ou autenticação dos endpoints comuns da API.

`Request` continua sendo a conversa oficial de atendimento. Eventos recebidos
antes da existência de uma solicitação são guardados em
`whatsapp_inbound_messages`, que fornece auditoria e idempotência.
`whatsapp_sessions` mantém o estado retomável por telefone. Nenhum payload
integral ou token é persistido.

### Atualizações administrativas sem mudança de status

Em `InProgress` e `WaitingForThirdParty`, a administração pode enviar uma nova
mensagem sem transicionar a Request. A mensagem literal fica em `RequestMessage`.
Com janela aberta, a moldura operacional efetiva e o texto são enviados em
`SessionText`. Com janela fechada, `request_status_update · pt_BR` envia somente
o primeiro nome e o botão `request_status_view`; o outbound guarda
`RequestMessageId`. Ao clicar em `Ver atualização`, `context.id` correlaciona o
outbound e entrega seu conteúdo exato, sem menu anterior e sem depender de
`RequestStatusHistoryId`. Retries reutilizam
`request-update:{RequestMessageId}:{ResidentUserId}`.

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

Para pedidos de informação ao morador, configure exatamente
`WhatsApp__Templates__InformationRequested__Name=message_warning` e
`WhatsApp__Templates__InformationRequested__Language=pt_BR`. O corpo aprovado
recebe apenas o primeiro nome em `{{1}}`. Os quick replies usam os payloads
`resident_reply_now` e `resident_reply_later`; o texto visível é usado somente
como compatibilidade quando a Meta não fornecer um identificador.

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

Na transição para `WaitingForResident`, somente o evento específico
`InformationRequested` é enfileirado para o WhatsApp; o histórico e a
notificação interna de status continuam sendo criados. Fora da janela de 24
horas, `message_warning` é enviado com um parâmetro de corpo e os dois botões
de resposta rápida. Mensagens antigas marcadas como `TemplateNotConfigured`
não são reprocessadas automaticamente. O retry administrativo preserva o
snapshot de template gravado na mensagem; portanto, itens antigos sem nome e
idioma não devem ser retentados. O retry continua disponível para mensagens que
já possuíam configuração válida quando foram criadas.

Configuração administrativa protegida:

- `PUT /management/condominiums/{id}/whatsapp/settings`;
- `GET /management/condominiums/{id}/whatsapp/outbound`;
- `POST /management/condominiums/{id}/whatsapp/outbound/{messageId}/retry`.

### Molduras das mensagens operacionais

Cada evento elegível (pedido de informação, mudança de status, resolução,
cancelamento, reabertura) tem uma moldura de texto global — prefixo e sufixo
em torno da mensagem escrita pelo síndico — configurável por
`PlatformAdmin` em `GET/PUT/DELETE /overwatch/messages/{key}`. A edição afeta
somente o texto dentro da janela de 24 horas; fora dela, o conteúdo depende do
template Meta aprovado (`Templates__<Evento>__Name`/`Language`, ver
"Configuração" acima), gerenciado externamente e fora do alcance dessa
customização. `DELETE` restaura o padrão oficial (`IsOverride = false`). Não
existe endpoint para criar um template novo — apenas editar a moldura de um
evento já suportado pelo código. A tela de administração
(`OverwatchMessagesPage`) mostra o modo (template Meta vs. texto livre na
janela), uma prévia com dados fictícios e, quando a moldura foi customizada,
a data da última atualização.

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

## Janela de 24 horas e templates operacionais

A janela da Meta não encerra a `Request` e é independente do timeout de 30
minutos de `WhatsAppSession`. Todo inbound válido registra `ReceivedAt`; a
identidade é resolvida prioritariamente por `IdentifiedUserId`, com telefone
normalizado (inclusive compatibilidade brasileira sem nono dígito) como apoio.

| Intenção/status | Dentro de 24h | Fora de 24h | Contrato Meta |
|---|---|---|---|
| `WaitingForThirdParty`, `Reopened` e atualização genérica | `SessionText` com moldura Overwatch + texto literal | `request_status_update` | `pt_BR`; body `{{1}}` primeiro nome; `request_status_view`/“Ver atualização” |
| `Cancelled` | `SessionText` correspondente | `request_status_update` (não há template específico aprovado) | mesmo contrato genérico |
| `WaitingForResident` | `SessionText` próprio | `resident_reply_required` | contrato e botões existentes preservados |
| `WaitingForResidentClosure` | `SessionText` com conclusão e opções | `resident_closure_confirmation` | `pt_BR`; `{{1}}` primeiro nome; `{{2}}` conclusão literal; `closure_confirm` e `closure_question` |
| Resolver (`Resolved` unilateral) | `SessionText` de finalização | `task_finalization_notification` | `pt_BR`; `{{1}}` primeiro nome; `{{2}}` título da Request; `{{3}}` `FINALIZADA`; `{{4}}` conclusão literal; botão estático “Portal Comvy”; sem confirmação |

`request_status_update` não transporta o texto administrativo. O outbound
persiste o `Content` completo, o conteúdo literal em
`TemplateParameterContent`, `RequestId` e `RequestStatusHistoryId`. Ao receber
“Ver atualização”, o webhook exige o `context.id` do outbound respondido,
reabre a janela e envia imediatamente o `Content` como `SessionText`, sem menu.

Os botões de closure também exigem esse contexto e o outbound persiste
`RequestClosureConfirmationId`. “Finalizar atendimento” decide exatamente essa
confirmação; “Ainda tenho uma dúvida” associa a sessão à mesma Request e à mesma
confirmação para a próxima mensagem. Título traduzido é apenas compatibilidade;
as chaves de negócio são os IDs estáveis. Webhooks e cliques repetidos permanecem
idempotentes pelas chaves externas e pela atualização condicional do estado.

Templates Meta são gerenciados externamente. O Overwatch apenas exibe nome,
idioma e botões; seus overrides afetam somente `SessionText`. Falha de um
template usa o retry/backoff normal e nunca troca por template de outra intenção.

```text
WhatsApp__Templates__StatusChanged__Name=request_status_update
WhatsApp__Templates__StatusChanged__Language=pt_BR
WhatsApp__Templates__ResidentClosureConfirmation__Name=resident_closure_confirmation
WhatsApp__Templates__ResidentClosureConfirmation__Language=pt_BR
WhatsApp__Templates__Resolved__Name=task_finalization_notification
WhatsApp__Templates__Resolved__Language=pt_BR
```
