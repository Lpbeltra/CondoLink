# CondoLink — Architecture

## Cardinalidade de SubManager (atualização)

Um condomínio pode ter 0..N SubManagers ativos. Cada usuário pode ter no máximo um vínculo SubManager ativo nesta versão. Um mesmo usuário pode acumular vínculo de morador/unidade e vínculo de SubManager; o vínculo de morador permanece inalterado e a promoção cria novo `CondominiumMembership`. Permissões permanecem vinculadas ao `CondominiumMembershipId`.

Mensagens do atendimento entre Manager/SubManager e Morador aceitam até 3000 caracteres; o limite também vale para respostas do morador e confirmações/questionamentos de conclusão.

## Objetivo

## Agenda operacional

A Agenda é escopada por `CondominiumId`, não pelo usuário criador. `AgendaReminder`
registra `CreatedByUserId` apenas para auditoria e pode referenciar unidade do
mesmo condomínio e terceiro textual. `AgendaReminderRequest` possui índice único
em `RequestId`; uma Request ativa pertence a no máximo um lembrete. Transições
para `Resolved` ou `Cancelled` removem o vínculo transacionalmente sem excluir o
lembrete.

Cada vencimento gera `AgendaReminderOccurrence`, única por lembrete e horário.
O worker trabalha em lotes, avança a recorrência ao reivindicar a ocorrência e
registra e-mail e WhatsApp separadamente. O destinatário precisa ser o único
Manager contextual ativo do condomínio no disparo; criador e PlatformAdmin não
são fallback.

O horário é o momento de avisar, nunca um prazo de conclusão. Depois de uma
ocorrência avulsa, o lembrete continua ativo, sem próxima ocorrência, e seu estado
“avisado, pendente de conclusão” é derivado da ocorrência e de `CompletedAt`.
Somente a ação explícita de conclusão define `IsActive=false` e `CompletedAt`.

Datas são UTC e cada lembrete preserva o fuso IANA operacional. O padrão
configurável é `America/Sao_Paulo`. A recorrência semanal preserva a referência
local; a mensal preserva o dia original, usa o último dia válido em meses curtos
e volta ao dia original nos meses seguintes. Edição recalcula somente o futuro e
não remove ocorrências históricas.

O formulário obtém unidades e Requests elegíveis em uma única operação escopada
ao condomínio. Requests terminais e vínculos pertencentes a outro lembrete não
são oferecidos; ao editar, os vínculos do próprio lembrete permanecem disponíveis.
A seleção é um checklist pesquisável. O fluxo iniciado no detalhe da Request usa
`/management/agenda?create=true&requestId=...`, preserva o contexto após refresh e
mantém a Request de origem fixa até a criação.

Conclusão manual define `IsActive=false`, preenche `CompletedAt` e remove apenas a
próxima ocorrência, preservando vínculos e entregas históricas. Reativação
recorrente calcula a próxima ocorrência estritamente futura segundo a referência
original. Um lembrete avulso vencido exige edição para uma data futura antes da
reativação, evitando disparo retroativo. O worker considera somente lembretes
ativos.

O protocolo humano exibido é o identificador legado já usado no WhatsApp: os oito
primeiros caracteres hexadecimais maiúsculos do `Request.Id`. Ele é estável e
imutável por derivação, mas ainda não possui constraint própria de unicidade no
banco; não foi criado um segundo identificador neste lote.

Este documento descreve a arquitetura inicial do CondoLink.

## Atualização administrativa sem transição

Managers podem publicar uma atualização em Requests `InProgress` ou
`WaitingForThirdParty` sem executar a máquina de estados. O conteúdo aprovado é
persistido como `RequestMessage` de canal `Portal`, aparece para administração e
morador na timeline e alimenta a atualização da análise. Não há
`RequestStatusHistory`, alteração de `ResolvedAt`, closure, requirement ou
prioridade.

Cada entrega usa `WhatsAppNotificationType.AdministrativeRequestUpdate` e a chave
`request-update:{RequestMessageId}:{ResidentUserId}`. Dentro da janela é
`SessionText`; fora dela usa `request_status_update`. O outbound persiste
`RequestMessageId`, permitindo que `request_status_view` entregue exatamente a
mensagem respondida sem procurar a última Request ou criar histórico artificial.
`WaitingForThirdParty` reutiliza sua moldura operacional; `InProgress` possui o
gatilho somente leitura/configurável `InProgressUpdate` porque não existia uma
moldura de envio para esse estado.

O objetivo é definir uma estrutura clara para o MVP, mantendo o projeto simples, testável e preparado para evolução.

A arquitetura deve apoiar o domínio do produto, sem adicionar abstrações ou camadas que não tragam benefício real.

---

# 1. Princípios arquiteturais

A arquitetura do CondoLink seguirá os seguintes princípios:

* o domínio deve representar as regras reais do produto;
* regras de negócio não devem depender diretamente de banco de dados ou interface;
* a API deve coordenar requisições, não concentrar toda a lógica;
* detalhes de infraestrutura devem permanecer isolados;
* o sistema deve ser preparado para múltiplos condomínios;
* implementar apenas o necessário para o MVP;
* evitar abstrações prematuras;
* priorizar clareza sobre complexidade;
* manter as dependências apontando para o domínio.

A regra principal continua sendo:

> Modelar para crescimento, mas implementar apenas o necessário para o MVP.

---

# 2. Stack

## Frontend

```text
React
TypeScript
Vite
Material UI
PWA
```

## Backend

```text
ASP.NET Core Web API
.NET 10
Entity Framework Core
ASP.NET Core Identity
```

## Banco de dados

```text
PostgreSQL
```

## Infraestrutura

```text
Docker
Docker Compose
```

---

# 3. Estrutura da solução

```text
CondoLink/

src/
    CondoLink.Api
    CondoLink.Application
    CondoLink.Domain
    CondoLink.Infrastructure

tests/
    CondoLink.Tests

docs/
    REQUIREMENTS.md
    DOMAIN.md
    ERD.md
    WORKFLOWS.md
    ARCHITECTURE.md
    DECISIONS.md
    BACKLOG.md

README.md
AGENTS.md
```

---

# 4. Dependências entre projetos

A direção das dependências deve ser:

```text
CondoLink.Api
    |
    v
CondoLink.Application
    |
    v
CondoLink.Domain

CondoLink.Api
    |
    v
CondoLink.Infrastructure
    |
    v
CondoLink.Application
    |
    v
CondoLink.Domain
```

De forma simplificada:

```text
Api → Application → Domain
Api → Infrastructure → Application → Domain
```

O projeto `Domain` não deve depender dos outros projetos.

O projeto `Application` pode depender do `Domain`.

O projeto `Infrastructure` pode depender de `Application` e `Domain`.

O projeto `Api` pode depender de `Application` e `Infrastructure`.

---

# 5. CondoLink.Domain

## Responsabilidade

O projeto `CondoLink.Domain` representa o núcleo do negócio.

Ele deve conter:

* entidades;
* enums;
* regras de domínio;
* comportamentos das entidades;
* exceções ou erros de domínio;
* value objects, quando houver necessidade real;
* serviços de domínio, somente quando uma regra não pertencer naturalmente a uma entidade.

## Exemplos de entidades

```text
Condominium
CondominiumMembership
CondominiumMembershipRole
Unit
UnitMembership
Category
Request
RequestMessage
RequestStatusHistory
RequestAttachment
Task
```

A representação de `User` dependerá da integração com ASP.NET Core Identity.

## O Domain não deve conhecer

* Entity Framework Core;
* PostgreSQL;
* controllers;
* endpoints;
* HTTP;
* autenticação JWT;
* armazenamento de arquivos;
* Material UI;
* React;
* Docker;
* detalhes de serialização.

## Regra principal

As entidades não devem ser apenas coleções de propriedades públicas.

Sempre que existir uma regra importante, a entidade deve proteger seu próprio estado.

Exemplos:

```text
Request.ChangeStatus(...)
Request.ChangePriority(...)
Request.Resolve(...)
Request.Reopen(...)
Task.Complete(...)
Task.Cancel(...)
```

Esses métodos devem impedir estados inválidos.

---

# 6. CondoLink.Application

## Responsabilidade

O projeto `CondoLink.Application` coordena os casos de uso do sistema.

Ele conecta:

* domínio;
* persistência;
* identidade;
* autorização;
* armazenamento de arquivos;
* serviços externos.

A camada de aplicação deve organizar o fluxo da operação, mas não substituir as regras do domínio.

## Exemplos de casos de uso

```text
CreateRequest
SendRequestMessage
ChangeRequestStatus
ChangeRequestPriority
CreateTask
CompleteTask
CreateCategory
AddUserToCondominium
AssignCondominiumRole
LinkUserToUnit
```

## Conteúdo esperado

```text
UseCases/
DTOs/
Interfaces/
Validation/
Common/
```

Essa estrutura poderá ser ajustada conforme o projeto evoluir.

