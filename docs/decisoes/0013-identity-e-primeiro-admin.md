# 0013. ASP.NET Core Identity com cookies; primeiro admin por comando

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D23, D24

## Contexto

A loja tem muitos clientes (cadastro, login, bloqueio por tentativas, recuperação de senha) e um papel de admin. A agenda do autor usa autenticação por cookie feita à mão, mas para **um** usuário. Alguém precisa ser o primeiro admin, e nenhuma rota pública pode conceder esse papel.

## Decisão

- **ASP.NET Core Identity com cookies** (`HttpOnly`, `Secure`, `SameSite`), com armazenamento no PostgreSQL via EF Core, na `Infrastructure`. O `Domain` conhece o cliente só pelo ID.
- O admin é um usuário do Identity com o **papel `Admin`**, sem tabela separada e sem flag `IsAdmin`.
- **Primeiro admin por comando administrativo:** `docker compose exec app dotnet KeycapStore.Web.dll create-admin --email ...`, com a senha pedida de forma interativa ou gerada e exibida uma única vez. **Nenhuma senha fica guardada.**
- ~~Em `Development`, um seed cria um admin de teste. Em nenhum outro ambiente isso roda.~~ **Substituído pelo ADR 0023:** no PC também se usa o comando `create-admin`; não há seed.
- **Nenhuma rota atribui o papel de admin.**

## Alternativas consideradas

- **Autenticação feita à mão:** hash de senha, bloqueio e tokens de recuperação reimplementados, que são lugares clássicos para brechas.
- **Login externo (Google, Auth0, Entra):** dependência externa sem ganho de aprendizado na v1.
- **Promover o admin com SQL manual em produção:** não reprodutível e sujeito a erro.
- **Seed a partir de variável de ambiente:** a senha ficaria em texto puro no servidor.

## Consequências

- **Positivas:** segurança auditada pelo framework; papéis prontos; o processo de criar admin é explícito e documentado.
- **Negativas:** tabelas e conceitos do Identity para aprender; um comando de linha de comando a manter.
- **Como saber se deu errado:** qualquer caminho, via HTTP, que conceda o papel `Admin`.
