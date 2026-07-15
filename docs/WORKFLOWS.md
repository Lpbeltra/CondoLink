# CondoLink — Workflows

## Objetivo

Este documento descreve os principais fluxos de negócio do CondoLink.

Enquanto o **ERD** descreve as entidades e seus relacionamentos, este documento descreve **como o sistema deve se comportar** durante o atendimento de uma solicitação.

---

# 1. Princípios

O CondoLink foi projetado para representar a rotina real de atendimento de um condomínio.

Os fluxos devem:

* reduzir trabalho repetitivo;
* deixar claro quem precisa agir;
* preservar o histórico das ações;
* manter o síndico no controle do atendimento;
* ser simples o suficiente para atender o MVP.

---

# 2. Ciclo de vida da Request

Toda solicitação nasce com o status:

```text
Open
```

Fluxo principal:

```text
Open
   │
   ▼
InProgress
   │
   ├───────────────┐
   ▼               ▼
WaitingForResident WaitingForThirdParty
   │               │
   └───────┬───────┘
           ▼
      InProgress
           │
           ▼
       Resolved
```

Uma solicitação também pode ser cancelada durante o atendimento.

```text
Cancelled
```

---

# 3. Status

## Open

Solicitação recém-criada.

Ainda não foi iniciada.

---

## InProgress

Solicitação em atendimento.

O síndico está analisando ou executando alguma ação.

---

## WaitingForResident

O atendimento depende de alguma informação ou ação do morador.

Exemplos:

* envio de foto;
* envio de vídeo;
* confirmação de horário;
* envio de comprovante;
* resposta a perguntas.

---

## WaitingForThirdParty

O atendimento depende de terceiros.

Exemplos:

* administradora;
* portaria remota;
* fornecedor;
* empresa de manutenção;
* prestador de serviço.

---

## Resolved

A solicitação foi solucionada.

O síndico decide quando uma solicitação deve ser considerada resolvida.

A confirmação do morador não é obrigatória.

---

## Cancelled

Solicitação encerrada por motivo administrativo.

Exemplos:

* duplicada;
* aberta por engano;
* desistência;
* assunto fora do escopo do condomínio.

---

# 4. Transições permitidas

## Open

Pode ir para:

* InProgress
* Cancelled

---

## InProgress

Pode ir para:

* WaitingForResident
* WaitingForThirdParty
* Resolved
* Cancelled

---

## WaitingForResident

Pode ir para:

* InProgress
* Resolved
* Cancelled

---

## WaitingForThirdParty

Pode ir para:

* InProgress
* Resolved
* Cancelled

---

## Resolved

Pode voltar para:

* InProgress

Essa transição representa a reabertura da solicitação.

---

## Cancelled

Estado final.

Solicitações canceladas não podem ser reabertas no MVP.

---

# 5. Abertura de uma solicitação

Ao criar uma solicitação, o sistema deve:

1. Registrar o autor.
2. Registrar o condomínio.
3. Registrar a categoria.
4. Registrar o título.
5. Registrar a descrição inicial.
6. Definir o status como `Open`.
7. Criar o primeiro registro em `RequestStatusHistory`.

---

# 6. Alteração de status

Toda alteração de status deve gerar um registro em `RequestStatusHistory`.

Cada registro deve armazenar:

* status anterior;
* novo status;
* usuário responsável;
* data e hora;
* motivo (quando informado).

---

# 7. Controle da solicitação

No MVP:

O morador pode:

* abrir solicitações;
* responder mensagens;
* anexar arquivos.

O síndico pode:

* alterar status;
* alterar prioridade;
* responder mensagens;
* anexar arquivos;
* resolver solicitações;
* cancelar solicitações;
* reabrir solicitações resolvidas.

O ciclo de vida da solicitação é controlado pelo síndico.

---

# 8. Mensagens

As mensagens representam a conversa da solicitação.

No MVP:

* não podem ser editadas;
* não podem ser apagadas;
* possuem autor e data;
* não alteram automaticamente o status.

Mudanças de status e mensagens são eventos diferentes.

---

# 9. Anexos

Os anexos podem ser enviados:

* na abertura da solicitação;
* durante a conversa.

O arquivo físico não será armazenado diretamente no banco de dados.

---

# 10. Tarefas

Uma tarefa representa uma atividade interna do condomínio.

Ela pode:

* nascer de uma solicitação;
* ser criada independentemente.

O ciclo de vida da tarefa é independente da solicitação.

Concluir uma tarefa não resolve automaticamente a solicitação.

Resolver uma solicitação não conclui automaticamente suas tarefas.

---

# 11. Fluxos futuros

Os itens abaixo não fazem parte do MVP, mas orientam a evolução do produto:

* templates de resposta;
* fluxos guiados por categoria;
* checklists;
* formulários dinâmicos;
* base de conhecimento;
* automações;
* mudanças automáticas de status;
* inteligência artificial para classificação e sugestões de resposta.

Esses recursos deverão ser implementados apenas quando houver necessidade real.