## Interfaces possíveis

```text
IApplicationDbContext
ICurrentUser
IFileStorage
IDateTimeProvider
```

Somente devem ser criadas quando existir necessidade concreta.

Não será adotada a regra de criar uma interface para toda classe.

## Responsabilidades da camada

* validar a entrada do caso de uso;
* verificar permissões;
* carregar entidades necessárias;
* executar comportamento de domínio;
* persistir alterações;
* transformar resultados em DTOs;
* coordenar transações.

## O Application não deve conter

* detalhes de HTTP;
* controllers;
* configuração do Entity Framework;
* SQL específico;
* detalhes de PostgreSQL;
* implementação concreta de armazenamento;
* componentes do frontend.

---

# 7. CondoLink.Infrastructure

## Responsabilidade

O projeto `CondoLink.Infrastructure` implementa os detalhes técnicos necessários para executar a aplicação.

## Conteúdo esperado

```text
Persistence/
Identity/
Storage/
Configurations/
Migrations/
DependencyInjection/
```

## Responsabilidades

* configurar Entity Framework Core;
* implementar o contexto do banco;
* configurar entidades e relacionamentos;
* integrar PostgreSQL;
* implementar ASP.NET Core Identity;
* implementar armazenamento de anexos;
* fornecer serviços concretos exigidos pela camada de aplicação;
* registrar dependências de infraestrutura.

## Entity Framework Core

As configurações do Entity Framework devem ficar separadas das entidades.

Preferência:

```text
Infrastructure/
    Persistence/
        AppDbContext.cs
        Configurations/
            CondominiumConfiguration.cs
            RequestConfiguration.cs
            TaskConfiguration.cs
```

As entidades do domínio não devem possuir atributos de persistência quando isso puder ser evitado.

Exemplos de atributos que não devem ser necessários no domínio:

```text
[Table]
[Column]
[Key]
[ForeignKey]
[MaxLength]
```

As regras de persistência devem ser configuradas com Fluent API.

## Migrations

As migrations devem permanecer no projeto `Infrastructure`.

Elas somente serão criadas depois que:

* as entidades estiverem modeladas;
* os relacionamentos estiverem revisados;
* o ASP.NET Core Identity estiver integrado;
* o DbContext estiver configurado.

### Build e migrations no container

Configurações de runtime (connection string, JWT, e-mail, OpenAI, WhatsApp e
credenciais administrativas) são fornecidas somente pelo ambiente de execução;
não são necessárias como argumentos do Docker build. O Dockerfile mantém o
restore e a ferramenta `dotnet-ef` em camadas cacheáveis, limita o publish a um
nó do MSBuild em hosts pequenos e gera o bundle sem recompilar (`--no-build`).

Em produção, o `efbundle` continua sendo executado pelo entrypoint antes da
API iniciar. A connection string usada na geração é apenas de design-time e não
é conectada. No Coolify, mantenha secrets em Environment Variables de runtime
e remova-os de Build Arguments; `VITE_API_URL` permanece a única configuração
de build do frontend porque é incorporada ao bundle público.

---

# 8. CondoLink.Api

## Responsabilidade

O projeto `CondoLink.Api` é o ponto de entrada HTTP da aplicação.

Ele deve conter:

* controllers ou endpoints;
* configuração da aplicação;
* autenticação;
* autorização;
* middleware;
* tratamento global de erros;
* documentação da API;
* injeção de dependências;
* configuração de CORS.

## Controllers

Os controllers devem ser pequenos.

Sua responsabilidade deve ser:

1. receber a requisição;
2. validar a estrutura básica;
3. chamar o caso de uso;
4. retornar a resposta HTTP adequada.

Exemplo conceitual:

```csharp
[HttpPost]
public async Task<IActionResult> Create(
    CreateRequestDto request,
    CancellationToken cancellationToken)
{
    var result = await useCase.Execute(request, cancellationToken);

    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

Regras de negócio não devem ficar nos controllers.

## Organização inicial

```text
Controllers/
Middleware/
Authorization/
Extensions/
Program.cs
```

A estrutura poderá evoluir conforme a quantidade de funcionalidades.

---

# 9. Frontend

O frontend será uma aplicação separada, construída com:

```text
React
TypeScript
Vite
Material UI
```

## Responsabilidades

* autenticação do usuário;
* seleção do condomínio ativo;
* visualização de solicitações;
* criação de solicitações;
* envio de mensagens e anexos;
* administração de categorias;
* gestão de unidades e usuários;
* gestão de tarefas;
* apresentação do dashboard.

## Organização sugerida

```text
src/
    api/
    components/
    features/
    layouts/
    pages/
    routes/
    hooks/
    types/
    utils/
```

Uma estrutura orientada por funcionalidades pode ser utilizada:

```text
features/
    auth/
    condominiums/
    requests/
    tasks/
    units/
    categories/
```

Não é necessário criar um sistema de design próprio no MVP.

O Material UI será utilizado como base visual.

---

# 10. Autenticação e identidade

O CondoLink utilizará ASP.NET Core Identity.

O usuário possui uma identidade global.

Papéis como `Manager` e `Resident` não devem ser armazenados como papéis globais do Identity.

Esses papéis pertencem ao contexto de um condomínio e devem ser representados por:

```text
CondominiumMembership
CondominiumMembershipRole
```

`PlatformAdmin` é uma autorização global e pode coexistir no mesmo
`ApplicationUser` com um ou mais vínculos contextuais ativos de `Manager`.
Consultas administrativas de síndicos consideram o vínculo e o papel ativos no
condomínio independentemente do papel global; a policy `PlatformAdmin` continua
sendo exigida para acessar o Overwatch. Um mesmo usuário é listado uma única
vez, com a quantidade de condomínios calculada pelos vínculos ativos.

O detalhe administrativo de uma Request expõe um resumo atual do autor (nome,
telefone, e-mail, unidade, bloco quando aplicável e relação ativa com a
unidade-alvo). Esses valores são projetados das entidades de identidade e
vínculo e não são copiados para a Request. A unidade-alvo preserva a referência
histórica da Request; a relação exibida reflete o vínculo ativo atual e pode
estar ausente em cadastros legados.

## Identity

O ASP.NET Core Identity será responsável por:

* credenciais;
* hash de senha;
* login;
* recuperação de conta;
* bloqueio;
* tokens de autenticação;
* informações básicas da conta.

## Domínio

O domínio será responsável por:

* participação no condomínio;
* papéis dentro do condomínio;
* vínculos com unidades;
* permissões contextuais.

## Autorização

A autorização deve considerar:

```text
Usuário autenticado
+
Condomínio solicitado
+
Vínculo ativo
+
Papel necessário
```

Não basta verificar apenas se o usuário está autenticado.

---

# 11. Multi-condomínio

O CondoLink será um sistema multi-condomínio com separação lógica dos dados.

Não será utilizado inicialmente:

* um banco por condomínio;
* um schema por condomínio;
* infraestrutura isolada por cliente.

Todos os condomínios utilizarão o mesmo banco de dados.

A separação ocorrerá por identificadores como:

```text
CondominiumId
```

## Regras

* toda operação deve considerar o condomínio atual;
* o usuário deve possuir vínculo ativo com o condomínio;
* consultas devem ser filtradas pelo condomínio;
* entidades relacionadas devem pertencer ao mesmo condomínio;
* dados de um condomínio nunca devem ser expostos a usuários de outro.

## Segurança

O `CondominiumId` enviado pelo frontend não deve ser considerado confiável sozinho.

A aplicação deve validar que o usuário atual possui acesso ao condomínio informado.

---

# 12. Contexto do condomínio ativo

Usuários podem participar de vários condomínios.

Por isso, a aplicação precisa conhecer qual condomínio está sendo utilizado na operação atual.

Possíveis abordagens:

```text
rota;
header HTTP;
claim temporária;
seleção armazenada no frontend.
```

Para o MVP, a abordagem recomendada é incluir o condomínio na rota:

```text
/api/condominiums/{condominiumId}/requests
/api/condominiums/{condominiumId}/tasks
/api/condominiums/{condominiumId}/units
```

Vantagens:

* contexto explícito;
* URLs claras;
* fácil validação;
* fácil leitura de logs;
* menor dependência de estado oculto.

A aplicação deve validar o vínculo do usuário em cada operação.

---

# 13. Persistência

O banco de dados será PostgreSQL.

O acesso será realizado com Entity Framework Core.

## Convenções iniciais

* identificadores com `Guid`;
* datas e horários em UTC;
* nomes de propriedades em inglês;
* enums persistidos de forma consistente;
* índices para consultas frequentes;
* chaves estrangeiras explícitas;
* restrições únicas quando aplicável.

## Exemplos de restrições únicas

```text
CondominiumMembership:
UserId + CondominiumId

CondominiumMembershipRole:
CondominiumMembershipId + Role

Category:
CondominiumId + Name

