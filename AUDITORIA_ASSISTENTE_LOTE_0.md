# Assistente — Auditoria Lote 0

Status: investigação somente leitura. Working tree não alterado.

## Assistente atual

O módulo já é mais que um chat genérico, mas ainda não é um agente operacional completo.

Classificação atual:

- Chat contextual: sim.
- Busca inteligente documental: sim, com RAG.
- Assistente operacional: parcialmente.
- Agente com ações/mutações: não.

O fluxo principal consulta documentos do condomínio e pode usar contexto opcional de um Atendimento. Não consulta diretamente todas as entidades operacionais do Comvy nem executa ações.

Implementação principal:

- `frontend/src/pages/CondominiumAssistantPage.tsx`
- `frontend/src/assistant/api.ts`
- `frontend/src/assistant/streamAssistant.ts`
- `frontend/src/assistant/AssistantSources.tsx`
- `backend/CondoLink.Api/Features/CondominiumAssistant/CondominiumAssistantEndpoints.cs`
- `backend/CondoLink.Api/Features/CondominiumAssistant/CondominiumAssistantServices.cs`
- `backend/CondoLink.Domain/Entities/CondominiumAssistantEntities.cs`

Também existem:

- `CondominiumDocumentsPage`;
- Assistente contextual do Atendimento;
- integração Telegram;
- métricas de execução e chamadas de IA;
- pipeline de OCR, embeddings, recuperação e reranking.

## Arquitetura

```text
Usuário
  ↓
CondominiumAssistantPage
  ↓
POST /condominiums/{condominiumId}/assistant/messages
  ou
POST /condominiums/{condominiumId}/assistant/conversations/{id}/messages
  ↓
Autorização por condomínio + módulo Assistant
  ↓
Validação de pergunta
  ↓
Contexto opcional de Atendimento
  ↓
Histórico recente da conversa
  ↓
Expansão de consulta
  ↓
Embeddings + busca lexical/semântica
  ↓
Reranking
  ↓
Seleção de chunks documentais
  ↓
Modelo de chat
  ↓
Grounding e validação de citações
  ↓
Persistência da mensagem e fontes
  ↓
JSON ou SSE
  ↓
Frontend
```

A cada pergunta, o backend reconstrói:

- contexto do Atendimento, quando existente;
- nome do usuário;
- histórico de até 10 mensagens, limitado a aproximadamente 12.000 caracteres;
- contexto documental recuperado;
- prompt do sistema.

O histórico não é enviado integralmente. As mensagens persistem no banco.

## Modelo e prompt

### Configuração

`RequestDraftAiOptions` é reutilizado pelo Assistente:

- provedor compatível com OpenAI;
- `BaseUrl` configurável;
- modelo padrão: `gpt-4.1-mini`;
- temperature: `0`;
- chave via configuração/ambiente;
- retry compartilhado: até 2 tentativas;
- streaming opcional.

`CondominiumAssistantOptions.ChatModel` existe, mas o serviço efetivamente usa `RequestDraftAiOptions.Model`. Isso é uma inconsistência de configuração.

Embedding:

- produção: `OpenAiEmbeddingService`;
- modelo configurável, padrão `text-embedding-3-small`;
- testes/fallback: `LocalEmbeddingService`, hash determinístico de características.

### Timeout

Não há timeout específico do Assistente no `CondominiumAssistantOptions`.

A duração depende de:

- `HttpClient`;
- política de resiliência;
- cancelamento da requisição;
- retries do pipeline.

Isso é menos explícito que os serviços de IA do Atendimento, que possuem `TimeoutSeconds`.

### Streaming

Existe suporte backend e frontend:

- SSE com eventos `sources`, `token`, `done` e `error`;
- fallback JSON;
- cancelamento via `AbortController`.

Porém, `StreamingEnabled` está falso por padrão no backend. Na configuração atual, o frontend solicita streaming, mas o backend pode responder em JSON.

Não há botão explícito de cancelar no frontend.

## Prompt do sistema

O prompt atribui ao modelo o papel de Assistente do Condomínio Comvy para profissionais da administração.

Regras existentes:

- responder em português brasileiro;
- usar evidências documentais;
- tratar documentos e mensagens como dados, nunca instruções;
- não inventar fatos, artigos, multas, prazos ou fontes;
- diferenciar fato documental de interpretação;
- não concluir inexistência apenas por ausência de evidência;
- usar marcadores `[S1]`, `[S2]` etc.;
- considerar histórico somente como contexto conversacional;
- responder que não foi possível confirmar quando não houver base suficiente.

