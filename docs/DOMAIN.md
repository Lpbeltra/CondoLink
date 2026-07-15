# CondoLink — Domain Model

## Objetivo

Este documento descreve os principais conceitos do domínio do CondoLink, suas responsabilidades e regras de negócio.

O `ERD.md` descreve os relacionamentos entre as entidades.

O `WORKFLOWS.md` descreve o comportamento dos fluxos.

Este documento responde:

> O que representa cada entidade e quais regras ela deve respeitar?

---

# 1. User

## Responsabilidade

Representa a identidade global de uma pessoa no CondoLink.

O usuário não é permanentemente síndico, morador ou proprietário.

Esses papéis dependem do contexto do condomínio e da unidade.

## Campos principais

```text
Id
FullName
Email
PhoneNumber
IsActive
CreatedAt
UpdatedAt
```

Parte desses dados poderá ser fornecida pelo ASP.NET Core Identity.

## Regras

* O e-mail deve ser único.
* Um usuário pode participar de vários condomínios.
* Um usuário pode possuir vínculos com várias unidades.
* A desativação da conta não deve apagar registros históricos.
* Um usuário não deve possuir acesso a dados de condomínios com os quais não tenha vínculo ativo.

---

# 2. Condominium

## Responsabilidade

Representa um condomínio cadastrado no CondoLink.

É o principal contexto de separação dos dados da aplicação.

## Campos principais

```text
Id
Name
Email
PhoneNumber
IsActive
CreatedAt
UpdatedAt
```

## Regras

* Todo dado operacional deve pertencer a um condomínio quando aplicável.
* Um condomínio inativo não deve permitir novas operações.
* A desativação do condomínio não deve apagar dados históricos.
* Endereço, dados fiscais, dados bancários e dados jurídicos ficam fora do MVP.

---

# 3. CondominiumMembership

## Responsabilidade

Representa a participação de um usuário em um condomínio.

A participação indica que o usuário possui algum tipo de acesso ao condomínio, mas não define sozinha qual é seu papel.

## Campos principais

```text
Id
UserId
CondominiumId
IsActive
JoinedAt
EndedAt
CreatedAt
```

## Regras

* Um usuário pode participar de vários condomínios.
* Um condomínio pode possuir vários usuários.
* Deve existir apenas um vínculo entre o mesmo usuário e condomínio.
* Um vínculo pode ser desativado e posteriormente reativado.
* Quando o vínculo estiver ativo, `EndedAt` deve ser nulo.
* Quando o vínculo estiver encerrado, `EndedAt` deve estar preenchido.
* Um síndico profissional pode possuir este vínculo sem possuir vínculo com nenhuma unidade.

## Restrição conceitual

```text
UserId + CondominiumId
```

---

# 4. CondominiumMembershipRole

## Responsabilidade

Representa um papel atribuído a um usuário dentro de determinado condomínio.

Permite que uma mesma pessoa possua mais de um papel no mesmo condomínio.

## Campos principais

```text
Id
CondominiumMembershipId
Role
IsActive
GrantedAt
RevokedAt
```

## Papéis do MVP

```text
Manager
Resident
```

## Papéis futuros

```text
CouncilMember
Administrator
```

## Regras

* Um vínculo pode possuir vários papéis.
* O mesmo papel não deve ser duplicado dentro da mesma participação.
* Um usuário pode ser `Manager` e `Resident` ao mesmo tempo.
* Um papel ativo deve possuir `RevokedAt` nulo.
* Um papel revogado deve possuir `RevokedAt` preenchido.
* Os papéis serão definidos inicialmente por enum.
* Papéis personalizados ficam fora do MVP.

## Restrição conceitual

```text
CondominiumMembershipId + Role
```

---

# 5. Unit

## Responsabilidade

Representa uma unidade pertencente a um condomínio.

Exemplos:

```text
Apartamento 305
Casa 12
Sala 03
Loja A
```

## Campos principais

```text
Id
CondominiumId
Identifier
Block
Floor
Description
IsActive
CreatedAt
UpdatedAt
```

## Regras

* Uma unidade pertence a apenas um condomínio.
* `Identifier` é obrigatório.
* `Block` é opcional.
* A identificação deve ser única dentro do condomínio e bloco.
* Uma unidade inativa não pode receber novos vínculos.
* Uma unidade inativa deve permanecer disponível para registros históricos.
* Uma unidade não deve ser removida fisicamente caso possua histórico.

## Restrição conceitual

```text
CondominiumId + Block + Identifier
```

---

# 6. UnitMembership

## Responsabilidade

Representa a relação entre um usuário e uma unidade.

A relação com a unidade é independente do papel do usuário no condomínio.