Unit:
CondominiumId + Block + Identifier
```

A restrição de unidade exigirá atenção quando `Block` for nulo.

## Exclusão

O sistema deve priorizar desativação em vez de exclusão física para entidades com histórico.

Exemplos:

```text
User.IsActive
Condominium.IsActive
Unit.IsActive
Category.IsActive
CondominiumMembership.IsActive
```

Mensagens e históricos de status não devem ser apagados no MVP.

---

# 14. Datas e horários

Datas e horários técnicos devem ser armazenados em UTC.

Exemplos:

```text
CreatedAt
UpdatedAt
ResolvedAt
CompletedAt
GrantedAt
RevokedAt
```

A conversão para o horário local deve ocorrer na apresentação.

Para campos que representam apenas uma data, como prazo de uma tarefa, poderá ser utilizado um tipo sem horário:

```text
DueDate
```

A aplicação não deve depender diretamente de `DateTime.UtcNow` em vários pontos.

Caso a necessidade apareça, poderá ser criada uma abstração como:

```text
IDateTimeProvider
```

Essa abstração deve ser introduzida principalmente para facilitar testes.

---

# 15. Anexos

O detalhe do atendimento busca o conteúdo pelo endpoint autenticado e cria URLs
Blob locais. Imagens usam miniaturas; áudio, vídeo e PDF são carregados sob
demanda em players/modal. As URLs Blob são revogadas no fechamento ou desmontagem
e nenhum caminho público de armazenamento é exposto.

Os arquivos não serão armazenados diretamente no PostgreSQL.

O banco armazenará apenas metadados:

```text
OriginalFileName
StorageKey
ContentType
FileSize
CreatedAt
```

## Armazenamento

No desenvolvimento, poderá ser utilizado armazenamento local.

Em produção, a implementação poderá evoluir para um serviço de objetos compatível com:

```text
Amazon S3
Azure Blob Storage
Cloudflare R2
MinIO
```

A camada de aplicação poderá depender de uma abstração:

```text
IFileStorage
```

A implementação concreta ficará em `Infrastructure`.

## Regras de segurança

A implementação deverá validar:

* no máximo 6 arquivos por envio;
* no máximo 15 MB por arquivo;
* apenas JPG/JPEG, PNG, WebP e PDF, validando extensão e MIME;
* nome seguro;
* autorização para acessar o anexo;
* inexistência de exposição direta do caminho físico.

O upload usa `multipart/form-data`, com todos os arquivos no campo `files`.
O banco recebe os metadados somente depois de todo o lote ser validado; lotes
inválidos não são parcialmente persistidos. Upload, listagem, download e
exclusão reutilizam a autorização da solicitação (autor ou síndico ativo do
condomínio). A exclusão exige confirmação no cliente e remove o metadado e o
arquivo físico.

Rotas atuais:

```text
POST   /requests/{requestId}/attachments
GET    /requests/{requestId}/attachments
GET    /request-attachments/{attachmentId}/content
DELETE /request-attachments/{attachmentId}
```

---

# 16. API

A API seguirá princípios REST.

## Exemplos de rotas

```text
POST   /api/auth/login

GET    /api/condominiums
POST   /api/condominiums

GET    /api/condominiums/{condominiumId}/requests
POST   /api/condominiums/{condominiumId}/requests

GET    /api/condominiums/{condominiumId}/requests/{requestId}
POST   /api/condominiums/{condominiumId}/requests/{requestId}/messages
POST   /api/condominiums/{condominiumId}/requests/{requestId}/status

GET    /api/condominiums/{condominiumId}/tasks
POST   /api/condominiums/{condominiumId}/tasks
```

Essas rotas são sugestões iniciais e podem ser revisadas durante a implementação.

## Respostas de erro

A API deve utilizar respostas padronizadas.

Preferência:

```text
ProblemDetails
```

Exemplos de status:

```text
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity
500 Internal Server Error
```

Não é necessário utilizar todos imediatamente.

---

# 17. Validação

A validação deve ocorrer em mais de um nível.

## API

Responsável por:

* formato da requisição;
* campos obrigatórios;
* tipos inválidos;
* payload malformado.

## Application

Responsável por:

* existência de entidades relacionadas;
* acesso ao condomínio;
* autorização;
* consistência do caso de uso;
* vínculos entre entidades.

## Domain

Responsável por:

* invariantes;
* transições válidas;
* estados internos;
* comportamentos da entidade.

Exemplo:

```text
A API valida que NewStatus foi enviado.

A Application verifica se o usuário pode alterar a solicitação.

