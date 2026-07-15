# CondoLink — Entity Relationship Diagram

## Objetivo

Este documento apresenta as entidades do domínio do CondoLink e seus principais relacionamentos no MVP.

O diagrama é conceitual. Regras detalhadas de negócio devem permanecer em `DOMAIN.md` e os fluxos de status em `WORKFLOWS.md`.

---

## Diagrama

```mermaid
erDiagram
    USER ||--o{ CONDOMINIUM_MEMBERSHIP : participates
    CONDOMINIUM ||--o{ CONDOMINIUM_MEMBERSHIP : has

    CONDOMINIUM_MEMBERSHIP ||--o{ CONDOMINIUM_MEMBERSHIP_ROLE : receives

    CONDOMINIUM ||--o{ UNIT : contains

    USER ||--o{ UNIT_MEMBERSHIP : has
    UNIT ||--o{ UNIT_MEMBERSHIP : receives

    CONDOMINIUM ||--o{ CATEGORY : defines

    CONDOMINIUM ||--o{ REQUEST : owns
    USER ||--o{ REQUEST : authors
    UNIT o|--o{ REQUEST : is_target_of
    CATEGORY ||--o{ REQUEST : classifies

    REQUEST ||--o{ REQUEST_MESSAGE : contains
    USER ||--o{ REQUEST_MESSAGE : writes

    REQUEST ||--o{ REQUEST_STATUS_HISTORY : tracks
    USER ||--o{ REQUEST_STATUS_HISTORY : changes

    REQUEST ||--o{ REQUEST_ATTACHMENT : has
    REQUEST_MESSAGE o|--o{ REQUEST_ATTACHMENT : includes
    USER ||--o{ REQUEST_ATTACHMENT : uploads

    CONDOMINIUM ||--o{ TASK : owns
    USER ||--o{ TASK : creates
    USER o|--o{ TASK : is_assigned
    REQUEST o|--o{ TASK : originates

    USER {
        uuid Id
        string FullName
        string Email
        string PhoneNumber
        boolean IsActive
        datetime CreatedAt
        datetime UpdatedAt
    }

    CONDOMINIUM {
        uuid Id
        string Name
        string Email
        string PhoneNumber
        boolean IsActive
        datetime CreatedAt
        datetime UpdatedAt
    }

    CONDOMINIUM_MEMBERSHIP {
        uuid Id
        uuid UserId
        uuid CondominiumId
        boolean IsActive
        datetime JoinedAt
        datetime EndedAt
        datetime CreatedAt
    }

    CONDOMINIUM_MEMBERSHIP_ROLE {
        uuid Id
        uuid CondominiumMembershipId
        string Role
        boolean IsActive
        datetime GrantedAt
        datetime RevokedAt
    }

    UNIT {
        uuid Id
        uuid CondominiumId
        string Identifier
        string Block
        string Floor
        string Description
        boolean IsActive
        datetime CreatedAt
        datetime UpdatedAt
    }

    UNIT_MEMBERSHIP {
        uuid Id
        uuid UserId
        uuid UnitId
        string RelationshipType
        boolean IsResident
        boolean IsPrimaryResidence
        boolean IsActive
        datetime StartedAt
        datetime EndedAt
        datetime CreatedAt
    }

    CATEGORY {
        uuid Id
        uuid CondominiumId
        string Name
        string Description
        boolean IsActive
        datetime CreatedAt
        datetime UpdatedAt
    }

    REQUEST {
        uuid Id
        uuid CondominiumId
        uuid AuthorUserId
        uuid TargetUnitId
        uuid CategoryId
        string Title
        string Description
        string Status
        string Priority
        datetime CreatedAt
        datetime UpdatedAt
        datetime ResolvedAt
    }

    REQUEST_MESSAGE {
        uuid Id
        uuid RequestId
        uuid AuthorUserId
        string Content
        datetime CreatedAt
    }

    REQUEST_STATUS_HISTORY {
        uuid Id
        uuid RequestId
        string PreviousStatus
        string NewStatus
        uuid ChangedByUserId
        string Reason
        datetime CreatedAt
    }

    REQUEST_ATTACHMENT {
        uuid Id
        uuid RequestId
        uuid RequestMessageId
        uuid UploadedByUserId
        string OriginalFileName
        string StorageKey
        string ContentType
        long FileSize
        datetime CreatedAt
    }

    TASK {
        uuid Id
        uuid CondominiumId
        uuid CreatedByUserId
        uuid AssignedToUserId
        uuid SourceRequestId
        string Title
        string Description
        string Status
        string Priority
        date DueDate
        datetime CompletedAt
        datetime CreatedAt
        datetime UpdatedAt
    }
```

---

## Cardinalidades principais

```text
User 1 ---- N CondominiumMembership
Condominium 1 ---- N CondominiumMembership

CondominiumMembership 1 ---- N CondominiumMembershipRole

Condominium 1 ---- N Unit

User 1 ---- N UnitMembership
Unit 1 ---- N UnitMembership

Condominium 1 ---- N Category

Condominium 1 ---- N Request
User 1 ---- N Request
Unit 0..1 ---- N Request
Category 1 ---- N Request

Request 1 ---- N RequestMessage
User 1 ---- N RequestMessage

Request 1 ---- N RequestStatusHistory
User 1 ---- N RequestStatusHistory

Request 1 ---- N RequestAttachment
RequestMessage 0..1 ---- N RequestAttachment
User 1 ---- N RequestAttachment

Condominium 1 ---- N Task
User 1 ---- N Task como criador
User 0..1 ---- N Task como responsável
Request 0..1 ---- N Task
```

---

## Observações

* `User` representa uma identidade global.
* Os papéis do usuário dependem do condomínio.
* Um usuário pode possuir vários papéis no mesmo condomínio.
* O vínculo com unidades é independente dos papéis no condomínio.
* Um síndico profissional pode não possuir vínculo com nenhuma unidade.
* Categorias pertencem ao condomínio.
* O autor da solicitação e a unidade-alvo são conceitos diferentes.
* A unidade-alvo de uma solicitação é opcional.
* `Request`, `RequestMessage`, `RequestStatusHistory` e `Task` representam conceitos diferentes.
* Uma solicitação pode originar várias tarefas.
* Uma tarefa também pode existir sem uma solicitação de origem.
* Arquivos anexados não serão armazenados diretamente no PostgreSQL.
* Entidades relacionadas devem pertencer ao mesmo condomínio quando aplicável.