## Campos principais

```text
Id
UserId
UnitId
RelationshipType
IsResident
IsPrimaryResidence
IsActive
StartedAt
EndedAt
CreatedAt
```

## Tipos de vínculo do MVP

```text
Owner
Tenant
AuthorizedOccupant
```

## Exemplos

```text
Owner + IsResident = true
```

Proprietário que mora na unidade.

```text
Owner + IsResident = false
```

Proprietário que não mora na unidade.

```text
Tenant + IsResident = true
```

Inquilino residente.

## Regras

* Um usuário pode possuir vínculos com várias unidades.
* Um usuário pode ser proprietário de uma unidade e morar em outra.
* Um usuário pode ter vários vínculos ativos no mesmo condomínio.
* `IsPrimaryResidence` só pode ser verdadeiro quando `IsResident` também for verdadeiro.
* Um vínculo ativo deve possuir `EndedAt` nulo.
* Um vínculo encerrado deve possuir `EndedAt` preenchido.
* Um síndico profissional não precisa possuir vínculo com unidade.
* A unidade deve estar ativa para a criação de novos vínculos.

---

# 7. Category

## Responsabilidade

Representa uma categoria de solicitação definida por um condomínio.

Categorias não são globais.

Cada condomínio pode organizar seu atendimento de acordo com sua própria realidade.

## Campos principais

```text
Id
CondominiumId
Name
Description
IsActive
CreatedAt
UpdatedAt
```

## Exemplos

```text
Manutenção
Encomendas
Barulho
Portaria
Limpeza
Segurança
```

## Regras

* Uma categoria pertence a apenas um condomínio.
* O nome deve ser único dentro do condomínio.
* Uma categoria inativa não pode ser utilizada em novas solicitações.
* Solicitações antigas permanecem vinculadas a categorias inativas.
* Categorias utilizadas em solicitações não devem ser removidas fisicamente.

## Restrição conceitual

```text
CondominiumId + Name
```

---

# 8. Request

## Responsabilidade

Representa uma demanda registrada dentro de um condomínio.

Uma solicitação não representa conversa e não representa tarefa interna.

## Campos principais

```text
Id
CondominiumId
AuthorUserId
TargetUnitId
CategoryId
Title
Description
Status
Priority
CreatedAt
UpdatedAt
ResolvedAt
```

## Status do MVP

```text
Open
InProgress
WaitingForResident
WaitingForThirdParty
Resolved
Cancelled
```

## Prioridades do MVP

```text
Normal
High
Urgent
```

## Regras

* Toda solicitação pertence a um condomínio.
* O autor deve possuir vínculo ativo com o condomínio.
* A categoria deve pertencer ao mesmo condomínio.
* A unidade-alvo é opcional.
* Quando informada, a unidade-alvo deve pertencer ao mesmo condomínio.
* O autor não precisa possuir vínculo com a unidade-alvo.
* A prioridade inicial deve ser `Normal`.
* O status inicial deve ser `Open`.
* Toda alteração de status deve gerar histórico.
* Apenas usuários com papel de gestão podem alterar status e prioridade no MVP.
* Uma solicitação resolvida pode ser reaberta.
* Uma solicitação cancelada não pode ser reaberta no MVP.
* `ResolvedAt` deve ser preenchido quando o status for `Resolved`.
* `ResolvedAt` deve ser nulo quando a solicitação for reaberta.
* Mensagens e tarefas não substituem a solicitação.

## Exemplo

```text
Autor: morador da unidade 305
Unidade-alvo: unidade 804
Categoria: Vazamento
```

O autor e a unidade envolvida são conceitos diferentes.

---

# 9. RequestMessage

## Responsabilidade

Representa uma mensagem enviada na conversa de uma solicitação.

## Campos principais

```text
Id
RequestId
AuthorUserId
Content
CreatedAt
```

## Regras

* Toda mensagem pertence a uma solicitação.
* O autor deve possuir acesso ao condomínio da solicitação.
* O conteúdo não pode estar vazio.
* Mensagens não podem ser editadas no MVP.
* Mensagens não podem ser apagadas no MVP.
* O envio de uma mensagem não altera automaticamente o status.
* Mensagens e alterações de status são eventos diferentes.

---

# 10. RequestStatusHistory

## Responsabilidade

Representa o histórico estruturado das alterações de status de uma solicitação.

## Campos principais

```text
Id
RequestId
PreviousStatus
NewStatus
ChangedByUserId
Reason
CreatedAt
```

## Regras

