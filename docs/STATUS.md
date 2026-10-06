# Status

> Atualizado pelo comando `/encerrar`. Lido pelo `/retomar`.

**Marco atual:** **M2** (carrinho, pedido, reserva atômica + testes de concorrência), a começar. **M1 concluído em 06/10/2026** (catálogo, cadastro, login/logout, admin; tudo no ar em https://keycapstore.duckdns.org). **M0 concluído em 01/10/2026.**
**Data de início da v1:** 30/09/2026
**Horas acumuladas:** M0: 7h (estimativa era 25–35h) · M1: 8,5h (estimativa era 15–25h) · total: 15,5h
**Estimativa da v1:** 120–180h (~3 meses a 10–15h/semana). Revisar ao fim do M2.

## Marcos da v1

- [x] **M0: esqueleto que anda** (25–35h estimadas; **7h reais**, concluído em 01/10/2026)
- [x] **M1:** catálogo (seed) + Identity + `create-admin` (15–25h estimadas; **8,5h reais**, concluído em 06/10/2026)
- [ ] **M2:** carrinho, pedido, reserva atômica + testes de concorrência (20–30h)
- [ ] **M3:** Stripe Checkout com cartão + webhook idempotente + página de retorno (20–30h)
- [ ] **M4:** Pix, expiração, falha, reembolso por falta de estoque (15–25h)
- [ ] **M5:** endpoints de admin para o envio (8–12h)
- [ ] **M6:** backups, monitoramento, checklist de go-live (15–20h)

## Checklist do M1

- [x] Entidade `Product` (`Domain/Catalog`): `private set`, construtor valida nome, preço > 0 e estoque ≥ 0; 9 testes unitários (PR #14)
- [x] `AppDbContext` + `ProductConfiguration` (Npgsql, EF Core 10.0.12 alinhado), migration `InitialCatalog`, `dotnet-ef` como ferramenta local (`dotnet-tools.json`), migrations marcadas como código gerado no `.editorconfig`; teste de integração salva e lê um `Product` (PR #15)
- [x] Consulta do catálogo: `ICatalogQueries` + `ProductSummary` (DTO, na Application) e `CatalogQueries` (Infrastructure), com teste de integração independente do seed (PR #16)
- [x] Seed dos 6 kits (migration `SeedCatalog`, Ids fixos) + teste que garante o seed (PR #18)
- [x] PostgreSQL de desenvolvimento: `compose.override.yaml` publica `127.0.0.1:5432` só no PC; conexão em user-secrets (`ConnectionStrings:Default`) (PR #18)
- [x] `AddInfrastructure` (DI, conexão obrigatória) + `CatalogController` fino + página `/Catalog` (preço em pt-BR, "Esgotado" / "Últimas N unidades") (PR #18)
- [x] Produção: `ConnectionStrings__Default` no `compose.prod.yaml` (senha do `.env`); comando `migrate` (`dotnet KeycapStore.Web.dll migrate`); `deploy.sh` roda `APP_TAG=<novo> docker compose run --rm -T app migrate` **antes** de trocar a versão. Primeiro deploy aplicou `InitialCatalog` + `SeedCatalog` na VM (PR #18)
- [x] Identity parte 1: `AppDbContext` herda de `IdentityDbContext<IdentityUser>` (`base.OnModelCreating` primeiro), migration `AddIdentity` (7 tabelas `AspNet...`, aplicada em produção pelo deploy), `AddIdentityCore` + `AddRoles` + `AddEntityFrameworkStores` no `AddInfrastructure` (e-mail único), teste de integração que prova o hash da senha (visto falhando com sabotagem) (PR #22)
- [x] Nome do `Product` com no máximo 150 caracteres (`Product.MaxNameLength`, usada também no `ProductConfiguration`); testes de borda 150/151 escritos antes da regra (PR #26)
- [x] Cadastro (PR #27): porta `IAccountService` + `AccountResult` (Application), `AccountService` com `UserManager` (Infrastructure), `PortugueseIdentityErrorDescriber` (e-mail duplicado com mensagem única, sem repetir o e-mail), `AccountController` + `RegisterViewModel` + `Views/Account/Register.cshtml`, antiforgery global (`AutoValidateAntiforgeryTokenAttribute`). Testes: serviço (válido, duplicado, senha fraca) e página (POST com token → 302 `/Catalog`; sem token → 400), com sabotagens. Primeira conta criada em produção.
- [x] Chaves do Data Protection no PostgreSQL (`IDataProtectionKeyContext`, migration `AddDataProtectionKeys`, `SetApplicationName`); teste que simula um deploy e confere a chave no banco; aviso `may not be persisted` sumiu da produção e a chave sobreviveu a um deploy (PR #29, ADR 0022 no PR #30)
- [x] Login (PR #31): `FrameworkReference` do ASP.NET na Infrastructure, `SignInManager`, `SignInAsync`/`SignOutAsync` na porta, bloqueio após **3** tentativas (5 min), mensagem genérica "E-mail ou senha inválidos.", cookie `HttpOnly` + `Secure` + `SameSite=Lax` (8h, renovável), `UseAuthentication`; testes de cookie e senha errada
- [x] Logout por POST + menu "Olá, e-mail"/"Sair" (PR #32); testes do bloqueio (2 erros, 3º bloqueia, senha certa barrada) e do ciclo login → menu → logout (cliente de teste em `https` por causa do `Secure`), com sabotagens
- [x] Comando `create-admin --email` (PR #33, ADR 0023): papel `Admin`, senha de 20 caracteres com `RandomNumberGenerator` mostrada uma vez, conta existente promovida sem trocar a senha, idempotente; 3 testes. Sem seed de admin (no PC usa-se o mesmo comando). Admin real criado na VM com e-mail separado (`+admin`)

## Checklist do M0

**Spike de premissas (fazer primeiro, 1–2h):**
- [x] Criar a conta Stripe e ativar o Pix no Dashboard (sandbox "Área restrita de KeycapStore", conta BR, Pix habilitado)
- [x] Gerar um pagamento Pix de teste e anotar quais eventos de webhook chegam (Q1, caminho de sucesso; ver "Resultado do spike" abaixo)
- [x] Testar o caminho de **falha do Pix** e anotar os eventos
- [ ] (M4) Confirmar `checkout.session.expired` com Pix pendente, numa sessão de 30 minutos criada pelo código (Q1b)
- [x] Criar a VM na Oracle (Q3 respondida: **sem capacidade A1** em São Paulo em 30/09). Criada a `keycapstore` como **E2.1.Micro** (x86_64, 1 GB), provisória (ADR 0020). Acesso: `ssh keycapstore` (alias em `~/.ssh/config`).
- [x] Confirmar os planos gratuitos do que o M0 usa. O restante (R2/B2, monitor externo, healthchecks.io) **passou para o M6**, onde é usado (Q4). Já confirmados: gitleaks-action gratuito para conta pessoal; GitHub Actions e rulesets no repositório público; GHCR gratuito ("currently free"). Runners ARM não são mais necessários enquanto durar a Micro (x86_64).

**Esqueleto:**
- [x] Solution `KeycapStore.slnx` com os 4 projetos + 2 de testes, `global.json` (SDK 10), `Directory.Build.props`, `.editorconfig` (`insert_final_newline = true`)
- [x] Um teste unitário (arquitetura: `Domain` sem dependências proibidas, visto falhando com sabotagem) e dois de integração (`WebApplicationFactory` + PostgreSQL 17 via Testcontainers) passando
- [x] Workflow de CI com os 5 itens (`.github/workflows/ci.yml`, actions fixadas por hash) + ruleset "Proteger main" (PR obrigatório, 2 checks, branch atualizado, sem bypass). PR #1 juntado com CI verde.
- [x] Dockerfile multi-stage (usuário sem privilégios) + `compose.yaml` (PostgreSQL sem porta, app em `127.0.0.1`, senha via `.env`). PR #3. Com a Micro (x86_64), o build ARM64 não é necessário por enquanto.
- [x] Script de reconstrução da VM `deploy/bootstrap-vm.sh`, idempotente, 5 de 5 partes (PRs #4 e #6): swap 2 GB; fuso de Brasília + atualizações + `unattended-upgrades` (reinício às 04:00); Docker + rotação de logs; portas 80/443 no iptables, SSH só por chave e sem root, fail2ban; Nginx + HTTPS (Let's Encrypt, `certonly` com configuração escrita pelo script, renovação testada com `--dry-run`, `server_tokens off`). Primeira execução exige `LETSENCRYPT_EMAIL`.
- [x] Subdomínio DuckDNS (`keycapstore.duckdns.org` → VM) + HTTPS com redirecionamento de http para https
- [x] CD completo: `publish-image` → GHCR com a etiqueta do hash do commit (PR #7); `compose.prod.yaml` com `image:` (PR #8); `deploy.sh` com validação do hash, pull antes de trocar, verificação do site e rollback (PR #10); usuário `deploy` com `sudo` só para o `deploy.sh` e chave com comando forçado + `restrict` (PR #11, testado: `ls /` recusado, sem terminal); job `deploy` com Environment `production` (revisor obrigatório, sem bypass de admin, só `main`, chave e `known_hosts` no cofre) (PR #12).
- [x] Página publicada **via CD com aprovação**: `APP_TAG=47591507...` (merge do PR #12) na VM. Reinício manual da VM testado: a loja volta sozinha.

### Resultado do spike da Stripe (30/09/2026, Payment Link no sandbox)

| Pagamento | Eventos observados (ordem de chegada) | `checkout.session.completed` |
|---|---|---|
| Cartão `4242` | `charge.succeeded`, `payment_intent.succeeded`, `checkout.session.completed`, `payment_intent.created`, `charge.updated` | `payment_status = paid` |
| Pix (simulado) | `payment_intent.requires_action`, `payment_intent.created`, `payment_intent.requires_action`, `payment_intent.succeeded`, `charge.succeeded`, `checkout.session.completed`, `charge.updated` | `payment_status = paid` |

**Conclusões:**
- No Checkout com Pix, `checkout.session.completed` **só chegou depois do Pix pago**, já com `paid`. **Não** chegou `completed` com `unpaid`, nem `async_payment_succeeded`. O cliente fica na página do Checkout com o QR até pagar.
- O design continua válido, porque o handler decide pelo `payment_status`. O ramo `unpaid` → `AwaitingConfirmation` e os eventos `async_payment_*` ficam como **defesa**, e podem nunca ocorrer com Pix no Checkout. Reavaliar no M4.
- **Eventos chegam fora de ordem, na prática:** no cartão, `payment_intent.created` chegou **depois** de `payment_intent.succeeded`.
- **Pix que falha ou expira** (teste 10:06): chegaram só `payment_intent.requires_action`, `payment_intent.created`, `payment_intent.payment_failed` e `charge.failed`. O PaymentIntent volta para `requires_payment_method`, e **a sessão continua `open` / `unpaid`**. O cliente volta ao Checkout ("Seu cartão foi recusado", uma mensagem genérica da Stripe mesmo para Pix) e pode tentar de novo. **Nenhum evento `checkout.session.*` é emitido.**
  - Consequência: uma falha de Pix **não** libera o estoque, e isso é o correto, porque o cliente ainda pode pagar na mesma sessão. A liberação acontece em `checkout.session.expired` (30 minutos no nosso código). A reserva máxima fica limitada pela expiração da sessão.
  - `async_payment_failed` → `Failed` também fica como defesa.
- Pendente (Q1b, para o M4, com o código definindo `expires_at` de 30 minutos): confirmar que uma sessão expirada com Pix pendente (`fill_never@…`) emite `checkout.session.expired`.

> **Plano B do M0:** se passar de 2 semanas, trocar o CD completo por um deploy manual simples e mover o endurecimento para o M6.

## Próximos passos

1. **Aquecimento (~30 min):** juntar o `CreateFactoryAsync` (hoje copiado em 6 classes de teste de integração) num lugar só (ex.: classe base ou método na `PostgresFixture`). Pronto quando: nenhuma classe de teste monta a `WebApplicationFactory` por conta própria e os 32 testes passam.
2. **M2, desenho:** reler `docs/DEFINICOES.md` (carrinho, pedido, reserva) + ADRs 0005, 0006 e 0008 e `.claude/rules/pagamentos.md` §4–§5; o autor propõe o `Order` (estados, `private set`, transições) e o Claude revisa. **O `Order` e a reserva são escritos pelo autor.**
3. **M2, carrinho:** carrinho no banco, por cliente, exige login (D34), com testes.

## Bloqueios

_Nenhum._

## Questões em aberto

- Q1b: confirmar `checkout.session.expired` com Pix pendente (M4)
- Q3: **respondida** (sem capacidade A1 em 30/09); seguir tentando para migrar da Micro
- Q4: R2/B2, monitor externo, healthchecks.io (M6)
- Q5: prazo legal de retenção dos pedidos (produção, não é código)
- Cookie do antiforgery sem `Secure` (o de login tem). Risco baixo; endurecer exige que os testes de página usem `https`.
- Senha do admin de produção apareceu no chat em 06/10; o autor optou por não trocar agora. **Trocar antes do go-live (checklist do M6).**
- O `migrate` no deploy mostra `libgssapi_krb5.so.2: cannot open shared object file` (aviso do driver do PostgreSQL; já aparecia antes; a migration roda normalmente). Investigar no M6.

**Decisões de 06/10:** bloqueio após **3** tentativas (não 5), ciente de que facilita bloquear a conta de outra pessoa (5 min); chaves no PostgreSQL (ADR 0022); admin pelo comando também no PC e conta existente promovida (ADR 0023).

**Decisões de 05/10 (2ª sessão):** os branches **não** são apagados no GitHub (só localmente, depois do merge), para guardar o histórico; o nome do `Product` limitado a 150 caracteres **é regra de negócio**; atualizações automáticas diárias (ADR 0021).

## Diário de sessões

### 06/10/2026: Data Protection, login, logout, `create-admin` e **M1 concluído** (2h)
- Chaves do Data Protection no PostgreSQL (PR #29) + ADR 0022 (PR #30). A sabotagem mostrou que, no PC, as chaves também vão para `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`: por isso o teste confere o **banco**. A chave `key-2d5f...` sobreviveu a um deploy.
- Login (PR #31), logout + menu (PR #32), `create-admin` (PR #33, ADR 0023). O Claude passou a testar cada passo numa cópia separada (scratchpad) antes de entregar as instruções.
- Tropeços: banco do PC sem a migration nova (`relation "DataProtectionKeys" does not exist`; resolvido com `-- migrate`); CI do PR #32 nunca disparou (resolvido fechando e reabrindo o PR).
- Decisões do autor: bloqueio em 3 tentativas; sem seed de admin; promover conta existente.
- **Segurança:** a senha do admin de produção foi colada no chat (texto + print); o autor decidiu não trocar agora. Fica no checklist do M6.
- Verificações: chave no banco × cookie no navegador (acertou com ajuste), "e-mail ou senha" genérico (acertou), POST no logout (sem resposta). O autor diz que nada ficou confuso.

### 05/10/2026 (3ª sessão): 150 caracteres e cadastro de clientes no ar (2h)
- Regra dos 150 caracteres no `Product` com TDD (vermelho → verde), constante `MaxNameLength` como fonte única (PR #26). Erros no caminho: `Name` (propriedade) em vez de `name.Length`; um `migrations remove` sem `add` antes, que só não apagou a `AddIdentity` porque não conectou no banco (usar `has-pending-model-changes` para conferir o modelo).
- Cadastro completo em 5 passos (PR #27): porta na Application, `AccountService`, página MVC com ViewModel, mensagens em pt-BR, teste da página com antiforgery. Arquivos `.cshtml.cs` de Razor Page que sobravam do modelo do VS foram apagados (usar "Razor View - Empty").
- Sabotagens: `RequireUniqueEmail = false` **não** fez o teste falhar, porque `UserName = email` tem índice **único** no banco (o `EmailIndex` não é único): é essa a garantia real. Sem o filtro de antiforgery, o POST sem token virou 302 (conta criada).
- Incidente do GitHub Actions travou o CI do PR #26 por horas; resolvido com "Re-run failed jobs" depois.
- Achado nos logs de produção: chaves do Data Protection dentro do container (mudam a cada deploy). Entra antes do login.
- Verificações: porta na Application (acertou depois do cardápio), `UserName` fixo (não sabia), navegador × servidor (parcial), 302 × 400 (trocou; o print mostrou `Found`). O autor diz que ficou sem dúvidas.

### 05/10/2026 (2ª sessão): Identity parte 1, deploy que se atualiza, atualizações automáticas (2h)
- Runner do CI fixado em `ubuntu-24.04` (PR #20); shebang que faltava no `bootstrap-vm.sh` (PR #21, inserido pelo Claude e conferido: sem BOM, LF, diff de 1 linha).
- Identity parte 1 (PR #22): `IdentityDbContext`, migration `AddIdentity` (aplicada na VM pelo deploy), `AddIdentityCore` no DI, teste do hash da senha com sabotagem. O `dotnet format` pegou um `using` fora de ordem antes do CI. Primeiro "Update branch" por causa da regra de branch atualizado.
- `deploy.sh` agora baixa o `compose.prod.yaml` e o próprio `deploy.sh` do GitHub, na mesma versão da imagem (depois do `docker pull`, em pasta temporária; o `deploy.sh` é trocado com `mv` só no final e só se o site responder) (PR #23). Ovo e galinha resolvido com uma última cópia manual; o deploy seguinte se atualizou sozinho.
- Reinício das 04:00 investigado: o robô só instalava segurança, e o kernel novo estava em `noble-updates`. Agora instala tudo (comuns + Docker) às 03:30, com horários fixos; o kernel `7.0.0-1013` está no ar após um reinício manual (PR #24, ADR 0021). O Claude tinha concluído errado de primeira ("nada pendente"); o banner do SSH mostrou o contrário.
- Git: rodou a rotina pós-merge antes do push (o `-d` recusou apagar, corretamente); explicado `-d` × `-D` e o `push -u`.
- Verificações: Scoped (errou, depois acertou com a analogia do carrinho), memória × banco (errou, depois acertou com o "Salvar" do Word), `mv` no final do `deploy.sh` (acertou depois da explicação). O autor diz que nada ficou confuso.

### 05/10/2026: catálogo completo, do banco até a produção (1h)
- Revisão: DTO (explicado do zero com a analogia do documento original × cópia) e o `DbContext` novo no teste; o autor respondeu bem às verificações.
- Seed dos 6 kits; o teste da consulta quebrou por depender de banco vazio e foi corrigido (filtra só os dados do próprio teste); teste novo para o seed.
- PostgreSQL de desenvolvimento (`compose.override.yaml`), user-secrets, `dotnet ef database update`, `psql` (o PowerShell 5.1 remove as aspas duplas de argumentos: usar o `psql` interativo).
- Injeção de dependência (`AddInfrastructure`), `SmokeTests` entregando a conexão do container (no CI não há user-secrets), `CatalogController` fino e a página.
- Produção: conexão via variável de ambiente, comando `migrate`, `deploy.sh` com migration antes da troca. **Catálogo no ar** (PR #18, deploy aprovado em 49 s).
- O autor apagava o `#!/usr/bin/env bash` achando que era comentário (3 vezes); explicado. Atenção a isso em todo script.

### 02/10/2026: revisão do deploy, Product, persistência e consulta do catálogo (1,5h)
- Revisão das 6 perguntas do deploy: acertou com ajuda as do comando forçado e da chave sem senha; as outras ficaram com correções.
- `Product` com invariantes e testes (PR #14). O primeiro teste do estoque zero estava com a regra invertida, e rodar o teste revelou o erro.
- `AppDbContext`, mapeamento, migration `InitialCatalog`, teste de integração que salva e lê (PR #15). Corrigidos: conflito de versões do EF Core (MSB3277) e o `dotnet format` reclamando das migrations.
- Consulta do catálogo (porta + DTO + teste de integração), commits no branch `feat/catalog-query`.
- Tropeços de editor: modelo do Visual Studio colado junto com o código (usar Ctrl+A antes de colar); arquivo com nome `FileName.cs` e depois `.cs.cs`.
- **Ficou confuso:** o que é um DTO. Revisar na próxima sessão.

### 01/10/2026 (2ª sessão): deploy contínuo completo e M0 concluído (1,5h)
- Reinício manual da VM: a loja voltou sozinha (Docker habilitado + `restart: unless-stopped`).
- `deploy.sh` (PR #10): subir de versão, rollback e recusa de valor inválido testados. Erro no caminho: faltava o `#!/usr/bin/env bash` e o arquivo estava em CRLF.
- Usuário `deploy` + comando forçado (PR #11): o teste com `ls /` foi recusado e nenhum terminal foi aberto.
- Job `deploy` com Environment `production` (PR #12). Na primeira vez, o revisor obrigatório não tinha sido salvo, e o deploy rodou sem aprovação; corrigido (revisor obrigatório, sem bypass de admin) e testado com "Re-run": parou em "waiting for review" e funcionou após a aprovação.
- **Ficou para revisar:** as perguntas da sessão (ver Próximos passos, item 1). O autor pediu para repassá-las.
- M0 fechado com 7h, bem abaixo da estimativa de 25–35h. Reavaliar a estimativa da v1 ao fim do M2, como planejado.

### 01/10/2026: revisão do bootstrap, DuckDNS, HTTPS, imagem no GHCR e loja no ar (1,5h)
- Revisão conceitual do bootstrap (swap, atualizações, Docker, firewall): o autor explicou as 4 partes, com correções pontuais.
- DuckDNS `keycapstore.duckdns.org`; Nginx + Let's Encrypt pelo script (partes 5a e 5b), redirecionamento para https e renovação testada (PR #6).
- CI publica a imagem no GHCR com a etiqueta do hash do commit (PR #7). A imagem nasceu pública (a documentação dizia "privada por padrão"; o teste mostrou o contrário).
- Compose de produção (PR #8) e primeiro deploy manual: a loja está no ar com HTTPS. Memória com tudo rodando: ~434 MB disponíveis + swap.
- Nova prática: uma pergunta de verificação a cada passo. Funcionou; ficaram poucas dúvidas.
- Lição de git: voltar para a `main` e dar `git pull` logo após cada merge.

### 30/09/2026 (tarde): revisão, Docker, VM e bootstrap (2h)
- Revisão guiada: referências entre projetos e fluxo dos testes (5 perguntas; dúvidas esclarecidas).
- Docker: Dockerfile + compose (PR #3).
- VM: A1 sem capacidade → E2.1.Micro provisória (ADR 0020). Alias `ssh keycapstore`.
- Bootstrap partes 1 a 4 rodadas na VM (PR #4): swap, fuso + atualizações, Docker, firewall/SSH/fail2ban. Idempotência comprovada. O fail2ban registrou uma tentativa de login de robô minutos após a VM ser criada.
- Aprendizado: CRLF quebra scripts bash; `.editorconfig` com `[*.sh] end_of_line = lf` resolve na origem.
- **Ficou confuso: praticamente toda a parte do script de bootstrap** (conceitos de Linux, firewall, SSH e bash). Causa provável: ritmo rápido demais no fim de uma sessão longa, com muitos conceitos novos. Revisar no início da próxima sessão (passo 1).

### 30/09/2026: ambiente, spike da Stripe, solution, testes e CI (2h)
- Ambiente: WSL 2, Docker Desktop, Stripe CLI (winget: o ID é `Stripe.StripeCli`, com "Cli").
- Spike da Stripe concluído (ver "Resultado do spike"): Pix no Checkout só gera `completed` depois de pago; falha de Pix não encerra a sessão; eventos fora de ordem observados na prática.
- Solution criada, com as referências definidas pelo autor. Teste de arquitetura (com sabotagem) e smoke tests de integração com Testcontainers.
- CI no GitHub Actions + ruleset na `main`. A partir de agora: **sempre branch + PR**.
- Aprendizados: `TreatWarningsAsErrors` pegou um construtor obsoleto do Testcontainers; `git restore` recuperou um `.csproj` que perdeu as referências; o workflow só roda dentro de `.github/workflows/`.
- **Ficou confuso:** as referências entre projetos e o que o código dos testes faz linha a linha (os conceitos ficaram claros). Revisar no início da próxima sessão.

### 29/09/2026: definição do projeto
- Entrevista de arquitetura concluída; `docs/DEFINICOES.md` gerado.
- Estrutura de contexto criada: `CLAUDE.md`, `.claude/rules/`, `/retomar`, `/encerrar`, subagente `revisor`, `ARQUITETURA.md`, ADRs.
- Nenhum código ainda.

## Histórico

_(sessões antigas resumidas em uma linha cada)_
