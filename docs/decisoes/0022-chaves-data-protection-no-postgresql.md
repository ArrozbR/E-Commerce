# 0022. Chaves do Data Protection guardadas no PostgreSQL

- **Status:** Aceito
- **Data:** 2026-10-06
- **Relacionado:** ADR 0012 (VM descartável, Docker Compose), ADR 0013 (Identity com cookies), ADR 0014 (segredos), ADR 0018 (backups)

## Contexto

O ASP.NET Core usa o **Data Protection** para lacrar o cookie de login e os tokens do antiforgery. Sem configuração, as chaves ficam numa pasta **dentro do container** (`/home/app/.aspnet/DataProtection-Keys`), e o log de produção avisava: `Storing keys in a directory ... that may not be persisted outside of the container`.

Cada deploy **troca** o container, e o novo cria outra chave. Com o login (M1), todo deploy desconectaria todos os clientes. Já hoje, um formulário aberto durante um deploy é recusado ao ser enviado.

## Decisão

**As chaves ficam na tabela `DataProtectionKeys` do PostgreSQL**, pelo pacote oficial `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (gratuito).

- `AppDbContext` implementa `IDataProtectionKeyContext`; migration `AddDataProtectionKeys`.
- `AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("KeycapStore")` no `AddInfrastructure`.
- Teste de integração que simula um deploy (dois "containers" no mesmo banco) e confere que a chave está **no banco**: no PC, as chaves também iriam para `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`, e abrir o texto sozinho não provaria nada.
- **As chaves não são criptografadas em repouso** (sem `ProtectKeysWith...`). O log de produção continua com `No XML encryptor configured`, **de propósito**.

## Alternativas consideradas

- **Volume do Docker para a pasta das chaves:** simples, mas fica preso à VM (que é descartável, ADR 0012), fora do backup do banco, e exige acertar permissões para o usuário sem privilégios do container.
- **Redis ou armazenamento em nuvem (Azure Blob etc.):** Redis é proibido pelas regras de arquitetura; nuvem paga ou mais um serviço externo, contra a regra de custo zero.
- **Criptografar as chaves com certificado:** cria **mais um segredo** para guardar e girar (o certificado), sem ganho proporcional na v1.

## Consequências

- **Positivas:** cookies e tokens sobrevivem a deploys e a reinícios; a chave entra no backup do banco e volta junto numa reconstrução da VM.
- **Negativas / custos:** quem lê o banco (ou um backup) consegue ler a chave e forjar cookies. Mitigação: o PostgreSQL não publica porta (só a rede do Compose), e os backups do M6 são criptografados com `age` (ADR 0018). Mais uma tabela e uma dependência para manter.
- **Como saber se deu errado:** clientes deslogados depois de um deploy; o aviso `may not be persisted` voltando ao log; o `FriendlyName` da chave mudando entre deploys sem que tenham passado ~90 dias (a validade padrão de uma chave).
