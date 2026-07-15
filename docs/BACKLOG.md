# CondoLink — Backlog

## Objetivo

Este documento registra ideias, funcionalidades futuras e melhorias que não fazem parte do MVP atual.

O backlog não representa compromisso de implementação.

Os itens devem ser priorizados com base em:

* valor para o síndico;
* valor para o morador;
* frequência do problema;
* redução de trabalho manual;
* complexidade;
* impacto no produto.

---

# 1. Atendimento e solicitações

## Templates de resposta

Permitir que o condomínio crie respostas reutilizáveis.

Exemplos:

```text
Encomenda não encontrada
Barulho de obra
Mudança
Cadastro na portaria
Tag veicular
Reserva de salão
```

Possível estrutura futura:

```text
ResponseTemplate
- Id
- CondominiumId
- CategoryId?
- Title
- Content
- IsActive
```

---

## Fluxos guiados por categoria

Antes da criação de uma solicitação, o sistema poderá fazer perguntas específicas.

Exemplo para encomenda não encontrada:

```text
Verificou o aplicativo da portaria?
Verificou o e-mail?
Conferiu a bancada da guarita?
Possui comprovante de entrega?
```

Objetivo:

* coletar informações antes do atendimento;
* evitar perguntas repetidas;
* reduzir solicitações incompletas;
* acelerar a resolução.

---

## Formulários personalizados

Permitir campos diferentes de acordo com a categoria.

Exemplos:

```text
Unidade envolvida
Data do ocorrido
Horário
Transportadora
Número do pedido
Foto
Vídeo
Comprovante
```

---

## Checklists

Permitir que uma solicitação possua etapas obrigatórias ou recomendadas.

Exemplo:

```text
Consultar aplicativo
Verificar e-mail
Conferir guarita
Anexar comprovante
Aguardar análise
```

---

## Respostas automáticas iniciais

Enviar uma orientação automática após a abertura de solicitações de determinadas categorias.

Exemplo:

```text
Categoria: Encomendas

Resposta automática:
Antes de iniciar a investigação, siga estas verificações.
```

---

## Encerramento automático por inatividade

Permitir configurar encerramento ou lembretes após determinado período sem resposta.

Possíveis comportamentos:

* enviar lembrete ao morador;
* avisar o síndico;
* sugerir cancelamento;
* encerrar após prazo configurável.

Não implementar sem avaliar cuidadosamente os riscos de encerrar solicitações relevantes.

---

## Confirmação de resolução pelo morador

Permitir que o morador confirme se o problema foi resolvido.

Possível evolução de status:

```text
Resolved
Closed
Reopened
```

Essa funcionalidade não deve retirar do síndico o controle operacional da solicitação.

---

## Reações ou respostas rápidas

Permitir respostas simples como:

```text
Confirmado
Recebido
Resolvido
Obrigado
```

Objetivo:

* reduzir mensagens repetitivas;
* facilitar confirmações;
* melhorar a experiência em dispositivos móveis.

---

# 2. Encomendas

## Fluxo de encomenda não encontrada

Criar um fluxo específico para encomendas marcadas como entregues, mas não localizadas.

Possíveis etapas:

1. verificar o aplicativo da portaria;
2. verificar o e-mail;
3. conferir o armário inteligente;
4. conferir a bancada da guarita;
5. anexar comprovante de entrega;
6. informar data, horário e transportadora;
7. abrir investigação com a portaria.

---

## Registro estruturado de encomendas

Permitir armazenar:

```text
Transportadora
Código de rastreio
Data da entrega
Horário da entrega
Foto do comprovante
Local informado
Nome de quem recebeu
```

---

## Integração com armário inteligente

Possível integração futura com sistemas de armário inteligente.

Objetivos:

* consultar depósitos;
* verificar códigos de retirada;
* associar encomendas a moradores;
* identificar compartimentos;
* consultar histórico.

---

## Integração com portaria remota

Possível integração com empresas de portaria.

Objetivos:

* consultar registros;
* validar entregas;
* obter eventos;
* cadastrar moradores;
* consultar acessos.

---

# 3. Base de conhecimento

## Artigos de ajuda

Permitir que cada condomínio publique orientações.