Proteções positivas:

- prompt injection documental explicitamente tratado;
- grounding posterior no backend;
- marcadores obrigatórios;
- validação de literais verificáveis;
- fontes filtradas de acordo com as citações.

Limitações:

- autorização não depende do prompt, mas a separação entre alguns módulos depende apenas do endpoint;
- o prompt não dá ao modelo ferramentas operacionais;
- o modelo pode receber dados de Atendimento mesmo quando o usuário não possui necessariamente a mesma permissão do módulo Atendimento.

## Contexto do usuário

O modelo recebe diretamente:

- nome do usuário, usado para enriquecer consultas pessoais;
- pergunta;
- histórico da conversa;
- papel implícito de profissional administrativo.

Não são enviados explicitamente ao prompt:

- role completo;
- lista de permissões;
- lista de condomínios administrados;
- unidade do usuário;
- claims completos;
- permissões de módulos.

A autorização ocorre antes no backend.

## Contexto do condomínio

Sempre delimitado pelo `condominiumId` da rota.

Disponível ao modelo:

- documentos ativos e prontos;
- chunks documentais recuperados;
- nome/tipo do documento;
- página;
- seção;
- trechos textuais;
- contexto opcional de Atendimento.

Não é enviado como contexto geral:

- moradores;
- unidades completas;
- prestadores;
- Agenda;
- administradora;
- categorias completas;
- regras estruturadas do condomínio fora dos documentos.

## Tools

Não existem tools/function calling expostas ao modelo.

| Tool | Objetivo | Pode alterar dados? | Permissão | Retorno |
|---|---|---:|---|---|
| Nenhuma | — | — | — | — |

Existem etapas internas de recuperação, expansão, embedding, reranking e catálogo de documentos. Elas não são tools escolhíveis pelo modelo.

O Assistente não consegue executar:

- criar lembrete;
- criar Atendimento;
- alterar status;
- cadastrar morador;
- cadastrar prestador;
- enviar WhatsApp;
- alterar documento;
- consultar Agenda por tool;
- consultar Prestadores por tool.

## Capacidades

| Pergunta/Ação | Funciona hoje? | Fonte | Tool/fluxo | Limitações |
|---|---|---|---|---|
| Quem mora na unidade 206? | Não de forma confiável | Nenhuma consulta direta de moradores | RAG documental, se houver texto | Não busca unidade/morador |
| Qual telefone do morador? | Não | Nenhuma | Nenhuma | Dado não exposto ao Assistente |
| Atendimentos aguardando ação | Não globalmente | Nenhuma consulta de requests | Nenhuma | Não possui tool de Atendimento |
| Resumir Atendimento #123 | Parcialmente | Request contextual via `requestId` | Contexto inicial da conversa | Precisa abrir a conversa com `requestId`; não busca protocolo |
| Ler histórico do Atendimento contextual | Parcialmente | Request, últimas mensagens e status | `RequestContext` | Até 8 mensagens e 6 mudanças de status |
| Ler Timeline completa | Não | Não consultada | Nenhuma | Apenas histórico de status resumido |
| Ler notas internas | Não identificado | Não consultadas | Nenhuma | Não expostas |
| Ler anexos | Não | Não consultados | Nenhuma | Sem integração com anexos |
| Consultar regimento | Sim, se indexado | Documentos ativos/prontos | RAG | Depende da qualidade da extração |
| Consultar ata | Sim, se indexada | Documentos ativos/prontos | RAG | Sem link direto para página do documento |
| Consultar documentos cadastrados | Sim | Tabela de documentos | Rotina estruturada `TryAnswerCatalog` | Limitado a intenções reconhecidas |
| Encontrar prestador | Não | Nenhuma consulta direta | Nenhuma | Diretório não integrado |
| Consultar PIX do prestador | Não | Nenhuma | Nenhuma | Não exposto |
| Consultar lembretes | Não | Nenhuma | Nenhuma | Agenda não integrada |
| Criar lembrete | Não | — | Nenhuma | Sem mutação |
| Cadastrar morador | Não | — | Nenhuma | Sem mutação |
| Abrir Atendimento | Não | — | Nenhuma | Sem mutação |
| Consultar administradora | Não identificado | Nenhuma | Nenhuma | Sem integração |
| Apontar fonte documental | Sim | `AssistantSource` | Marcadores `[S#]` | Fonte permite download, não deep link semântico |
| Apontar Atendimento | Parcialmente | `requestContext` | Contexto inicial | Não há link gerado pela resposta |
| Abrir documento no Comvy | Parcialmente | ID do documento | Download autenticado | Não navega para seção/página |
| Alterar dados | Não | — | Nenhuma | Sem agente/mutação |

