# CondoLink — Architecture

## Objetivo

Este documento descreve a arquitetura inicial do CondoLink.

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

* tamanho máximo;
* tipo de arquivo permitido;
* nome seguro;
* autorização para acessar o anexo;
* inexistência de exposição direta do caminho físico.

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