Exemplos:

```text
Como realizar uma mudança
Como cadastrar reconhecimento facial
Como solicitar tag veicular
Como reservar o salão
O que fazer em caso de encomenda não encontrada
```

---

## Pesquisa antes da abertura

Ao começar uma solicitação, o sistema poderá sugerir artigos relacionados.

Objetivo:

* resolver dúvidas sem intervenção do síndico;
* reduzir solicitações repetidas;
* orientar o morador antes da abertura.

---

## Perguntas frequentes

Permitir uma seção de perguntas frequentes organizada por categoria.

---

# 4. Tarefas

## Comentários em tarefas

Permitir registrar observações internas em uma tarefa.

---

## Histórico de tarefas

Registrar:

* mudanças de status;
* alterações de responsável;
* mudanças de prazo;
* mudanças de prioridade.

---

## Subtarefas

Permitir dividir uma tarefa em atividades menores.

Exemplo:

```text
Resolver vazamento

- solicitar orçamento;
- aprovar orçamento;
- agendar visita;
- acompanhar execução;
- arquivar nota fiscal.
```

---

## Tarefas recorrentes

Permitir tarefas com repetição.

Exemplos:

```text
Verificar extintores mensalmente
Acompanhar manutenção do elevador
Revisar iluminação semanalmente
```

---

## Dependências entre tarefas

Permitir que uma tarefa dependa da conclusão de outra.

---

## Lembretes de prazo

Enviar notificações antes do vencimento.

---

## Tarefas vencidas

Criar visualização específica para tarefas atrasadas.

---

# 5. Fornecedores e terceiros

## Cadastro de prestadores

Possível entidade futura:

```text
ServiceProvider
```

Dados possíveis:

```text
Nome
Empresa
Telefone
E-mail
Serviço prestado
Observações
Status
```

---

## Cadastro de empresas

Exemplos:

* administradora;
* portaria remota;
* manutenção de elevador;
* eletricista;
* encanador;
* empresa de segurança.

---

## Atribuição de solicitação a terceiro

Permitir registrar qual empresa ou prestador está atendendo uma solicitação.

---

## Histórico de atendimento de fornecedores

Registrar:

* solicitações atendidas;
* prazos;
* retornos;
* avaliações;
* documentos;
* orçamentos.

---

## Solicitação de orçamento

Criar fluxo para solicitar, receber e comparar orçamentos.

---

# 6. Administradoras

## Entidade ManagementCompany

Representar administradoras de condomínio.

---

## Usuários da administradora

Permitir que funcionários da administradora participem de vários condomínios.

---

## Papel Administrator

Adicionar papel específico para administradoras.

---

## Visão consolidada

Permitir que a administradora visualize os condomínios sob sua gestão.

---

## Aprovações e documentos

Criar fluxos de:

* pagamento;
* orçamento;
* documentação;
* prestação de contas.

---

# 7. Estrutura física do condomínio

## Blocos como entidade

Criar entidade própria para blocos ou torres.

---

## Áreas comuns

Possível entidade:

```text
CommonArea
```

Exemplos:

```text
Salão de festas
Academia
Piscina
Garagem
Guarita
Área gourmet
```

---

## Equipamentos

Possível entidade:

```text
Equipment
```

Exemplos:

```text
Elevador
Portão
Bomba
Interfone
Câmera
Armário inteligente
```

---

## Alvo genérico da solicitação

Evoluir o campo atual de unidade-alvo para permitir:

```text
Unit
Block
CommonArea
Equipment
Condominium
```

Essa evolução deve evitar relacionamentos polimórficos frágeis.

---

# 8. Notificações

## Notificações no sistema

Criar uma central de notificações.

---

## Notificações por e-mail

Exemplos:

* nova solicitação;
* nova mensagem;
* mudança de status;
* tarefa próxima do vencimento.

---

## Push notifications

Enviar notificações pela PWA.

---

## WhatsApp

Avaliar integração oficial com WhatsApp.

Possíveis usos:

* avisos;
* atualizações de solicitação;
* lembretes;
* confirmação de ações.

A integração deve respeitar custos, consentimento e regras da plataforma.

---

## Preferências de notificação