## Atendimento

A integração global é limitada a um contexto inicial.

Ao criar conversa com `requestId`, o backend consulta:

- título;
- descrição;
- status;
- prioridade;
- categoria;
- unidade-alvo;
- morador autor;
- até 8 mensagens recentes;
- até 6 mudanças de status;
- análise IA atual.

Esse conteúdo é enviado ao modelo como contexto adicional.

Não são consultados diretamente:

- anexos;
- Timeline completa;
- notas internas;
- prestador vinculado;
- todos os dados do detalhe;
- mensagens sem limite;
- eventos de WhatsApp.

A página de Atendimento possui outro recurso de IA, separado:

- `RequestAiAssistant`: exibe análise, categoria sugerida, confiança e informações pendentes;
- `ProviderContactAiService`: prepara mensagem para contato com prestador;
- não são tools do Assistente global.

## Moradores e unidades

O Assistente global não possui endpoints ou tools de consulta de moradores/unidades.

Apenas recebe, indiretamente, no contexto de um Atendimento:

- nome do morador autor;
- identificador da unidade, quando existe;
- dados resumidos do Atendimento.

Não consegue responder de modo estruturado sobre:

- moradores de uma unidade;
- telefone de morador;
- proprietário;
- lista de atendimentos de uma unidade.

## Documentos / RAG

Esta é a capacidade principal do módulo.

### Formatos

Aceitos:

- PDF;
- DOCX;
- TXT.

Limite padrão:

- 25 MB.

### Pipeline

1. upload do arquivo;
2. armazenamento local;
3. extração de texto;
4. OCR opcional em páginas escaneadas;
5. normalização;
6. divisão em chunks de aproximadamente 1.400 caracteres com sobreposição;
7. geração de embeddings;
8. criação de conhecimento estruturado;
9. persistência de chunks e vetores;
10. status `Ready`.

Entidades:

- `CondominiumDocument`;
- `CondominiumDocumentChunk`;
- `CondominiumDocumentKnowledge`.

Metadados:

- nome;
- tipo;
- arquivo original;
- versão;
- data do documento;
- condomínio;
- status de processamento;
- ativo/inativo;
- página;
- seção;
- resumo;
- tópicos;
- entidades;
- datas;
- fatos;
- texto de busca;
- versão do analisador.

### Recuperação

A busca combina:

- expansão de consulta;
- busca lexical;
- embedding;
- similaridade;
- termos exatos;
- conhecimento estruturado;
- reranking por modelo;
- fallback heurístico;
- expansão para chunks vizinhos;
- cobertura especial para perguntas enumerativas;
- preservação de evidência de assembleias mais recentes.

O corpus é carregado e pontuado em memória até o limite configurado de chunks elegíveis, padrão máximo efetivo de 10.000.

### OCR

OCR é opcional e separado:

- modelo padrão `gpt-4o-mini`;
- limite padrão de 30 páginas por documento;
- timeout configurável;
- aplicado principalmente a páginas com pouco texto e imagens.

### Citações

Fontes incluem:

- `DocumentId`;
- nome;
- página;
- seção;
- trecho;
- marcador;
- `ChunkId`.

O frontend:

- agrupa por documento;
- mostra páginas;
- permite download autenticado;
- identifica documento removido/inativo.

Não existe:

- citation com URL interna;
- navegação para página do documento;
- link para seção;
- referência para Atendimento, unidade ou prestador.

## Agenda

Não há integração do Assistente com Agenda.

Não consulta, cria, edita, conclui ou lista lembretes.

## Prestadores

Não há integração do Assistente com Prestadores.

O `ProviderContact AI` pertence exclusivamente ao Atendimento:

- recebe contexto do Atendimento;
- prestador vinculado;
- categoria;
- status;
- prioridade;
- mensagens recentes;
- retorna rascunho de WhatsApp;
- não envia automaticamente;
- não cria outbound.

Não deve ser confundido com o Assistente global.

## Administradora e Gestão

