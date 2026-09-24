# Ambiente comercial fictício — `commercial-demo-v1`

O Residencial Aurora é criado somente por uma operação explícita de PlatformAdmin. A migration `AddCommercialDemoDatasetLedger` acrescenta apenas a tabela de ownership; não insere dados. O seed de desenvolvimento existente permanece independente e não cria o Aurora.

## Cobertura auditada

- Gestão/síndico: Dashboard e relatórios (atendimentos variados e datas distribuídas), Atendimentos, histórico, mensagens, notas, anexos, blocos, unidades, moradores, categorias, prestadores, agenda, Documentos e Assistente operacional.
- Morador: conta Camila Nogueira na unidade A-302, com atendimento e histórico próprios.
- Administradora: Administradora Horizonte, Lívia Monteiro, categoria e solicitação com mensagens e histórico. Funcionários/holerites privados não são populados: exigiriam documentos pessoais/pagamentos simulados e não ajudam esta demonstração.
- Overwatch: usado somente para proteger a ferramenta. Seus painéis globais mostram a organização criada, sem massa artificial adicional.
- WhatsApp: uma conversa de atendimento tem origem e canal WhatsApp persistidos, sem inbound/outbound da integração nem status de entrega fictícios. Não há telefone de usuário, sessão ou envio.
- Telegram experimental: não é exercitado.

## Segurança e ownership

O manifesto `commercial_demo_datasets` guarda a chave lógica e os IDs exatos de cada entidade criada. Create roda em transação serializável e usa a chave única para idempotência. Revert e dry-run consultam as FKs do modelo: se qualquer registro fora do manifesto referenciar um registro demo, a remoção é recusada. Contas e prestadores externos nunca são reaproveitados. Os arquivos locais têm caminhos vinculados aos IDs registrados. Nenhuma chamada de e-mail, WhatsApp, Telegram, webhook ou IA ocorre em Create/Revert; lembretes e atualizações por WhatsApp ficam desligados.

As contas com login recebem **três senhas fortes diferentes fornecidas pelo operador** no Create, não salvas no manifesto, log nem código. Os demais moradores não têm senha. E-mails usam o domínio reservado `.invalid`, telefones de prestadores usam um número não roteável e chaves PIX demo são e-mails `.invalid`.

## Operação

Use apenas uma API com a migration de schema aplicada e um JWT de PlatformAdmin. Não rode Create em produção sem decidir deliberadamente que o showroom deve existir ali. No PowerShell, defina `$apiBase` para a URL HTTPS da API e `$token` com um token PlatformAdmin obtido pelo login normal:

```powershell
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "$apiBase/overwatch/commercial-demo/revert-preview" -Headers $headers

$managerPassword = Read-Host 'Senha forte da gestão demo' -AsSecureString | ConvertFrom-SecureString -AsPlainText
$residentPassword = Read-Host 'Senha forte da moradora demo' -AsSecureString | ConvertFrom-SecureString -AsPlainText
$employeePassword = Read-Host 'Senha forte da administradora demo' -AsSecureString | ConvertFrom-SecureString -AsPlainText
$body = @{ confirmation = 'CREATE commercial-demo-v1'; managerPassword = $managerPassword; residentPassword = $residentPassword; employeePassword = $employeePassword } | ConvertTo-Json
Invoke-RestMethod -Uri "$apiBase/overwatch/commercial-demo/create" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
Remove-Variable body,managerPassword,residentPassword,employeePassword

Invoke-RestMethod -Uri "$apiBase/overwatch/commercial-demo" -Headers $headers
Invoke-RestMethod -Uri "$apiBase/overwatch/commercial-demo/revert-preview" -Headers $headers
Invoke-RestMethod -Uri "$apiBase/overwatch/commercial-demo/revert" -Method Post -Headers $headers -ContentType 'application/json' -Body '{"confirmation":"REVERT commercial-demo-v1"}'
Invoke-RestMethod -Uri "$apiBase/overwatch/commercial-demo" -Headers $headers
```

No estado final, `exists` deve ser `false`. Se `conflicts` não estiver vazio, **não remova dados manualmente por nome**: inspecione as referências externas e decida como desvinculá-las antes de repetir o Revert.

O Revert absorve sessões de login e inscrições web-push dos usuários demo. Mensagens e notas internas criadas depois do Create só são absorvidas quando **atendimento e autor** constam do manifesto original; uma mensagem de autor externo bloqueia a limpeza. Outras gravações posteriores (por exemplo, uma conversa nova no Assistente) ainda podem gerar conflitos e bloquear a limpeza. Isso é intencionalmente fail-safe: sempre execute o dry-run após usar o showroom e investigue cada conflito antes de Revert.

Logins demo após Create: `gestao@aurora.invalid`, `camila@aurora.invalid` e `atendimento@horizonte.invalid`, com as respectivas senhas escolhidas pelo operador. Não envie essas credenciais por e-mail ou mensagem.

## Limite documental

Os dois documentos `.txt` são fictícios e baixáveis. O Create grava chunks com embedding local, para não chamar IA externa. A configuração normal do Assistente usa outro modelo de embedding, portanto a busca semântica documental ainda exige **reindexação explícita posterior**. Com a conta de gestão, abra Documentos no Residencial Aurora e use **Reprocessar** em cada documento (ou selecione os dois e use **Reprocessar selecionados**). A mesma operação é `POST /condominiums/{condominiumId}/documents/{documentId}/reprocess`, autorizada para o módulo Documentos. Ela lê o arquivo local e chama o serviço de embeddings configurado; só execute num ambiente de screenshots com credenciais apropriadas. Aguarde status `Ready`, confirme que não há indicação de reindexação pendente e então teste uma pergunta documental como “O que o regimento diz sobre mudanças?”. Sem esse serviço, marque a validação de RAG como pendente, não como sucesso. As perguntas operacionais estruturadas não dependem dessa etapa.
