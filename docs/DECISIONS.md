# CondoLink — Architecture Decisions

## Objetivo

Este documento registra as principais decisões técnicas e de domínio do CondoLink.

Cada decisão deve explicar:

* o que foi decidido;
* por que foi decidido;
* quais consequências essa decisão produz.

As decisões podem evoluir, mas não devem ser alteradas silenciosamente. Quando uma decisão mudar, o documento deve registrar a nova escolha e o motivo.

---

# ADR-001 — O usuário possui identidade global

## Status

Aceita

## Decisão

O usuário será representado por uma identidade global no sistema.

O usuário não será definido permanentemente como síndico, morador, proprietário ou inquilino.

Esses papéis dependerão do contexto de cada condomínio ou unidade.

## Motivo

Uma mesma pessoa pode:

* administrar vários condomínios;
* morar em um condomínio e administrar outro;
* ser proprietária de uma unidade e moradora de outra;
* possuir mais de um papel no mesmo condomínio.

Definir um papel fixo diretamente no usuário impediria esses cenários.

## Consequências

* a autenticação será global;
* os papéis serão definidos por condomínio;
* os vínculos com unidades serão independentes;
* a autorização deverá sempre considerar o condomínio atual.

---

# ADR-002 — Papéis pertencem ao vínculo com o condomínio

## Status

Aceita

## Decisão

Os papéis do usuário serão associados a `CondominiumMembership`.

Uma participação poderá possuir vários registros em `CondominiumMembershipRole`.

## Motivo

Um usuário pode possuir mais de um papel no mesmo condomínio.

Exemplo:

```text
Manager
Resident
```

Também é necessário permitir que os papéis mudem sem alterar a identidade global do usuário.

## Consequências

* os papéis não serão tratados como roles globais do ASP.NET Core Identity;
* a autorização dependerá do vínculo ativo com o condomínio;
* um mesmo usuário poderá possuir papéis diferentes em condomínios diferentes;
* papéis personalizados ficam fora do MVP.

---

# ADR-003 — Vínculos com unidades são independentes dos papéis no condomínio

## Status

Aceita

## Decisão

A relação entre usuário e unidade será representada por `UnitMembership`.

Ela será independente de `CondominiumMembershipRole`.

## Motivo

Ser morador, proprietário ou inquilino de uma unidade não é a mesma coisa que possuir uma função administrativa dentro do condomínio.

Um síndico profissional pode administrar um condomínio sem possuir nenhuma unidade.

## Consequências

* um usuário pode possuir vínculo com uma unidade sem ser gestor;
* um gestor pode não possuir vínculo com unidade;
* a aplicação deverá validar separadamente papéis administrativos e vínculos residenciais;
* um usuário poderá possuir vínculos com várias unidades.

---

# ADR-004 — Um usuário pode possuir vários papéis no mesmo condomínio

## Status

Aceita

## Decisão

Os papéis não serão armazenados em um único campo dentro de `CondominiumMembership`.

Será utilizada uma coleção de papéis.

## Motivo

Uma pessoa pode ser simultaneamente síndica e moradora.

Um único campo obrigaria a escolher apenas um papel ou criar combinações artificiais.

## Consequências

* será criada a entidade `CondominiumMembershipRole`;
* deverá existir restrição contra duplicidade do mesmo papel;
* a aplicação deverá considerar todos os papéis ativos do vínculo;
* os papéis do MVP serão definidos por enum.

---

# ADR-005 — Categorias pertencem ao condomínio

## Status

Aceita

## Decisão

Cada condomínio poderá criar e administrar suas próprias categorias de solicitações.

Não haverá uma lista global fixa de categorias.

## Motivo

Condomínios possuem realidades, estruturas e processos diferentes.

Uma lista global obrigatória poderia não representar corretamente todos os clientes.

## Consequências

* `Category` possuirá `CondominiumId`;
* o nome deverá ser único dentro do condomínio;
* categorias poderão ser desativadas;
* solicitações antigas continuarão vinculadas a categorias inativas;
* categorias globais ou modelos pré-definidos poderão ser avaliados futuramente.

---

# ADR-006 — Request representa uma demanda

## Status

Aceita

## Decisão

`Request` representará exclusivamente uma demanda registrada no condomínio.

Ela não representará uma conversa e não representará uma tarefa interna.

## Motivo