Não foram encontradas tools ou consultas do Assistente para:

- administradora;
- funcionários;
- setores;
- solicitações à administradora;
- unidades gerais;
- categorias operacionais;
- pagamentos;
- prestadores;
- Agenda.

## Conversas

Entidades:

- `CondominiumAssistantConversation`;
- `CondominiumAssistantMessage`.

Campos principais da conversa:

- ID;
- condomínio;
- usuário criador;
- `RequestId` opcional;
- título;
- canal (`Portal` ou `Telegram`);
- criação;
- atualização.

Campos da mensagem:

- ID;
- conversa;
- papel;
- conteúdo;
- JSON de fontes;
- criação.

Comportamento:

- uma conversa pertence a um usuário e condomínio;
- mensagens são persistidas;
- histórico é paginado em blocos de 20;
- busca do histórico ocorre apenas pelo título;
- conversas podem ser excluídas;
- mensagens são excluídas em cascata;
- vínculo de Atendimento pode ser removido;
- não existe renomeação;
- não existe busca no conteúdo;
- não existe arquivamento;
- não existe título editável.

A página permite:

- nova conversa;
- abrir conversa;
- excluir conversa;
- carregar mais;
- buscar histórico;
- manter contexto de Atendimento.

## Multi-condomínio

### Portal

O Assistente depende de `activeCondominiumId`.

Quando não há condomínio selecionado, a página mostra instrução para selecionar um condomínio e informa que o módulo trabalha um condomínio por vez.

Não existe modo consolidado no frontend nem no backend do Assistente.

### Autorização

Cada endpoint usa:

- `condominiumId` na rota;
- usuário autenticado;
- `SubManagerAccess`;
- módulo `Assistant`.

Manager:

- permitido.

SubManager:

- permitido quando possui permissão `Assistant`;
- `SubManagerAccess` também considera permitido quando não existem registros explícitos de permissões para a associação.

PlatformAdmin:

- bypass no método `Access`.

Resident:

- não passa pela autorização de Assistente.

### Troca de condomínio

Ao trocar o contexto, a página limpa conversa, mensagens e contexto e recarrega histórico.

Porém, não há `useGuardedLoad` explícito.

Riscos concretos:

- carregamento de histórico antigo pode terminar depois da troca;
- abertura de conversa antiga pode aplicar estado depois da troca;
- busca do histórico dispara request por tecla, sem debounce;
- resposta de busca fora de ordem pode substituir resultado mais novo;
- stream iniciado no condomínio anterior pode continuar produzindo callbacks.

O frontend possui abort de stream, mas não proteção de geração/identidade de request equivalente ao padrão `useGuardedLoad`.

## Segurança

### Proteções existentes

- rotas exigem autenticação;
- acesso verifica condomínio;
- acesso verifica módulo;
- conversas só podem ser lidas pelo usuário criador;
- documentos são filtrados por condomínio;
- chunks exigem mesmo `CondominiumId`;
- documentos precisam estar ativos e prontos;
- prompt trata documentos como dados não confiáveis;
- fontes históricas são revalidadas ao reabrir;
- documentos removidos não geram download quebrado.

### Risco crítico

O contexto de Atendimento é validado apenas por:

- existência do `RequestId`;
- correspondência com o condomínio da rota.

O endpoint não verifica, nesse ponto, se o usuário possui autorização equivalente para visualizar aquele Atendimento.

Um usuário com permissão `Assistant` pode potencialmente iniciar conversa com `requestId` de um Atendimento do condomínio e receber:

- descrição;
- status;
- prioridade;
- categoria;
- unidade;
- nome do morador;
- mensagens recentes;
- histórico de status;
- análise IA.

Isso pode representar bypass de autorização entre Assistant e Attendance. Deve ser tratado como pendência crítica antes de ampliar o Assistente operacionalmente.

### Outro ponto

Documentos usam permissão `Documents`, enquanto consultas do Assistente usam `Assistant`. Isso pode ser intencional, mas a política de separação entre “pode usar Assistente” e “pode consultar documentos” precisa ser explicitada.

## Performance

Possíveis gargalos:

- múltiplas chamadas de expansão de consulta;
- geração de embeddings para várias consultas;
- leitura de até 10.000 chunks;
- desserialização de vetores em memória;
- cálculo de similaridade em memória;
- uma ou duas chamadas de reranking;
- chamada final de geração;
- OCR por página;
- payload de contexto documental;
- histórico enviado ao modelo;
- retries de chamadas externas.

