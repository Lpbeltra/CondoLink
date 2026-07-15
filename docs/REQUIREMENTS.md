# CondoLink — Requirements

## Objetivo

Este documento descreve os requisitos funcionais e não funcionais do MVP do CondoLink.

O objetivo do MVP é validar a proposta do produto com um conjunto enxuto de funcionalidades, evitando complexidade desnecessária.

---

# 1. Visão do Produto

O CondoLink é uma plataforma SaaS para gestão de atendimento condominial.

Seu objetivo é centralizar a comunicação entre moradores e síndicos, organizar solicitações, registrar históricos e reduzir o trabalho operacional do gestor.

O MVP tem como foco principal o gerenciamento de solicitações.

---

# 2. Objetivos do MVP

O MVP deve permitir que um condomínio consiga:

* cadastrar moradores;
* cadastrar unidades;
* organizar solicitações por categoria;
* acompanhar o andamento das solicitações;
* manter um histórico das interações;
* registrar tarefas internas;
* consultar indicadores básicos do condomínio.

O objetivo não é automatizar processos neste momento.

---

# 3. Requisitos Funcionais

## RF-001 — Autenticação

O sistema deve permitir que usuários autenticados acessem a plataforma.

---

## RF-002 — Gestão de Condomínios

O sistema deve permitir cadastrar e editar condomínios.

No MVP:

* nome;
* e-mail;
* telefone.

---

## RF-003 — Gestão de Usuários

O sistema deve permitir que usuários participem de um ou mais condomínios.

Cada usuário possui uma identidade única.

---

## RF-004 — Papéis no Condomínio

O sistema deve permitir atribuir papéis aos usuários.

Papéis do MVP:

* Manager
* Resident

Um usuário pode possuir mais de um papel no mesmo condomínio.

---

## RF-005 — Gestão de Unidades

O sistema deve permitir:

* cadastrar unidades;
* editar unidades;
* desativar unidades.

Cada unidade pertence a um condomínio.

---

## RF-006 — Vínculo com Unidades

O sistema deve permitir vincular usuários às unidades.

Tipos do MVP:

* Owner
* Tenant
* AuthorizedOccupant

---

## RF-007 — Categorias

Cada condomínio poderá criar suas próprias categorias de atendimento.

Exemplos:

* Manutenção
* Barulho
* Encomendas
* Portaria

---

## RF-008 — Solicitações

O sistema deve permitir criar solicitações contendo:

* categoria;
* título;
* descrição;
* unidade-alvo (opcional).

---

## RF-009 — Conversa

Cada solicitação deve possuir uma linha de conversa.

Usuários autorizados poderão enviar mensagens.

---

## RF-010 — Anexos

O sistema deve permitir anexar arquivos:

* na abertura da solicitação;
* durante a conversa.

---

## RF-011 — Status

O sistema deve permitir controlar o ciclo de vida das solicitações.

Status do MVP:

* Open
* InProgress
* WaitingForResident
* WaitingForThirdParty
* Resolved
* Cancelled

---

## RF-012 — Histórico

Toda alteração de status deve gerar um registro permanente.

---

## RF-013 — Prioridade

O síndico poderá alterar a prioridade das solicitações.

Prioridades:

* Normal
* High
* Urgent

---

## RF-014 — Tarefas

O sistema deve permitir criar tarefas internas.

Uma tarefa poderá:

* nascer de uma solicitação;
* ser criada independentemente.

---

## RF-015 — Dashboard

O sistema deverá apresentar indicadores básicos.

Exemplos:

* solicitações abertas;
* solicitações resolvidas;
* aguardando morador;
* aguardando terceiros;
* tarefas pendentes.

---

# 4. Requisitos Não Funcionais

## RNF-001

A aplicação deverá ser responsiva.

---

## RNF-002

A interface deverá funcionar como Progressive Web App (PWA).

---

## RNF-003

O sistema deverá utilizar autenticação baseada em ASP.NET Core Identity.

---

## RNF-004

A API deverá seguir arquitetura REST.

---

## RNF-005

O banco de dados será PostgreSQL.

---

## RNF-006

Toda a aplicação deverá ser executável via Docker Compose.

---

## RNF-007

A aplicação deverá utilizar TypeScript no frontend.

---

## RNF-008

O backend deverá utilizar ASP.NET Core Web API (.NET 10).

---

## RNF-009

O histórico das solicitações deve ser preservado.

---

## RNF-010

A aplicação deverá ser preparada para múltiplos condomínios (multi-tenant lógico).

---

# 5. Regras Gerais

* Todo dado pertence a um condomínio quando aplicável.
* O usuário possui identidade global.
* Os papéis dependem do condomínio.
* O vínculo com unidades é independente dos papéis.
* O autor da solicitação não precisa ser o morador da unidade envolvida.
* O síndico controla o ciclo de vida das solicitações.
* Solicitações e tarefas possuem ciclos independentes.

---

# 6. Fora do MVP

Os seguintes recursos não fazem parte da primeira versão:

* administradoras;
* fornecedores;
* prestadores;
* áreas comuns;
* elevadores;
* garagens;
* templates de resposta;
* fluxos guiados;
* formulários personalizados;
* checklists;
* base de conhecimento;
* notificações avançadas;
* integrações externas;
* inteligência artificial;
* SLAs;
* automações;
* subtarefas;
* comentários em tarefas;
* histórico de tarefas.

Esses recursos poderão ser adicionados futuramente conforme a evolução do produto.

---

# 7. Critério de sucesso do MVP

O MVP será considerado concluído quando um síndico conseguir:

1. cadastrar um condomínio;
2. cadastrar moradores e unidades;
3. organizar categorias;
4. receber solicitações dos moradores;
5. acompanhar a conversa;
6. controlar o status das solicitações;
7. registrar tarefas internas;
8. acompanhar o dashboard de atendimento.

Se essas atividades puderem ser realizadas de forma simples e consistente, o MVP terá atingido seu objetivo.