Esses conceitos possuem responsabilidades e ciclos de vida diferentes.

Misturá-los tornaria a entidade difícil de evoluir e aumentaria o acoplamento.

## Consequências

* mensagens ficarão em `RequestMessage`;
* alterações de status ficarão em `RequestStatusHistory`;
* tarefas internas ficarão em `Task`;
* a interface poderá combinar esses dados em uma única linha do tempo.

---

# ADR-007 — Autor e unidade-alvo são conceitos diferentes

## Status

Aceita

## Decisão

A solicitação armazenará separadamente:

```text
AuthorUserId
TargetUnitId
```

A unidade-alvo será opcional.

## Motivo

Quem abre a solicitação nem sempre é a pessoa vinculada à unidade envolvida.

Exemplo:

```text
Morador da unidade 305
abre uma solicitação sobre a unidade 804.
```

Também existem solicitações sem unidade específica.

Exemplos:

* portão;
* elevador;
* iluminação;
* área comum.

## Consequências

* o autor não precisa possuir vínculo com a unidade-alvo;
* a unidade-alvo deverá pertencer ao mesmo condomínio;
* solicitações poderão existir sem unidade-alvo;
* outros tipos de alvo poderão ser adicionados futuramente.

---

# ADR-008 — O síndico controla o ciclo de vida da solicitação

## Status

Aceita

## Decisão

No MVP, apenas usuários com papel de gestão poderão alterar o status e a prioridade das solicitações.

## Motivo

O síndico é responsável por avaliar o atendimento e decidir:

* quando começou o tratamento;
* quando faltam informações;
* quando depende de terceiros;
* quando a demanda foi resolvida;
* quando deve ser cancelada ou reaberta.

## Consequências

* moradores não alterarão diretamente o status;
* o backend deverá validar o papel do usuário;
* a confirmação do morador não será obrigatória para resolver;
* o síndico poderá reabrir solicitações resolvidas.

---

# ADR-009 — Status de espera serão explícitos

## Status

Aceita

## Decisão

O MVP possuirá os estados:

```text
WaitingForResident
WaitingForThirdParty
```

## Motivo

Nem toda solicitação aberta depende de atuação imediata do síndico.

É necessário distinguir:

* solicitações que aguardam o morador;
* solicitações que aguardam terceiros;
* solicitações em tratamento ativo.

## Consequências

* o dashboard poderá separar essas situações;
* o síndico visualizará com mais clareza o que depende dele;
* não será necessário cadastrar o terceiro responsável no MVP;
* automações de prazo poderão ser adicionadas futuramente.

---

# ADR-010 — Resolved será o encerramento operacional

## Status

Aceita

## Decisão

O status `Resolved` representará a conclusão operacional da solicitação.

Não haverá um status adicional `Closed` no MVP.

## Motivo

Na rotina atual, o síndico frequentemente consegue encerrar demandas simples sem precisar de confirmação do morador.

Um estado adicional aumentaria o número de etapas sem gerar benefício suficiente.

## Consequências

* o síndico decide quando resolver;
* a confirmação do morador será opcional;
* uma solicitação resolvida poderá voltar para `InProgress`;
* um estado `Closed` poderá ser considerado futuramente caso exista confirmação formal.

---

# ADR-011 — Solicitações canceladas não serão reabertas no MVP

## Status

Aceita

## Decisão

`Cancelled` será um estado final.

Uma solicitação cancelada não poderá ser reaberta no MVP.

## Motivo

O cancelamento representa que a demanda era inválida, duplicada, aberta por engano ou não deveria continuar.

Reabrir esses registros poderia confundir o histórico.

## Consequências

* uma nova demanda deverá gerar uma nova solicitação;
* o histórico da solicitação cancelada será preservado;
* a regra poderá ser revisada futuramente se o uso real indicar necessidade.

---

# ADR-012 — Toda mudança de status gera histórico

## Status

Aceita

## Decisão

Toda alteração de status de uma solicitação criará um registro em `RequestStatusHistory`.

A criação da solicitação também deverá gerar o primeiro registro.

## Motivo

O status atual não explica como a solicitação chegou até aquele estado.

O histórico é necessário para rastreabilidade, auditoria e compreensão da linha do tempo.

## Consequências

* o histórico não poderá ser editado ou apagado no MVP;
* cada registro armazenará status anterior, novo status, usuário e data;
* a criação será representada por `PreviousStatus = null`;
* a interface poderá exibir o histórico junto às mensagens.