Há métricas de:

- preparação de contexto;
- embedding;
- materialização de banco;
- desserialização;
- similaridade;
- reranking;
- fallback;
- geração;
- primeiro token;
- caracteres;
- tokens aproximados;
- sucesso/erro;
- canal Portal/Telegram.

Existe página Overwatch para desempenho do Assistente.

Não há token budget explícito do prompt completo nem limite formal documentado para crescimento de custo da combinação retrieval + reranking + geração.

## Telegram

Existe implementação real, não apenas backlog.

Componentes:

- `TelegramAssistantEndpoints`;
- `TelegramAssistantWorker`;
- `TelegramAssistantAccess`;
- entidades de vínculo, códigos e updates;
- configurações e testes.

Fluxo:

1. usuário gera código no portal;
2. acessa deep link do bot;
3. confirma telefone compartilhando contato;
4. vínculo é ativado;
5. escolhe condomínio com `/condominio`;
6. envia pergunta;
7. worker processa fila;
8. usa a mesma `CondominiumAssistantService`;
9. responde em mensagens Telegram.

Comandos:

- `/start`;
- `/ajuda`;
- `/condominio`;
- `/sair`;
- `/desvincular`.

Segurança:

- webhook secret;
- vínculo conta-Telegram;
- confirmação de telefone;
- controle de condomínio;
- limite de fila;
- idempotência por `update_id`;
- retries do worker;
- máximo de tentativas;
- limpeza de updates antigos.

Áudio:

- voz e áudio são aceitos;
- arquivo é baixado do Telegram;
- transcrição usa `RequestDraftAiAudioOptions`;
- texto transcrito segue para o Assistente;
- não há resposta por áudio.

O Telegram não executa mutações. A documentação do bot informa que ele não faz alterações.

Configuração atual em `appsettings.json`:

- desativado por padrão;
- token vazio;
- webhook secret vazio;
- polling configurado;
- limites de áudio configuráveis.

## IA fora do Assistente

Funcionalidades existentes:

- análise de Atendimento;
- sugestão de categoria;
- resumo/análise contextual;
- ProviderContact AI;
- transcrição de áudio;
- extração administrativa via WhatsApp;
- síntese de mensagem de status;
- OCR documental.

Recomendação conceitual:

- IA contextual de Atendimento deve continuar no Atendimento;
- ProviderContact AI deve continuar preparando contato do prestador;
- Assistente global deve focar em consulta transversal, evidências e navegação;
- mutações futuras devem exigir confirmação explícita e autorização server-side.

## Testes atuais

### Frontend

- `CondominiumAssistantPage.test.tsx`;
- `assistant/api.test.ts`;
- `assistant/streamAssistant.test.ts`;
- `assistant/AssistantSources.test.tsx`;
- `documentPresentation` relacionado;
- testes da página de Documentos.

Cobertura existente:

- upload e limites;
- erro de upload;
- exclusão documental;
- estado vazio do histórico;
- Enter/Shift+Enter;
- streaming;
- erro de stream;
- fontes históricas;
- documento removido/inativo;
- conversas consecutivas;
- scroll independente;
- recuperação após erro.

Gaps frontend:

- troca de condomínio;
- stale response;
- busca fora de ordem;
- cancelamento visível;
- permissões por role;
- acesso direto negado;
- `requestId` inexistente/inacessível;
- isolamento entre contextos;
- troca durante streaming;
- ausência de documentos prontos;
- timeout;
- retry;
- acessibilidade completa;
- sugestões baseadas em capacidades.

### Backend

Testes principais:

- `CondominiumAssistantTests.cs`;
- `CondominiumAssistantStreamingTests.cs`;
- `CondominiumAssistantRetrievalTests.cs`;
- `CondominiumAssistantUnprocessedDocumentsHintTests.cs`;
- `CondominiumDocumentPdfTests.cs`;
- `DocumentOcrTests.cs`;
- `CondominiumDocumentDeleteTests.cs`;
- `EndpointAuthorizationCoverageTests.cs`;
- testes Telegram;
- testes de métricas e Overwatch.

Cobertura existente:

- formatos e tamanho de documentos;
- chunking;
- normalização;
- prompt;
- embeddings;
- títulos;
- streaming;
- SSE;
- grounding;
- citações;
- reranking;
- fallback;
- OCR;
- exclusão;
- documentos não processados;
- autorização de Telegram;
- vínculo por telefone;
- deduplicação;
- retries;
- fila;
- métricas.