O Domain verifica se a transição de status é permitida.
```

---

# 18. Tratamento de erros

O sistema deve possuir tratamento centralizado de erros.

A API não deve repetir blocos de `try/catch` em todos os controllers.

Um middleware ou handler global deverá converter erros conhecidos em respostas HTTP adequadas.

Exemplos:

```text
ValidationException → 400 ou 422
NotFoundException → 404
ForbiddenException → 403
ConflictException → 409
DomainException → 422
```

Erros inesperados devem retornar uma mensagem genérica ao cliente.

Detalhes internos não devem ser expostos em produção.

---

# 19. Logs

A aplicação deverá utilizar logging estruturado.

Os logs devem ajudar a identificar:

* usuário;
* condomínio;
* operação;
* solicitação;
* tarefa;
* erro;
* duração da requisição.

Dados sensíveis não devem ser gravados nos logs.

Exemplos que não devem ser registrados:

* senhas;
* tokens;
* conteúdo completo de arquivos;
* informações privadas desnecessárias.

No MVP, o logging padrão do ASP.NET Core é suficiente.

Ferramentas adicionais poderão ser incluídas futuramente.

---

# 20. Testes

O projeto `CondoLink.Tests` será responsável pelos testes automatizados.

## Prioridades

Os primeiros testes devem focar em regras de domínio.

Exemplos:

* transições de status da solicitação;
* reabertura de solicitação resolvida;
* impossibilidade de reabrir solicitação cancelada;
* preenchimento de `ResolvedAt`;
* conclusão e reabertura de tarefas;
* regras de residência principal;
* ativação e encerramento de vínculos.

## Tipos de teste

### Testes de domínio

Testam entidades e regras sem banco de dados.

### Testes de aplicação

Testam casos de uso com dependências controladas.

### Testes de integração

Testam:

* API;
* Entity Framework;
* PostgreSQL;
* autenticação;
* autorização.

O MVP não precisa começar com cobertura total.

Os testes devem priorizar regras com maior risco de erro.

---

# 21. Transações

Cada caso de uso que modifica dados deve executar suas alterações de forma atômica quando necessário.

Exemplo:

Ao criar uma solicitação, devem ser persistidos juntos:

```text
Request
RequestStatusHistory inicial
Anexos iniciais, quando aplicável
```

Se parte da operação falhar, o banco não deve permanecer em estado inconsistente.

O Entity Framework poderá gerenciar a transação quando todas as alterações ocorrerem no mesmo `SaveChanges`.

Transações explícitas devem ser usadas apenas quando necessário.

---

# 22. Eventos de domínio

Eventos de domínio não serão utilizados inicialmente como requisito obrigatório.

Exemplos que poderiam futuramente gerar eventos:

```text
RequestCreated
RequestStatusChanged
RequestResolved
TaskCreatedFromRequest
```

No MVP, o fluxo poderá ser implementado diretamente nos casos de uso.

Eventos devem ser introduzidos apenas quando houver necessidade concreta, como:

* notificações;
* integrações;
* auditoria adicional;
* automações;
* processamento assíncrono.

---

# 23. CQRS e MediatR

O projeto não adotará CQRS completo como obrigação arquitetural no MVP.

Também não é obrigatório utilizar MediatR.

Os casos de uso podem ser implementados com classes simples e explícitas.

Exemplo:

```text
CreateRequestUseCase
ChangeRequestStatusUseCase
SendRequestMessageUseCase
```

Caso o número de funcionalidades cresça e a organização se beneficie, padrões adicionais poderão ser adotados posteriormente.

Não devemos introduzir bibliotecas apenas para seguir uma arquitetura de referência.

---

# 24. Repositórios

Não será criada obrigatoriamente uma classe de repositório genérico.

O Entity Framework já fornece abstrações para acesso a dados.

A camada de aplicação poderá utilizar:

```text
IApplicationDbContext
```

ou interfaces específicas quando houver benefício real.

Evitar:

```text
IGenericRepository<TEntity>
```

caso ele apenas replique operações como:

```text
Add
Update
Delete
GetById
```

Interfaces específicas podem ser criadas quando expressarem consultas relevantes do domínio.

---

# 25. Docker

O ambiente local deve ser executável com Docker Compose.

Serviços esperados:

```text
api
database
frontend
```

Durante o desenvolvimento, frontend e API também poderão ser executados diretamente na máquina.

## Objetivo

O Docker deve facilitar:

* configuração do ambiente;
* execução do PostgreSQL;
* padronização entre máquinas;
* futura implantação.

O Docker não deve tornar o desenvolvimento local desnecessariamente lento ou complexo.

---

# 26. Configuração

Configurações não devem ser gravadas diretamente no código.

Exemplos:

* connection string;
* segredo de autenticação;
* URLs;
* limites de upload;
* configurações de e-mail;
* credenciais de armazenamento.

Durante o desenvolvimento, poderão ser usados:

```text
appsettings.Development.json
User Secrets
variáveis de ambiente
.env para Docker Compose
```

Segredos reais não devem ser versionados no Git.

---

# 27. PWA

O frontend será preparado como Progressive Web App.

O objetivo inicial da PWA é permitir:

* instalação no dispositivo;
* ícone próprio;
* abertura em modo semelhante a aplicativo;
* experiência responsiva.

Funcionamento offline completo não faz parte do MVP.

Não deve ser criada uma estratégia complexa de sincronização offline neste momento.

---

# 28. Segurança

O sistema deve seguir princípios básicos de segurança:

* autenticação obrigatória nas áreas privadas;
* autorização por condomínio;
* validação de entrada;
* proteção contra acesso entre condomínios;
* armazenamento seguro de senhas via Identity;
* uso de HTTPS em produção;
* limitação de tipos e tamanhos de anexos;
* mensagens de erro sem detalhes internos;
* segredos fora do repositório.

O frontend não deve ser considerado uma barreira de segurança.

Toda autorização deve ser aplicada também no backend.

---

# 29. Performance

O MVP não exige otimizações avançadas.

Entretanto, algumas práticas devem ser adotadas desde o início:

* consultas filtradas pelo condomínio;
* paginação em listas;
* índices em chaves estrangeiras;
* índices em campos usados em filtros;
* evitar carregar relacionamentos desnecessários;
* utilizar projeções para DTOs;
* utilizar `AsNoTracking` em consultas somente leitura;
* evitar problemas de N+1.

O sistema não deve carregar todo o histórico de um condomínio em uma única requisição.

---

# 30. Dashboard

O dashboard deverá utilizar consultas específicas para indicadores.

Exemplos:

```text
solicitações abertas;
solicitações aguardando morador;
solicitações aguardando terceiros;
solicitações resolvidas;
tarefas pendentes;
tarefas vencidas.
```

Não é necessário criar uma entidade `Dashboard`.

O dashboard é uma projeção de dados existentes.

As consultas devem sempre respeitar o condomínio atual.

---

# 31. Decisões fora do escopo atual

Ainda não precisam ser definidas:

* provedor de hospedagem;
* serviço de armazenamento definitivo;
* serviço de e-mail;
* serviço de push notification;
* filas;
* mensageria;
* observabilidade avançada;
* cache distribuído;
* Kubernetes;
* microsserviços;
* banco por condomínio;
* arquitetura orientada a eventos;
* inteligência artificial.

Essas decisões devem ser tomadas apenas quando o produto exigir.

---

# 32. Evolução arquitetural

A arquitetura inicial deve permitir crescimento sem tentar prever todos os recursos futuros.

Possíveis evoluções:

* notificações em tempo real;
* templates de resposta;

### Correlação operacional do WhatsApp fora da janela Meta

`Request` e `WhatsAppSession` continuam agregados distintos. A fila
`WhatsAppOutboundMessage` guarda `RequestId`, `RequestStatusHistoryId` e, quando
aplicável, `RequestClosureConfirmationId`, além do ID externo retornado pela
Meta. O webhook usa o `context.id` da resposta para localizar esse outbound;
não seleciona a última Request nem uma confirmação global.

Dentro de 24 horas o conteúdo composto pelo Overwatch é enviado como
`SessionText`. Fora da janela, atualização genérica usa
`request_status_update`, closure usa `resident_closure_confirmation`, pedido de
resposta mantém `resident_reply_required` e resolução unilateral usa
`task_finalization_notification`. O clique genérico reabre a janela e reproduz
o `Content` persistido, que contém a moldura vigente no momento da aprovação e
o texto administrativo literal. Retry técnico reutiliza o mesmo outbound;
transições usam updates condicionais para tolerar webhook ou clique duplicado.

O contrato posicional de `task_finalization_notification · pt_BR` é
`[primeiroNome, request.Title, "FINALIZADA", conclusãoLiteral]`. `FINALIZADA`
é estrutural e não passa por IA. O botão “Portal Comvy” é URL estática no
template Meta; o worker não envia componente nem parâmetro de URL para ele.
* fluxos guiados;
* base de conhecimento;
* integrações com administradoras;
* integrações com portarias;
* armazenamento em nuvem;
* processamento assíncrono;
* filas;
* eventos de domínio;
* auditoria avançada;
* inteligência artificial.

Esses recursos não devem influenciar excessivamente a implementação do MVP.

---

# 33. Resumo das decisões

1. A solução será dividida em `Api`, `Application`, `Domain` e `Infrastructure`.

2. O domínio não dependerá de Entity Framework ou ASP.NET Core.

3. A API será responsável apenas pela entrada e saída HTTP.

4. A camada de aplicação coordenará os casos de uso.

5. A infraestrutura implementará persistência, identidade e armazenamento.

6. O Entity Framework será configurado por Fluent API.

7. O sistema usará PostgreSQL.

8. O usuário terá identidade global.

9. Papéis serão definidos no contexto do condomínio.

10. A aplicação será multi-condomínio por separação lógica.

11. O condomínio atual será explicitado inicialmente nas rotas.

12. Toda autorização será validada no backend.

13. O sistema utilizará ASP.NET Core Identity.

14. Arquivos não serão armazenados diretamente no banco.

15. Datas e horários técnicos serão armazenados em UTC.

16. O frontend será React, TypeScript, Vite, Material UI e PWA.

17. Docker Compose será utilizado para execução do ambiente.

18. Repositório genérico, CQRS completo, MediatR e eventos de domínio não serão obrigatórios no MVP.

19. Testes priorizarão regras de domínio e autorização entre condomínios.

20. A arquitetura deverá permanecer simples, explícita e pragmática.
## Sessão de autenticação no frontend

O token JWT é persistido em `localStorage` com a chave
`condolink.accessToken`. O `AuthProvider` restaura a sessão lendo esse token,
configurando o cabeçalho `Authorization` e consultando `/users/me`. Não há
persistência separada do usuário, cookie de autenticação ou listener do evento
`storage`; login e logout alteram o armazenamento compartilhado pela mesma
origem, e as outras abas observam a conta mais recente ao serem recarregadas.

Para testar usuários simultâneos, utilizar perfis de navegador, janela anônima
ou navegadores diferentes.

## Ampliação cadastral do Lote 2

- O contato pessoal de funcionários e síndicos é persistido em
  `ApplicationUser.PhoneNumber`; a função do funcionário pertence ao vínculo
  `ManagementCompanyEmployee.JobTitle`.
- CPF, CNPJ e endereço do síndico são dados globais de `ApplicationUser` e não
  são repetidos em vínculos com condomínios.
- CPF/CNPJ são normalizados para somente dígitos e validados pelos dígitos
  verificadores. UF é persistida em maiúsculas e limitada às siglas brasileiras.
- A migration `ExpandRegistrationLot2` renomeia o documento da administradora
  para CNPJ, remove Razão Social e remove o telefone antigo do condomínio.
  A remoção de Razão Social descarta os dados históricos dessa coluna.
- CNPJ/endereço/cidade/UF novos permanecem nullable no banco para não inventar
  dados de registros históricos. Cadastro e edição completos exigem esses campos.
- A coluna `management_companies.cnpj` mantém capacidade de 20 caracteres para
  preservar documentos históricos não normalizáveis. Novos cadastros e edições
  continuam aceitando somente CNPJ válido, normalizado para 14 dígitos.
- Índices únicos filtrados protegem CNPJ de administradora e condomínio e
  CPF/CNPJ de síndico, permitindo múltiplos valores nulos históricos.

## Síndico único por condomínio — Lote 3

- Um condomínio possui no máximo um síndico ativo; um mesmo síndico pode
  administrar vários condomínios. Subsíndicos não fazem parte deste lote.
- Um vínculo de síndico é considerado ativo quando a `CondominiumMembership`
  está ativa e sem `EndedAt`, a `CondominiumMembershipRole` de tipo `Manager`
  está ativa e sem `RevokedAt`, e o `ApplicationUser` está ativo.
- A role condominial continua sendo uma entidade dependente separada. Não foi
  criada coluna redundante nem constraint que represente apenas parte da regra.
- Vínculo, reativação, troca e desvinculação são serializados por condomínio.
  Em PostgreSQL, uma transação adquire `pg_advisory_xact_lock` derivado do ID do
  condomínio; a validação é repetida depois do lock. Um lock local equivalente
  mantém o comportamento determinístico nos testes SQLite.
- A troca usa `PUT /overwatch/condominiums/{condominiumId}/manager` e revoga a
  role `Manager` anterior e ativa/cria a nova dentro da mesma transação. A
  operação pelo mesmo síndico é idempotente.
- `GET /overwatch/condominiums/{id}/manager` retorna um objeto resumido ou
  `null`. O endpoint plural anterior permanece temporariamente para
  compatibilidade, mas retorna no máximo um item; o frontend utiliza o singular.
- Desvincular revoga somente a role `Manager`. Membership, usuário,
  `UnitMemberships`, outros papéis e vínculos com outros condomínios são
  preservados. `ActiveManagementCondominiumId` é limpo quando aponta para o
  condomínio removido ou trocado.
- Inativar um síndico preserva seus vínculos, mas ele deixa de ser considerado
  síndico ativo e seu contexto administrativo é limpo. Sua reativação é
  bloqueada com conflito caso algum condomínio preservado já possua outro
  síndico ativo.
- Não há migration no Lote 3: o modelo relacional existente não permite um
  índice parcial correto envolvendo simultaneamente membership, role e usuário,
  e a auditoria do banco local não encontrou conflitos históricos.

## Reconciliação do contexto administrativo

- A fonte de verdade do contexto de Gestão é `GET /management/context`, baseada
  somente em membership ativa, role `Manager` ativa, usuário ativo e condomínio
  ativo. O contexto residencial continua separado.
- Com zero condomínios disponíveis, `ActiveManagementCondominiumId` é limpo.
  Com exatamente um, esse condomínio é selecionado e persistido
  automaticamente. Com vários, uma seleção válida é preservada; sem seleção
  válida, o escopo consolidado é usado sem escolher o primeiro item.
- `UsesConsolidatedManagementScope` é derivado da lista atual e do ID ativo:
  ele é verdadeiro quando existem vários condomínios e nenhum condomínio
  específico está selecionado. Não foi criada coluna adicional.
- `PUT /management/context` aceita um condomínio autorizado ou `null`. Para um
  único condomínio, `null` é reconciliado novamente para esse condomínio; para
  vários, `null` seleciona “Todos os condomínios”.
- Vínculo, troca, desvinculação, inativação de síndico e mudança de status do
  condomínio reconciliam o contexto persistido. Referências removidas ou
  inativas não permanecem em `ActiveManagementCondominiumId`.
- `GET /management/requests` aceita `condominiumId` opcional. Sem o parâmetro,
  retorna o consolidado autorizado e mantém o nome do condomínio em cada item;
  com o parâmetro, valida o acesso e restringe a consulta ao condomínio.
- Home, navegação e páginas de Gestão usam o mesmo
  `ManagementContextProvider`. O frontend não persiste nome ou ID administrativo
  em `localStorage` e descarta respostas de contexto que perderam a corrida para
  uma seleção mais recente.

## Primeiro acesso e ciclo de senha

- `ApplicationUser` armazena `MustChangePassword`, `LastLoginAt` e
  `PasswordChangedAt`. Contas criadas pelos fluxos de onboarding recebem uma
  senha temporária e ficam com troca obrigatória.
- Um login com senha temporária válida retorna
  `requiresPasswordChange = true`, sem emitir JWT. O frontend encaminha o
  usuário para `/change-password`.
- `POST /auth/change-temporary-password` permanece anônimo porque o usuário
  ainda não possui token, mas valida e consome a credencial temporária no
  backend. A troca limpa `MustChangePassword` e atualiza `PasswordChangedAt`.
- A validação do JWT consulta o estado atual do usuário e rejeita contas
  inativas ou com troca pendente. Assim, redefinir uma senha também bloqueia
  imediatamente tokens emitidos anteriormente.
- Gestores podem redefinir a senha somente de membros do condomínio que
  administram. Platform Admin possui acesso global; moradores não possuem essa
  ação. A nova senha temporária é exibida uma única vez pela mesma experiência
  usada no onboarding.
- Recuperação por e-mail ou WhatsApp não faz parte do MVP atual.

## Configuração inicial do condomínio

- O modelo atual já representa condomínios sem unidades, blocos opcionais,
  identificadores e andares textuais e descrições opcionais. O módulo de
  configuração não adiciona estado redundante e não exige migration.
- Gestores acessam somente condomínios nos quais possuem membership e papel
  `Manager` ativos. Platform Admin pode configurar qualquer condomínio ativo;
  moradores não possuem acesso.
- Importação e gerador convergem para o mesmo `SetupRequest`. A prévia valida o
  lote completo contra os dados atuais, sem persistência. A confirmação repete
  a validação e grava blocos, unidades, usuários e vínculos em uma única
  transação.
- A importação aceita CSV e XLSX, preserva células textuais e informa linha,
  coluna e motivo de cada erro. Os modelos CSV de estrutura e moradores são
  fornecidos pela própria API.
- O gerador usa torres com segmentos independentes por faixa de andares,
  quantidade por andar, número inicial, dígitos, inclusão opcional do andar,
  prefixo e sufixo. Isso evita pressupor uma topologia ou numeração específica.
- Usuários são localizados por e-mail. Contas existentes ativas são
  reutilizadas; contas novas recebem senha temporária e seguem o fluxo
  obrigatório de primeiro acesso. As credenciais novas são retornadas somente
  na confirmação.

## Tratamento de erro, resiliência HTTP, paginação e streaming do assistente

- **Erro centralizado**: `CondoLink.Api.Common` define `AppException` e os
  subtipos `NotFoundAppException`, `ForbiddenAppException`,
  `ConflictAppException`, `UnauthorizedAppException` e `ValidationAppException`.
  `AppExceptionHandler` (`IExceptionHandler`) converte exceções não tratadas em
  `ProblemDetails` padronizado (400/401/403/404/409/500), ocultando
  `exception.Message` fora de `Development` para erros 500. Registrado em
  `Program.cs` via `AddExceptionHandler`/`UseExceptionHandler`, posicionado
  depois do middleware de telemetria existente (que continua medindo/logando
  toda requisição, agora sem depender de a exceção "estourar" para calcular o
  status). A migração é incremental: os ~470 `Results.BadRequest/NotFound/...`
  já existentes não foram reescritos; código novo ou tocado por outro motivo
  deve preferir lançar as exceções do `Common`.
- **Resiliência HTTP para IA**: `CondoLink.Api.Common.OpenAiResilience`
  adiciona retry (2 tentativas, backoff exponencial com jitter) e circuit
  breaker aos 7 `HttpClient` tipados usados pelos serviços de IA
  (embeddings, assistente, rascunho de solicitação, extração/consulta/mutação
  de moradores administrativos, resposta a morador, transcrição de áudio),
  via `Microsoft.Extensions.Http.Resilience`. Deliberadamente sem `AddTimeout`
  na política — o timeout de negócio de cada serviço (`CancellationTokenSource`
  configurado a partir de `RequestDraftAiOptions`/`RequestDraftAiAudioOptions`)
  continua sendo a única fonte de verdade do prazo total. Não aplicado ao
  `MetaWhatsAppClient` (fora de escopo; já tem timeout e lógica de
  transiência próprios).
- **Paginação em `ListCondominiumRequests`**: `CondoLink.Api.Common.PagedResult`
  guarda a lógica de normalização de `page`/`pageSize` (`page` mínimo 1,
  `pageSize` padrão 200, máximo 500). O endpoint `GET /management/requests`
  ganhou os parâmetros opcionais `page`, `pageSize` e `search` (busca por
  título, autor, categoria e unidade/bloco, via `.ToLower().Contains()` —
  não `EF.Functions.ILike`, que não traduz sob o provedor SQLite usado nos
  testes de integração). `Total` passou a ser um `CountAsync()` real sobre a
  query filtrada, em vez do tamanho da lista já materializada em memória.
  Como o frontend ainda decide busca textual, ordenação e filtro de categoria
  no cliente sobre a lista completa, o `pageSize` padrão (200) foi escolhido
  para preservar o comportamento visual atual sem paginação de fato — mover
  essas responsabilidades para o backend e construir os controles de página
  na UI é trabalho futuro deliberadamente não incluído nesta rodada. Nenhum
  outro endpoint `List*` foi paginado; o padrão está pronto para reaproveitar.
- **Streaming do assistente de IA**: `CondominiumAssistantOptions.StreamingEnabled`
  (padrão `false`) controla um modo SSE opcional nos mesmos endpoints
  `POST /condominiums/{id}/assistant/messages` e
  `.../assistant/conversations/{id}/messages`, acionado com `?stream=true`.
  Com a flag desligada (padrão), o comportamento é idêntico ao anterior — o
  parâmetro é ignorado e a resposta continua sendo um JSON único. Com a flag
  ligada, a resposta é `text/event-stream` com eventos `sources` (todas as
  fontes recuperadas pelo RAG, antes da filtragem por citação — apenas
  informativo, para o cliente trocar o indicador de "buscando"), `token`
  (delta de texto por chunk da OpenAI) e `done` (resposta completa,
  `conversation` e fontes já filtradas por citação — mesmo formato que a
  resposta síncrona). `CondominiumAssistantService.AskStreamAsync` reaproveita
  o mesmo pipeline de recuperação de `AskAsync` (extraído para
  `PrepareAnswerContextAsync`), diferindo apenas na chamada final ao chat
  (`ChatStreamAsync`, com `stream: true` e leitura incremental via
  `HttpCompletionOption.ResponseHeadersRead`). `OpenAiTelemetryHandler` foi
  ajustado para não bufferizar o corpo da resposta quando o `Content-Type` é
  `text/event-stream`. No frontend, `assistant/streamAssistant.ts` sempre
  envia `?stream=true` e decide como interpretar a resposta pelo
  `Content-Type` recebido — se não for SSE (flag desligada ou backend antigo
  durante um deploy assíncrono), trata como o JSON de sempre. Isso permite
  publicar o código de streaming no frontend antes de habilitar a flag no
  backend sem quebrar nada.
- **OCR de documentos escaneados**: um PDF sem camada de texto (ata
  fotografada/escaneada) hoje falha com `Unsupported` e nenhum chunk é criado
  — o assistente então diz "não encontrei essa informação", o que parece um
  bug para quem acabou de subir o arquivo certo. `DocumentOcrOptions`
  (`DocumentOcr__Enabled`, padrão `false`) liga um fallback: para páginas com
  texto insuficiente, `CondominiumDocumentProcessor` extrai as imagens já
  incorporadas na página via PdfPig (`IPdfImage.RawBytes`/`TryGetPng` —
  não há renderização de página, só as imagens que o PDF já contém) e pede a
  um modelo de visão da OpenAI (`OpenAiDocumentOcrService`, mesma conta/chave
  usada pelo resto do assistente) para transcrever cada uma. Limite de páginas
  por documento (`MaximumPagesPerDocument`, padrão 30) protege contra custo
  descontrolado em documentos inteiramente escaneados e muito longos. Também
  independentemente do OCR: quando o assistente não encontra nenhuma evidência
  para uma pergunta E existem documentos ativos com status `Failed`/
  `Unsupported` no condomínio, a resposta ganha uma frase determinística
  apontando isso (`AppendUnprocessedDocumentsHintAsync`), em vez de depender do
  modelo notar e mencionar sozinho.

## Observabilidade operacional

O Overwatch considera operacional apenas instâncias de worker com heartbeat
dentro de 30 vezes seu intervalo esperado. Registros de containers antigos são
históricos: não degradam a saúde atual e são removidos pelo retention worker após
sete dias. A ausência ou atraso real de uma instância esperada continua
degradando a saúde. `UptimeSeconds` mede o tempo desde o início do processo atual
da API, reiniciando a cada deploy que recria o processo.
# Gestão condominial e administradora — Fundação (Lote 1)

O escopo administrativo de um condomínio é contextual e deriva de um vínculo ativo em
`CondominiumMembership` com papel ativo `Manager` ou `SubManager`. `Manager` continua
podendo gerir vários condomínios; `SubManager` representa **Subsíndico** na interface e
pode ter somente um vínculo ativo globalmente e somente um ocupante ativo por condomínio.
A API valida esse escopo no banco; papéis globais, inclusive `PlatformAdmin`, não concedem
automaticamente o papel contextual. A migration do lote instala uma restrição PostgreSQL
com advisory locks para serializar atribuições concorrentes de subsíndico.

PIX (`PixKeyType`/`PixKey`) pertence ao `ApplicationUser`, pois identifica o beneficiário,
e não ao condomínio. Os tipos aceitos são CPF, CNPJ, e-mail, telefone e chave aleatória.

`ManagementCompanyEmployee` é mantido como nome técnico/tabela por compatibilidade com
produção, mas sua abstração pública é **Acesso da administradora**. Um acesso autenticável
é `Person` ou `Department`, pertence a uma administradora e possui N categorias através de
`ManagementCompanyRequestCategoryResponsible`. A categoria fica operacionalmente
indisponível quando não há responsável ativo. Exclusão física de acessos foi substituída
por inativação para preservar histórico.

Na V1, Pessoa e Setor (`Person`/`Department`) são igualmente **acessos autenticáveis
individuais**: cada um tem seu próprio login, senha e conjunto de categorias, e a única
diferença entre eles é como o autor aparece na timeline (nome da pessoa e função vs. nome
do setor, sem se passar por uma pessoa). Setor não representa um grupo com múltiplos
usuários, não tem membership própria e não introduz RBAC adicional — isso está fora de
escopo até uma revisão explícita de produto.

Novas administradoras recebem as categorias estruturais Multa (`UnitFine`), Solicitação de
pagamento (`SupplierPayment`) e Dúvidas gerais (`Generic`). A migration cria essas mesmas
categorias para administradoras existentes sem sobrescrever categorias homônimas.

O vínculo atual `Condominium.ManagementCompanyId` permanece como projeção compatível. O
histórico autoritativo é `CondominiumManagementCompanyLink` (`LinkedAt`, `UnlinkedAt`,
`IsActive`), com índice parcial garantindo no máximo um vínculo ativo. Trocar ou desvincular
encerra o registro atual e nunca apaga o histórico.

Primeiro acesso reutiliza Identity, `MustChangePassword`, token de redefinição,
`SecurityStamp` e SMTP existentes. Acessos recebem senha temporária exibível, podem ter
instruções reenviadas e senha redefinida; redefinição invalida credenciais/sessões anteriores.
Endereços reservados de teste (`example.com`, `example.test`, `test.com`, `localhost`) não
recebem tentativa de e-mail.
# Solicitações Gestão ↔ Administradora (Lote 2)

`ManagementCompanyRequest` é um agregado separado de `Request` (Morador ↔ Gestão). A tabela principal guarda condomínio, administradora e categoria históricas, criador, tipo, estado e o identificador estável `ADM-` derivado de entropia do UUID e protegido por índice único. Os templates fixos usam tabelas 1:1 tipadas (`Fine`, `Payment` e `GeneralQuestion`), evitando colunas específicas anuláveis na raiz. A mensagem inicial de dúvida é a primeira `ManagementCompanyRequestMessage`, fonte única do texto da conversa.

## Portal da gestão (Lote 3)

As rotas `/management/administrator`, `/management/administrator/new` e
`/management/administrator/:id` atendem Manager e SubManager pelo mesmo escopo
contextual. O contexto de gestão informa, em uma consulta consolidada, se existe
administradora ativa no condomínio selecionado ou em ao menos um condomínio da
visão consolidada; esse indicador controla somente a navegação, permanecendo a
API como autoridade de acesso.

A listagem é paginada no backend, permite condomínio, tipo, status, busca e
período de criação (`CreatedAt`, datas inicial e final inclusivas) e prioriza
`WaitingManager`. Criação e respostas usam os contratos multipart atômicos do
domínio. A timeline resolve nome e função contextual do autor, reservando
"Sistema" para eventos automáticos. Anexos continuam privados: o frontend faz
download autenticado, cria Blob URLs sob demanda para imagem, áudio, vídeo e
PDF e os revoga ao desmontar; arquivos genéricos mantêm download autenticado.

## Portal da administradora (Lote 4)

O portal usa `/administrator/requests` e um contexto autenticado derivado do
`ManagementCompanyEmployee`, sem promover Pessoa ou Setor a papel global. A
fila possui consulta própria: deriva a administradora do acesso ativo e limita
categorias pela relação N:N, com paginação, busca e filtros no servidor.
`Submitted` é priorizado antes de `UpdatedAt DESC`; listar nunca registra
ciência, enquanto abrir o detalhe reutiliza a operação idempotente do domínio.

Detalhe e mutações continuam autorizados pela administradora histórica gravada
na solicitação e pela categoria atual do acesso. Assim, uma administradora nova
não recebe solicitações antigas; o acesso ativo da administradora histórica
mantém operação conforme categoria atribuída, independentemente do vínculo
atual do condomínio. O portal reutiliza multipart atômico e previews Blob
autenticados. `WaitingManager`, `Completed` e `Cancelled` são somente leitura
para a administradora; não existe cancelamento ou reabertura por esse portal.

**Regra histórica (confirmada como comportamento definitivo do produto):** uma
`ManagementCompanyRequest` pertence, para sempre, à administradora que a
recebeu — `Request.ManagementCompanyId` é gravado na criação e nunca migra. Se
um condomínio troca de administradora (`CondominiumManagementCompanyLink` da
antiga é desativado e um novo vínculo é criado para a nova empresa), a nova
administradora nunca herda acesso às solicitações antigas: ela não as lista,
não as abre, não baixa os attachments e não atua nelas. A administradora
anterior continua podendo operar suas próprias solicitações históricas
enquanto (a) o usuário estiver ativo, (b) o acesso da administradora estiver
ativo e (c) esse acesso continuar responsável pela categoria histórica da
solicitação — o vínculo atual do condomínio com outra empresa não revoga esse
acesso histórico. Coberto por
`ManagementCompanyRequestEndpointTests.Historical_access_survives_administrator_company_swap_and_new_company_never_inherits`.

O fluxo interno é `Submitted → Acknowledged → InProgress ↔ WaitingManager → Completed`; também são aceitos `Acknowledged → WaitingManager` e cancelamento pela gestão a partir de qualquer estado não terminal. `Completed` e `Cancelled` são terminais. Toda mutação registra `ManagementCompanyRequestHistory`; resposta da gestão em `WaitingManager` salva mensagem e retorno a `InProgress` na mesma transação. A primeira ciência é idempotente, usa token de concorrência otimista e índice filtrado único do evento `Acknowledged`.

Autorização operacional não concede bypass a `PlatformAdmin`. Gestão exige membership ativo com papel contextual `Manager` ou `SubManager`. Um acesso da administradora exige usuário e acesso ativos, igualdade com `ManagementCompanyId` histórico e responsabilidade pela `CategoryId` histórica. Após troca de administradora, a nova empresa nunca herda solicitações anteriores; a anterior mantém acesso apenas pelos seus acessos ativos e ainda responsáveis pela categoria histórica.

Multas validam unidade do condomínio e representam valor indefinido explicitamente. Pagamentos de reembolso guardam snapshot de beneficiário, nome, tipo e chave PIX. Anexos reutilizam política e armazenamento físico existentes, mas possuem metadados e endpoints autenticados próprios; todas as FKs históricas usam `Restrict`. A criação resolve a administradora ativa no servidor, valida categoria/template e ao menos um responsável ativo, e persiste agregado, detalhe, mensagem inicial e histórico em transação serializável.

Criações e interações que possuem arquivos usam contratos `multipart/form-data`. Metadados, mensagem, histórico e transição pertencem à mesma transação PostgreSQL. Como o filesystem não é transacional, os arquivos são gravados antes do commit e registrados para compensação: qualquer falha posterior remove todos os arquivos recém-gravados; falha de gravação impede o commit. Respostas da gestão em `WaitingManager` e solicitações de informação da administradora são operações únicas contendo mensagem, anexos e mudança de estado.

O portal da gestão expõe `/management/administrator`, criação e detalhe em rotas lazy-loaded. A listagem é paginada no backend e aplica primeiro `WaitingManager`, depois `UpdatedAt DESC`, com busca e filtros de condomínio, tipo e status dentro do escopo contextual combinado de `Manager`/`SubManager`. O endpoint de opções resolve a administradora ativa, categorias tipadas disponíveis, unidades e beneficiários elegíveis com PIX; o navegador não infere categoria por nome nem envia uma administradora como autoridade. Criação e resposta usam os contratos multipart atômicos, e cancelamento permanece uma operação explícita do domínio.

## Notificações Gestão ↔ Administradora (Lote 5)

Camada de notificações operacionais (interna + e-mail) para os cinco eventos relevantes do fluxo `ManagementCompanyRequest`. Reutiliza integralmente a infraestrutura existente do Comvy — nenhuma entidade paralela foi criada. Sem WhatsApp: fora de escopo neste lote.

**Eventos e destinatários** (`ManagementCompanyRequestNotificationService`, em `Features/ManagementCompanyRequests/`):

- **Criada** (`Create` → `Submitted`) — acessos ativos da administradora responsáveis pela categoria.
- **Solicitar informação** (transição para `WaitingManager`, via `/status` ou `/interactions`) — `Manager` + `SubManager` ativos do condomínio. Resolução própria, deliberadamente diferente da regra de Manager único da Agenda.
- **Resposta da gestão** (`ManagerResponded`: `WaitingManager → InProgress`) — acessos ativos da administradora responsáveis pela categoria.
- **Concluída** (transição para `Completed`) — `Manager` + `SubManager`, com título contextual por tipo (`Multa processada` / `Pagamento efetuado` / `Dúvida respondida`).
- **Cancelada** (`Cancel`) — acessos ativos da administradora responsáveis pela categoria.

Eventos puramente internos (`Submitted → Acknowledged`, `Acknowledged → InProgress`) não geram notificação nem e-mail — comportamento inalterado.

**Resolução histórica e dinâmica**: destinatários da administradora são sempre resolvidos a partir de `Request.ManagementCompanyId`/`Request.CategoryId` (imutáveis desde a criação), nunca do vínculo atual do condomínio — preserva integralmente a regra do Lote 4 (nova administradora nunca recebe eventos de solicitações antigas). A lista de responsáveis pela categoria é recalculada no momento do evento, não persistida na criação: se o responsável mudar entre a criação e uma resposta, o responsável atual recebe, não o antigo. `Manager`/`SubManager` são resolvidos da mesma forma a cada evento, deduplicados por `UserId`; `PlatformAdmin` nunca é destinatário.

**Modelo de dados**: `Notification` ganhou dois campos opcionais — `ManagementCompanyRequestId` (FK `Restrict` para `ManagementCompanyRequest`, já que `RequestId` tem FK obrigatória para `Request` do morador e não podia ser reaproveitado) e `IdempotencyKey` (string, índice único filtrado `WHERE idempotency_key IS NOT NULL`, mesmo padrão já validado em `WhatsAppOutboundMessage.IdempotencyKey`). Cinco novos valores de `NotificationType` (`ManagementCompanyRequestCreated/InfoRequested/ManagerReplied/Completed/Cancelled`); cada tipo tem destinatário fixo por natureza do evento, o que permite ao frontend decidir o link só pelo `type`. Migration única: `AddNotificationManagementCompanyRequestSupport`.

**Idempotência**: chave determinística `management-company-request:{requestId}:{evento}[:{historyId|messageId}]:{recipientId}` — sem timestamp. Antes de inserir, verifica se a chave já existe; uma violação do índice único no `SaveChanges` (corrida) é tratada como duplicata já registrada, nunca como erro. Eventos terminais (`Completed`/`Cancelled`) dispensam sufixo de correlação, pois só podem ocorrer uma vez por solicitação; `WaitingManager` usa o `HistoryId` da transição e a resposta da gestão usa o `MessageId`.

**Canais e atomicidade**: notificação interna é sempre persistida antes de qualquer tentativa de e-mail. E-mail reaproveita `IEmailSender`/`SmtpEmailSender`/`EmailOptions` (síncronos, já existentes para o primeiro acesso) e o flag `ApplicationUser.EmailDeliveryEnabled` para pular endereços mock/dev — sem outbox/worker novo, seguindo o padrão já usado pelo restante do projeto para e-mail. O endpoint chama o serviço de notificação **depois** do commit da mutação principal, em try/catch que só loga — falha de e-mail ou do dispatcher de notificação nunca reverte ou bloqueia a operação de negócio (mesmo padrão de `UpdateRequestStatus.cs`). Links de e-mail reaproveitam `FirstAccessOptions.FrontendBaseUrl` (nenhuma env var nova) apontando para `/administrator/requests/{id}` (administradora) ou `/management/administrator/{id}` (gestão) — mesmas rotas do portal existente.

**Frontend**: `notifications/presentation.ts::notificationLink()` passa a rotear pelo `type` quando `managementCompanyRequestId` está presente, sem alterar o comportamento existente de Resident Request nem criar uma central de notificações nova; o sino/badge existentes absorvem as novas notificações naturalmente.

**Segurança**: receber uma notificação não concede autorização — o clique ainda passa pelas checagens normais de `ManagementCompanyRequestAccessService` (ex.: um acesso que perde a categoria depois de notificado recebe 403 ao abrir o link). Coberto por `ManagementCompanyRequestNotificationTests`.

## Lote 6 — concorrência, segurança e estabilização final

Auditoria e endurecimento do módulo Gestão ↔ Administradora (Lotes 1–5), sem novas
funcionalidades. Objetivo: comprovar sob concorrência real (PostgreSQL, não
SQLite/in-memory) as invariantes que os Lotes 4/5 já implementavam mas nunca haviam sido
exercitadas por um teste verdadeiramente concorrente, fechar combinações de papel ausentes
na matriz de autorização, e registrar decisões conscientes sobre riscos que não foram
corrigidos.

**Concorrência comprovada em Postgres real** (`ManagementCompanyRequestPostgresConcurrencyTests`,
`SubManagerPostgresConcurrencyTests`, `CondominiumManagementCompanyLinkPostgresConcurrencyTests`,
gated por `COMVY_TEST_POSTGRES`):
- Duas respostas concorrentes da gestão em `WaitingManager` (via `InteractAsync` real, não
  domínio cru): só uma transição vence, uma mensagem sobrevive, um evento `ManagerResponded`.
- `WaitingManager` vs `Completed` a partir de `InProgress` (via `TransitionAsync` real): um
  vencedor consistente, um evento de histórico novo, nenhuma corrupção.
- Primeira ciência concorrente via `AcknowledgeAsync` do serviço (não só o domínio):
  encontrado e corrigido um bug real — o método só capturava `DbUpdateConcurrencyException`,
  mas a corrida real no Postgres pode se manifestar como um `DbUpdateException` genérico
  (violação do índice único `ux_mc_request_history_first_acknowledged`), que vazava sem
  tratamento para o chamador. Corrigido para capturar `DbUpdateException` (superclasse, já
  cobre o caso de concorrência otimista também).
- Idempotência de notificação sob concorrência real (duas chamadas simultâneas de
  `NotifyCreatedAsync` para o mesmo destinatário): exatamente 1 `Notification`, com o branch
  `catch(DbUpdateException)` do `DispatchAsync` de fato exercitado pela primeira vez.
- SubManager único: mesmo usuário não vence em dois condomínios simultaneamente; dois
  usuários não vencem no mesmo condomínio simultaneamente — este segundo caso só é protegido
  pelo trigger de banco `enforce_single_active_submanager_role` (o lock de aplicação em
  `SubManagerEndpoints.AssignAsync` é só por usuário), agora comprovado por teste real.
- Administradora ativa única por condomínio: duas trocas concorrentes de vínculo para o
  mesmo condomínio terminam com exatamente um link ativo e nunca alteram o
  `ManagementCompanyId` histórico de uma `ManagementCompanyRequest` já existente.

`SubManagerEndpoints.AssignAsync` e `SetCondominiumManagementCompany.HandleAsync` foram
tornados `internal` (de `private`) só para permitir que os testes chamem o caminho real de
produção — sem nenhuma mudança de comportamento.

**Autorização — combinações adicionadas**: SubManager de outro condomínio, Resident do
mesmo condomínio da request, e um usuário ativo cujo papel de Manager foi desativado
(distinto de "usuário inativo", já coberto) — todos 403 em list/detail/mensagem/status/
cancelamento/anexos. Usuário multi-role (Manager de um condomínio + acesso ativo da
administradora de outra empresa) comprovado com escopos totalmente independentes, sem
concessão cruzada. Snapshot histórico de PIX comprovado por teste ponta-a-ponta: mudar o PIX
do beneficiário depois da criação não altera o valor exibido na request histórica.

**Logging**: `ManagementCompanyRequestNotificationService.DispatchAsync` agora inclui
`FriendlyIdentifier` em todas as linhas de log, para correlação por identificador amigável
além do `RequestId`.

**Decisão documentada — janela pós-commit de notificação: ACEITÁVEL PARA V1.** O dispatch de
notificação roda no mesmo processo, logo após o commit da mutação principal, sem
outbox/fila. Se o processo morrer entre os dois `await`, a `ManagementCompanyRequest` fica
correta para sempre, mas a notificação/e-mail daquele evento específico se perde, sem retry
automático. Optou-se por **não** implementar um outbox transacional porque: (a) o registro
de negócio nunca fica inconsistente — o evento perdido é recuperável manualmente (a outra
parte só precisa abrir o portal); (b) o padrão atual (e-mail síncrono, best-effort) já é o
padrão usado no resto do Comvy (ex.: primeiro acesso); (c) um outbox real exigiria nova
entidade, nova migration e um worker — infraestrutura nova que o próprio lote pede para
evitar. Reavaliar apenas se o volume de eventos ou a criticidade do canal mudarem.

**Riscos aceitos, não corrigidos neste lote** (documentados para revisão futura, não
regressões):
- PIX/beneficiário: qualquer Manager/SubManager do condomínio pode ver o PIX de qualquer
  outro Manager/SubManager via `/management-company-requests/options` (necessário para
  escolher beneficiário de reembolso). Comportamento do Lote 3/4, não introduzido agora;
  restringir isso seria uma mudança de produto, não um hardening.
- Attachments: validação continua por extensão + `Content-Type` declarado, sem
  magic-byte/content-sniffing. Um arquivo renomeado com MIME forjado passaria. Implementar
  sniffing é uma camada de validação nova (feature), não um bug — fica para revisão futura.
- Filtros da tela Administradora seguem pouco responsivos (já documentado no Lote 4) —
  usáveis, sem regressão funcional, refinamento visual fica para depois.
- `/administrator/context` retorna 403 para Manager/SubManager puro (sem acesso de
  administradora) a cada carregamento — já não bloqueia nada (`AppShell` trata isso
  explicitamente) e não há métricas de observabilidade genéricas por status HTTP para
  poluir. Aceitável para V1; corrigir exigiria redesenhar o provider global de contexto.

**Frontend**: `CreateManagementCompanyRequestPage` agora mostra um aviso explícito quando a
administradora está vinculada mas nenhuma categoria tem responsável ativo, em vez de uma
área em branco sob o título — a regra de negócio (bloquear a criação nesse caso) não mudou,
só deixou de parecer uma tela quebrada.

## Refinamento pós-Lote 6 — fluxo Gestão ↔ Administradora

O detalhe deixou de produzir ciência por navegação. A fila da Administradora confirma a intenção e o endpoint `start-processing` executa `Submitted → Acknowledged → InProgress` com o token de concorrência existente. Mensagens e anexos usam `ManagementCompanyRequestMessage` como chat bilateral, em ordem cronológica, e ficam fora da projeção visual da timeline de eventos de negócio.

`WaitingManager` passou a ser uma ação explícita, independente de mensagem. Mensagem comum da Administradora não muda estado; mensagem da Gestão durante `WaitingManager` persiste mensagem e retorno a `InProgress` na mesma transação. A Administradora também pode cancelar estados não terminais, usando exatamente a autorização histórica de empresa e categoria aplicada ao detalhe.

Cada mensagem notifica somente a outra ponta por Notification interna e e-mail, com chave idempotente baseada em `message.Id` e destinatário. Gestão → Administradora resolve acessos ativos pela empresa/categoria históricas; Administradora → Gestão resolve Manager e SubManager ativos do condomínio, sem fallback para PlatformAdmin. As filas refazem a consulta no foco; a fila da Administradora também usa polling conservador de 30 segundos.

### Refinamento 2 de apresentação

Os formulários do módulo usam `CurrencyField` para exibir BRL enquanto mantêm decimal numérico no contrato, com atalhos locais e não negativos para multas. Arquivos selecionados recebem previews locais com `object URL` revogada; anexos persistidos preservam o fluxo autenticado `fetch → Blob → object URL` nos dois portais.

A fila operacional da Administradora oculta concluídas e canceladas por padrão por filtros server-side, pode incluí-las separadamente e recebe projeção compacta de unidade/valor/beneficiário apenas depois de `Submitted`. O detalhe apresenta os papéis Manager/SubManager efetivos como Síndico/Subsíndico, informações do solicitante e dados da solicitação em grade responsiva.
# Refinamento 3 pós-Lote 6 — identificação e sessão persistente

Novas solicitações Gestão ↔ Administradora usam `ADM-{ANO}-{SEQUENCIAL}`, com ano UTC e padding mínimo de quatro dígitos. Uma tabela de contador anual é atualizada por `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` no PostgreSQL; o índice único de `FriendlyIdentifier` permanece como última garantia. O GUID continua sendo PK/FK e rota técnica. Identificadores históricos não são alterados.

A autenticação mantém o access token JWT com a duração configurada em `Jwt:ExpirationMinutes` (60 minutos no desenvolvimento) e acrescenta refresh token rotativo com validade configurável, 30 dias por padrão. O segredo aleatório fica somente no cookie `HttpOnly`, `Secure` sob HTTPS, `SameSite=Lax`, restrito a `/auth`; o banco guarda apenas SHA-256. Cada refresh revoga o token anterior, emite um novo par e valida usuário ativo, `MustChangePassword` e o `SecurityStamp` capturado. Logout revoga a sessão atual. A validade é deslizante pela rotação, sem sessão eterna: uma sessão inativa expira após o período configurado.

O frontend usa uma única promise compartilhada para refresh e repete cada request no máximo uma vez. No bootstrap, o cookie permite reconstruir a sessão mesmo sem access token JavaScript válido. `/administrator/context` só é carregado quando `/users/me` informa acesso ativo à Administradora, preservando usuários multi-role e a autorização do backend.
## Permissões contextuais de SubManager

SubManager continua sendo uma role contextual de um único vínculo de condomínio, não um RBAC genérico. A tabela `sub_manager_module_permissions` guarda uma linha tipada por módulo (`Requests`, `Attendance`, `ManagementCompany`, `Agenda`, `Assistant`, `Documents`, `Management`), com concessão/revogação e usuário auditor. Manager mantém o comportamento integral atual. Vínculos SubManager sem linhas usam compatibilidade integral; novos vínculos são inicializados com os sete módulos habilitados, permitindo revogação posterior sem conceder acesso fora do condomínio.