---

# ADR-013 — Mensagens e histórico de status serão separados

## Status

Aceita

## Decisão

Mensagens serão armazenadas em `RequestMessage`.

Alterações de status serão armazenadas em `RequestStatusHistory`.

## Motivo

Uma mensagem é conteúdo de conversa.

Uma mudança de status é um evento estruturado do domínio.

Misturar os dois reduziria a capacidade de consultar, filtrar e validar o histórico.

## Consequências

* mudanças de status não dependerão de mensagens;
* a interface poderá combinar ambos em uma linha do tempo;
* mensagens automáticas de sistema ficam fora do MVP;
* mensagens não poderão ser editadas ou apagadas inicialmente.

---

# ADR-014 — Task é independente de Request

## Status

Aceita

## Decisão

`Task` representará uma atividade interna de gestão e possuirá ciclo de vida próprio.

Ela poderá ser criada a partir de uma solicitação ou independentemente.

## Motivo

Uma tarefa não é uma demanda do morador.

Exemplos:

```text
Comprar lâmpadas
Solicitar orçamento
Conferir gravações
Agendar manutenção
```

Uma única solicitação também pode gerar várias tarefas.

## Consequências

* `SourceRequestId` será opcional;
* uma solicitação poderá originar várias tarefas;
* concluir uma tarefa não resolverá automaticamente a solicitação;
* resolver uma solicitação não concluirá automaticamente as tarefas;
* comentários, subtarefas e histórico de tarefas ficam fora do MVP.

---

# ADR-015 — O condomínio não terá endereço no MVP

## Status

Aceita

## Decisão

A entidade `Condominium` não armazenará endereço no MVP.

## Motivo

O endereço não participa atualmente de nenhuma regra de negócio ou funcionalidade necessária.

Armazená-lo seria apenas um cadastro sem uso concreto.

## Consequências

* a entidade permanecerá mais simples;
* endereço poderá ser adicionado futuramente;
* nenhuma abstração de endereço será criada agora;
* funcionalidades que dependam de localização ficam fora do MVP.

---

# ADR-016 — Arquivos não serão armazenados no PostgreSQL

## Status

Aceita

## Decisão

O banco armazenará apenas os metadados dos anexos.

O conteúdo físico ficará em um serviço de armazenamento.

## Motivo

Arquivos binários aumentariam o tamanho do banco e dificultariam backup, distribuição e evolução da infraestrutura.

## Consequências

* `RequestAttachment` armazenará `StorageKey`;
* o desenvolvimento poderá usar armazenamento local;
* produção poderá utilizar armazenamento de objetos;
* o acesso aos arquivos deverá respeitar autorização;
* uma abstração como `IFileStorage` poderá ser criada quando necessária.

---

# ADR-017 — O sistema será multi-condomínio por separação lógica

## Status

Aceita

## Decisão

Todos os condomínios utilizarão inicialmente o mesmo banco de dados.

A separação será feita por `CondominiumId`.

## Motivo

Banco ou schema separado por cliente aumentaria a complexidade operacional sem necessidade para o MVP.

## Consequências

* consultas deverão sempre considerar o condomínio;
* a autorização deverá validar o vínculo do usuário;
* dados de condomínios diferentes não poderão ser expostos entre si;
* índices deverão considerar filtros por condomínio;
* isolamento físico poderá ser avaliado futuramente.

---

# ADR-018 — O condomínio ativo será informado nas rotas

## Status

Aceita

## Decisão

O contexto do condomínio será inicialmente informado nas rotas da API.

Exemplo:

```text
/api/condominiums/{condominiumId}/requests
```

## Motivo

Usuários podem participar de vários condomínios.

O contexto explícito facilita leitura, validação, logs e testes.

## Consequências

* o frontend deverá informar o condomínio atual;
* o backend deverá validar o vínculo em cada operação;
* o identificador enviado pelo frontend não será considerado confiável sozinho;
* outras abordagens poderão ser avaliadas futuramente.

---

# ADR-019 — O Domain não dependerá do Entity Framework

## Status

Aceita

## Decisão

O projeto `CondoLink.Domain` não possuirá dependência do Entity Framework Core.

As configurações de persistência ficarão em `Infrastructure`.

## Motivo