Permitir que o usuário escolha quais notificações deseja receber.

---

# 9. Dashboard e relatórios

## Indicadores avançados

Exemplos:

```text
Tempo médio de primeira resposta
Tempo médio de resolução
Solicitações por categoria
Solicitações por unidade
Solicitações reabertas
Solicitações sem resposta
```

---

## Tendências

Exibir evolução ao longo do tempo.

---

## Relatórios por período

Permitir filtros por:

* data;
* categoria;
* status;
* prioridade;
* unidade;
* responsável.

---

## Exportação

Exportar dados em:

```text
CSV
Excel
PDF
```

---

## Relatório para assembleia

Gerar resumo de gestão com:

* solicitações atendidas;
* principais problemas;
* tarefas concluídas;
* melhorias realizadas;
* demandas pendentes.

---

# 10. SLA e prazos

## SLA por categoria

Permitir definir prazo esperado para primeira resposta e resolução.

---

## Alertas de SLA

Avisar quando uma solicitação estiver próxima do limite.

---

## Pausa de SLA

Avaliar pausa do prazo quando estiver:

```text
WaitingForResident
WaitingForThirdParty
```

---

## Indicadores de cumprimento

Exibir percentual de solicitações atendidas dentro do prazo.

---

# 11. Automações

## Automação por categoria

Exemplo:

```text
Categoria: Encomendas
→ enviar orientação inicial;
→ solicitar comprovante;
→ criar tarefa de investigação.
```

---

## Criação automática de tarefa

Permitir que determinadas solicitações gerem tarefas.

---

## Mudança automática de status

Exemplos possíveis:

```text
Morador respondeu
WaitingForResident → InProgress
```

```text
Terceiro respondeu
WaitingForThirdParty → InProgress
```

A automação não deve ocorrer quando houver risco de alterar incorretamente o estado.

---

## Regras condicionais

Exemplo:

```text
Se prioridade = Urgent
→ notificar todos os gestores.
```

---

## Lembretes automáticos

Enviar lembrete quando não houver resposta.

---

# 12. Inteligência artificial

## Classificação de categoria

Sugerir categoria com base na descrição.

---

## Sugestão de prioridade

Sugerir prioridade sem alterar automaticamente.

---

## Respostas sugeridas

Gerar rascunhos com base em:

* categoria;
* histórico;
* templates;
* base de conhecimento.

---

## Resumo de conversa

Gerar resumo de solicitações longas.

---

## Extração de informações

Identificar automaticamente:

* unidade;
* data;
* horário;
* empresa;
* tipo de problema.

---

## Identificação de duplicidade

Sugerir que uma nova solicitação pode estar relacionada a outra já aberta.

---

## Análise de tendências

Identificar problemas recorrentes.

Exemplo:

```text
Aumento de reclamações sobre o portão nas últimas semanas.
```

Toda funcionalidade de IA deve manter revisão humana e não deve tomar decisões críticas sozinha.

---

# 13. Pesquisa e experiência do usuário

## Busca global

Pesquisar por:

* solicitações;
* unidades;
* moradores;
* tarefas;
* mensagens.

---

## Filtros salvos

Permitir salvar combinações de filtros.

---

## Favoritos

Permitir destacar solicitações ou tarefas importantes.

---

## Atalhos

Criar ações rápidas para operações frequentes.

---

## Modo escuro

Adicionar tema escuro.

---

## Acessibilidade

Melhorar suporte a:

* teclado;
* leitores de tela;
* contraste;
* tamanhos de fonte.

---

# 14. Permissões

## Novos papéis

Possíveis papéis:

```text
CouncilMember
Administrator
Employee
Concierge
Auditor
```

---

## Permissões granulares

Exemplos:

```text
Visualizar solicitações
Alterar status
Gerenciar moradores
Gerenciar categorias
Visualizar relatórios
Criar tarefas
```

---

## Acesso por categoria

Permitir restringir determinados usuários a categorias específicas.

---

## Solicitações privadas

Permitir solicitações visíveis apenas para grupos autorizados.

Exemplos:

```text
Financeiro
Jurídico
Conflitos entre moradores
```

---

# 15. Segurança e auditoria

## Auditoria geral