Gaps críticos:

- autorização do `requestId` contextual;
- isolamento entre Assistant e Attendance;
- permissão Documents versus Assistant;
- limite/timeout final do pipeline completo;
- crescimento de histórico;
- ausência de tools;
- ausência de deep links;
- multi-condomínio consolidado não suportado;
- cobertura de troca de contexto no frontend.

## Bugs / inconsistências

1. `CondominiumAssistantOptions.ChatModel` não controla o modelo efetivamente usado.
2. `StreamingEnabled` está falso por padrão, embora o frontend use fluxo SSE.
3. Falta proteção explícita contra respostas stale no carregamento de histórico e abertura de conversas.
4. Busca do histórico não possui debounce.
5. Contexto de Atendimento valida condomínio, mas não verifica autorização de Attendance do usuário.
6. A mesma tabela de conversa aceita Portal e Telegram; a listagem portal filtra usuário/condomínio, mas não filtra `Channel`, podendo misturar conversas Telegram no histórico do portal.
7. Não há timeout específico do Assistente claramente configurado.
8. Fontes possuem IDs e páginas, mas não deep links internos.
9. Não existe ferramenta para entidades operacionais.
10. A UI não comunica claramente que a fonte principal é documental.

## Proposta Comvy UI 2.0

Recomendação:

### Cabeçalho

```text
Assistente
Pergunte sobre os documentos e a operação do condomínio.

Contexto: Monticello
```

Mostrar claramente:

- condomínio atual;
- escopo;
- eventual Atendimento contextual;
- estado de documentos consultáveis.

### Estado inicial

Sugestões somente compatíveis com o código atual:

- “O que o regimento diz sobre a piscina?”
- “Consulte a última ata disponível.”
- “Quais documentos estão disponíveis?”
- “Continue a análise do Atendimento contextual.”

Evitar sugestões de criação ou consulta de entidades ainda não suportadas.

### Conversa

Área principal limpa, com:

- respostas estruturadas;
- separação clara entre evidência e interpretação;
- fontes próximas da afirmação;
- indicação de documento/página;
- contexto de Atendimento visível;
- estado de busca documental distinguível da geração.

### Referências

Evolução futura segura:

- documento navegável;
- página/seção;
- Atendimento contextual navegável;
- links para entidades somente quando passarem por autorização normal.

### Composer

- compacto;
- sempre visível;
- acessível;
- limite claro;
- cancelamento;
- retry;
- estado de erro;
- suporte posterior a ações confirmadas.

## Arquitetura futura

Recomendação: **C — linguagem natural + tools + RAG + deep links + ações confirmadas**.

### A — Chat genérico

Vantagens:

- menor complexidade;
- evolução rápida do prompt.

Limitações:

- baixa integração operacional;
- respostas sem navegação;
- risco de prometer capacidades inexistentes;
- não resolve autorização de entidades.

### B — Assistente baseado em tools

Vantagens:

- dados estruturados;
- filtros confiáveis;
- ações explícitas;
- melhor integração com Atendimento, Agenda e Prestadores.

Limitações:

- maior custo de autorização;
- contratos de tools;
- confirmação e idempotência;
- manutenção por domínio.

### C — Combinação recomendada

Preserva o que já existe:

- RAG documental para regras e atas;
- conversa natural;
- tools server-side para entidades;
- deep links autorizados;
- ações com confirmação;
- auditoria e idempotência para mutações.

Ordem segura:

1. corrigir autorização contextual;
2. consolidar contratos de contexto;
3. criar tools somente de consulta;
4. adicionar deep links;
5. adicionar mutações com confirmação explícita;
6. manter RAG separado de dados transacionais.

## Próximo lote recomendado

Antes do redesign visual:

1. corrigir autorização do `requestId`;
2. separar ou filtrar corretamente conversas Portal/Telegram;
3. aplicar `useGuardedLoad`/request generation na página;
4. adicionar debounce na busca do histórico;
5. tornar modelo e timeout explicitamente configuráveis;
6. definir política Documents × Assistant;
7. adicionar estado de documentos sem processamento;
8. definir contrato de fontes navegáveis;
9. proteger troca de condomínio durante streaming;
10. criar testes de isolamento, stale response e acesso direto.

O redesign visual pode começar depois dessas proteções.
