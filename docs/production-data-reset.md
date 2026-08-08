# Reset controlado de dados de produção

Esta ferramenta apaga os dados cadastrados e operacionais sem recriar o banco. Ela não é chamada no startup, não possui endpoint e exige uma ação manual explícita.

## Pré-requisitos

- Pare ou coloque em manutenção todas as instâncias da API e workers que escrevem no banco.
- Confirme que o e-mail normalizado identifica exatamente um usuário ativo no banco e que ele possui a role `PlatformAdmin`.
- Disponibilize `ConnectionStrings__DefaultConnection` e `RESET_PRESERVE_USER_EMAIL` no shell da execução, sem registrar seus valores no histórico. Alternativamente, use as opções equivalentes da CLI.
- Execute a ferramenta a partir da mesma revisão da aplicação implantada.

## Backup no PostgreSQL/Coolify

Antes do reset, obtenha no Coolify os dados de conexão do PostgreSQL e faça um dump em formato customizado, por exemplo:

```sh
pg_dump --format=custom --no-owner --no-acl --dbname="$DATABASE_URL" --file=condolink-before-reset.dump
pg_restore --list condolink-before-reset.dump
```

Guarde o dump fora do container efêmero e valide seu tamanho/listagem. A ferramenta não automatiza nem verifica o conteúdo do backup; `--backup-confirmed` é uma confirmação operacional obrigatória.

## Dry-run

```sh
dotnet run --project backend/CondoLink.DataReset -- --dry-run
```

O resultado contém somente contagens por tipo, sem e-mails, telefones, tokens ou outros dados pessoais.

## Execução

```sh
dotnet run --project backend/CondoLink.DataReset -- --execute --backup-confirmed
```

O reset preserva o usuário indicado (incluindo hash, stamps, claims, logins e tokens), todas as roles e apenas sua atribuição `PlatformAdmin`. Apaga suas atribuições de condomínio, unidade e empresa, seu contexto de condomínio ativo e qualquer outra atribuição de role. Todos os demais usuários e respectivos dados Identity são apagados por cascata.

Também são apagados condomínios, administradoras, funcionários/categorias de administradoras, blocos, unidades, categorias, memberships e roles de membership, solicitações, históricos, mensagens, anexos, análises de IA, requisitos de resposta, notificações e todos os registros de sessão, entrada, saída, rascunho e verificação do WhatsApp.

Migrations (`__EFMigrationsHistory`), schema, roles/claims de roles, chaves de Data Protection e configuração externa do Coolify não são alterados. As entidades usam GUID; as únicas sequences são de claims Identity e são preservadas.

## Verificações pós-reset

1. Confira as contagens impressas e rode novamente o dry-run; todas as categorias removíveis devem estar zeradas.
2. Inicie a API e autentique com o usuário preservado.
3. Confirme acesso ao Overwatch e presença exclusiva da role `PlatformAdmin` nesse usuário.
4. Confirme que não há condomínios, administradoras, usuários adicionais ou filas WhatsApp.
5. Recadastre os dados somente após essas verificações.

## Rollback

Se houver qualquer resultado inesperado, mantenha a aplicação parada, restaure o dump em um banco vazio/controlado conforme o procedimento operacional do Coolify e aponte a aplicação novamente para o banco restaurado. Exemplo conceitual:

```sh
pg_restore --clean --if-exists --no-owner --no-acl --dbname="$DATABASE_URL" condolink-before-reset.dump
```

Valide a restauração antes de liberar tráfego.