* Toda mudança de status deve gerar um registro.
* O primeiro registro deve representar a criação da solicitação.
* No primeiro registro, `PreviousStatus` deve ser nulo.
* O histórico não pode ser editado.
* O histórico não pode ser apagado.
* O usuário responsável deve possuir acesso ao condomínio.
* A transição deve respeitar as regras definidas em `WORKFLOWS.md`.
* O motivo é opcional, mas pode ser exigido futuramente em transições específicas.

## Registro inicial

```text
PreviousStatus = null
NewStatus = Open
ChangedByUserId = AuthorUserId
```

---

# 11. RequestAttachment

## Responsabilidade

Representa um arquivo anexado à abertura de uma solicitação ou a uma mensagem.

## Campos principais

```text
Id
RequestId
RequestMessageId
UploadedByUserId
OriginalFileName
StorageKey
ContentType
FileSize
CreatedAt
```

## Regras

* Todo anexo pertence a uma solicitação.
* `RequestMessageId` é opcional.
* Quando informado, a mensagem deve pertencer à mesma solicitação.
* Um anexo sem mensagem associada pertence à abertura da solicitação.
* O usuário responsável pelo upload deve possuir acesso ao condomínio.
* O arquivo físico não será armazenado diretamente no PostgreSQL.
* `StorageKey` representa a localização do arquivo no serviço de armazenamento.
* Limites de tamanho e tipos permitidos serão definidos na implementação.
* Anexos não devem ser removidos sem que exista uma política explícita de retenção.

---

# 12. Task

## Responsabilidade

Representa uma atividade interna de gestão.

Uma tarefa não representa uma solicitação e não possui conversa própria no MVP.

## Campos principais

```text
Id
CondominiumId
CreatedByUserId
AssignedToUserId
SourceRequestId
Title
Description
Status
Priority
DueDate
CompletedAt
CreatedAt
UpdatedAt
```

## Status do MVP

```text
Pending
InProgress
Completed
Cancelled
```

## Prioridades do MVP

```text
Normal
High
Urgent
```

## Regras

* Toda tarefa pertence a um condomínio.
* O criador deve possuir vínculo ativo com o condomínio.
* O responsável é opcional.
* Quando informado, o responsável deve possuir vínculo ativo com o condomínio.
* A solicitação de origem é opcional.
* Quando informada, deve pertencer ao mesmo condomínio.
* Uma solicitação pode originar várias tarefas.
* Uma tarefa pode existir sem solicitação de origem.
* Concluir uma tarefa não resolve automaticamente a solicitação.
* Resolver uma solicitação não conclui automaticamente suas tarefas.
* Uma tarefa concluída deve possuir `CompletedAt`.
* Uma tarefa não concluída deve possuir `CompletedAt` nulo.
* Comentários, subtarefas e histórico de tarefas ficam fora do MVP.

---

# 13. Integridade entre condomínios

O CondoLink é uma aplicação multi-condomínio.

Por isso, a aplicação deve validar que entidades relacionadas pertencem ao mesmo condomínio quando aplicável.

Exemplos:

* a categoria da solicitação pertence ao mesmo condomínio;
* a unidade-alvo pertence ao mesmo condomínio;
* a solicitação de origem da tarefa pertence ao mesmo condomínio;
* o responsável pela tarefa possui vínculo com o condomínio;
* o autor da mensagem possui acesso ao condomínio;
* o usuário que envia um anexo possui acesso ao condomínio.

Essas regras devem ser verificadas no domínio ou na camada de aplicação.

A interface não pode ser a única responsável por garantir essas restrições.

---

# 14. Preservação de histórico

Registros históricos devem ser preservados.

Por isso:

* usuários devem ser desativados, não apagados, quando possuírem histórico;
* condomínios devem ser desativados, não apagados;
* unidades utilizadas em registros devem ser desativadas;
* categorias utilizadas em solicitações devem ser desativadas;
* vínculos encerrados devem manter suas datas;
* mensagens não podem ser apagadas no MVP;
* históricos de status não podem ser apagados;
* tarefas concluídas ou canceladas devem permanecer disponíveis.

A estratégia técnica de exclusão lógica será definida posteriormente.

---

# 15. Conceitos fora do MVP

Os seguintes conceitos não fazem parte do domínio inicial:

* administradoras;
* fornecedores;
* prestadores;
* portarias remotas;
* áreas comuns;
* blocos como entidade própria;
* elevadores;
* garagens;
* portões;
* templates de resposta;
* fluxos guiados;
* formulários dinâmicos;
* checklists;
* base de conhecimento;
* SLAs;
* comentários em tarefas;
* subtarefas;
* histórico de tarefas;
* papéis personalizados;
* dados fiscais;
* endereços;
* integrações externas;
* inteligência artificial.

Esses conceitos poderão ser adicionados quando houver necessidade real.