O domínio deve representar regras de negócio sem conhecer detalhes de banco de dados.

## Consequências

* configurações serão feitas com Fluent API;
* atributos de persistência serão evitados nas entidades;
* o domínio poderá ser testado sem banco;
* alterações de infraestrutura terão menor impacto nas regras de negócio.

---

# ADR-020 — Não será utilizado repositório genérico por padrão

## Status

Aceita

## Decisão

Não será criada uma abstração obrigatória como:

```text
IGenericRepository<TEntity>
```

## Motivo

O Entity Framework Core já oferece abstrações para operações de persistência.

Um repositório genérico que apenas replique `Add`, `Update`, `Delete` e `GetById` adicionaria código sem expressar o domínio.

## Consequências

* a camada de aplicação poderá utilizar `IApplicationDbContext`;
* interfaces específicas poderão ser criadas quando houver necessidade real;
* consultas de domínio poderão possuir abstrações próprias;
* a decisão pode ser revisada conforme a aplicação crescer.

---

# ADR-021 — CQRS, MediatR e eventos de domínio não serão obrigatórios no MVP

## Status

Aceita

## Decisão

O MVP não exigirá CQRS completo, MediatR ou eventos de domínio.

Os casos de uso poderão ser implementados com classes simples.

## Motivo

Esses padrões podem ser úteis, mas também adicionam abstrações, arquivos e indireções.

O projeto ainda não possui complexidade suficiente para justificar sua adoção obrigatória.

## Consequências

* casos de uso serão explícitos;
* o fluxo será mais simples de acompanhar;
* padrões adicionais poderão ser adotados quando houver benefício concreto;
* notificações e integrações futuras poderão motivar eventos de domínio.

---

# ADR-022 — Datas técnicas serão armazenadas em UTC

## Status

Aceita

## Decisão

Datas e horários técnicos serão armazenados em UTC.

## Motivo

O armazenamento em UTC reduz ambiguidades e facilita evolução para usuários em diferentes localidades.

## Consequências

* a apresentação será responsável pela conversão para o horário local;
* `DueDate` poderá usar apenas data quando não existir horário;
* testes deverão controlar o relógio quando necessário;
* uma abstração de tempo poderá ser criada futuramente.

---

# ADR-023 — Desativação será preferida à exclusão física

## Status

Aceita

## Decisão

Entidades com uso histórico deverão ser desativadas em vez de removidas fisicamente.

## Motivo

A exclusão poderia quebrar o histórico de solicitações, mensagens, tarefas e vínculos.

## Consequências

* entidades principais possuirão indicadores como `IsActive`;
* mensagens e históricos não poderão ser apagados no MVP;
* consultas operacionais deverão ignorar registros inativos quando apropriado;
* políticas de retenção poderão ser definidas futuramente.

---

# ADR-024 — Templates e fluxos guiados ficam fora do MVP

## Status

Aceita

## Decisão

Templates de resposta, checklists e fluxos guiados por categoria não serão implementados inicialmente.

## Motivo

Esses recursos possuem alto potencial de valor, mas exigem modelagem adicional.

O primeiro objetivo é validar o núcleo de solicitações.

## Consequências

* o atendimento de encomendas será tratado inicialmente como uma solicitação comum;
* a funcionalidade será registrada no backlog;
* o domínio atual não será acoplado a formulários dinâmicos;
* a evolução poderá ocorrer com base no uso real.

---

# ADR-025 — O produto será construído como monólito modular

## Status

Aceita

## Decisão

O CondoLink será implementado inicialmente como uma única aplicação backend organizada em projetos e camadas.

Não serão utilizados microsserviços no MVP.

## Motivo

O sistema ainda está no início e não possui escala, equipes ou necessidades operacionais que justifiquem distribuição.

Um monólito modular é mais simples para desenvolver, testar, implantar e manter.

## Consequências

* uma única API atenderá o produto;
* módulos serão separados logicamente pela solução;
* transações permanecerão simples;
* extrações futuras poderão ocorrer caso exista necessidade concreta.

---

# Como adicionar novas decisões

Novas decisões devem seguir o formato:

```text
ADR-XXX — Título

Status

Decisão

Motivo

Consequências
```

Status possíveis:

```text
Proposta
Aceita
Substituída
Rejeitada
Obsoleta
```

Quando uma decisão for substituída, o documento deve indicar qual ADR passou a valer.