Registrar alterações relevantes.

Exemplos:

* edição de cadastro;
* mudança de papel;
* desativação de usuário;
* mudança de responsável;
* alteração de configuração.

---

## Sessões e dispositivos

Permitir visualizar e encerrar sessões ativas.

---

## Autenticação em dois fatores

Adicionar 2FA.

---

## Política de retenção

Definir retenção para:

* anexos;
* logs;
* mensagens;
* dados pessoais.

---

## Adequação à LGPD

Avaliar:

* consentimento;
* finalidade;
* acesso;
* correção;
* anonimização;
* exclusão quando legalmente possível;
* exportação de dados.

---

# 16. Integrações

## E-mail

Integração para envio e recebimento de mensagens.

---

## Calendário

Criar eventos para:

* visitas;
* manutenções;
* mudanças;
* prazos.

---

## Sistemas de administradoras

Avaliar integrações com ERPs condominiais.

---

## Controle de acesso

Possíveis integrações com:

* portaria remota;
* reconhecimento facial;
* tags;
* visitantes;
* veículos.

---

## Webhooks

Permitir que sistemas externos recebam eventos do CondoLink.

---

## API pública

Avaliar disponibilização de API para integrações autorizadas.

---

# 17. Infraestrutura e arquitetura

## Armazenamento em nuvem

Migrar anexos para serviço de objetos.

---

## Processamento assíncrono

Adicionar fila quando houver necessidade de:

* envio de notificações;
* geração de relatórios;
* processamento de arquivos;
* integrações.

---

## Eventos de domínio

Avaliar eventos quando existirem múltiplas ações após uma alteração.

---

## Cache

Adicionar somente quando houver gargalos reais.

---

## Observabilidade

Evoluir para:

* métricas;
* tracing;
* alertas;
* dashboards técnicos.

---

## Isolamento por cliente

Avaliar banco ou schema separado apenas se houver necessidade técnica, contratual ou regulatória.

---

# 18. PWA e mobilidade

## Push notifications

Habilitar notificações no dispositivo.

---

## Compartilhamento

Permitir abrir solicitação por compartilhamento de foto ou arquivo.

---

## Câmera

Facilitar envio direto de fotos e vídeos.

---

## Funcionamento offline parcial

Permitir rascunhar solicitações sem conexão.

Sincronização offline completa não é prioridade inicial.

---

## Aplicativos nativos

Avaliar apenas se a PWA não atender às necessidades reais.

---

# 19. Comercial e SaaS

## Planos

Criar diferentes planos de assinatura.

---

## Limites por plano

Possíveis limites:

```text
Número de unidades
Número de usuários
Armazenamento
Relatórios
Automações
Integrações
```

---

## Período de teste

Permitir trial.

---

## Cobrança

Integrar meios de pagamento.

---

## Onboarding

Criar fluxo guiado para configurar:

* condomínio;
* unidades;
* moradores;
* categorias;
* gestores.

---

## Importação de dados

Permitir importar usuários e unidades por planilha.

---

# 20. Critérios de priorização

Antes de mover um item para implementação, avaliar:

1. O problema acontece com frequência?
2. A funcionalidade reduz trabalho manual?
3. Ela melhora claramente a experiência do usuário?
4. Existe evidência de que clientes precisam disso?
5. Pode ser resolvido de forma mais simples?
6. A funcionalidade exige novas entidades?
7. Ela aumenta significativamente a manutenção?
8. Ela pertence ao núcleo do produto?
9. Existe dependência de integração externa?
10. O benefício justifica a complexidade?

---

# 21. Prioridade inicial sugerida

## Curto prazo após o MVP

```text
Templates de resposta
Filtros e busca
Notificações no sistema
Relatórios básicos
Importação de unidades e moradores
Melhorias no fluxo de encomendas
```

## Médio prazo

```text
Fluxos guiados
Base de conhecimento
Tarefas recorrentes
Fornecedores
SLA
Push notifications
Auditoria
```

## Longo prazo

```text
Administradoras
Integrações externas
Automações avançadas
Inteligência artificial
Cobrança SaaS
API pública
```

A prioridade real deverá ser revisada após validação do MVP com usuários.
