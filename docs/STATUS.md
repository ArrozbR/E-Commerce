# Status

> Atualizado pelo comando `/encerrar`. Lido pelo `/retomar`.

**Marco atual:** M0 (esqueleto que anda), em andamento: 10 de 13 itens concluídos, 3 parciais (sem contar o item do M4). **A loja está no ar:** https://keycapstore.duckdns.org (deploy ainda manual).
**Data de início da v1:** 30/09/2026
**Horas acumuladas:** M0: 5,5h · total: 5,5h
**Estimativa da v1:** 120–180h (~3 meses a 10–15h/semana). Revisar ao fim do M2.

## Marcos da v1

- [ ] **M0: esqueleto que anda** (25–35h, timebox de 2 semanas)
- [ ] **M1:** catálogo (seed) + Identity + `create-admin` (15–25h)
- [ ] **M2:** carrinho, pedido, reserva atômica + testes de concorrência (20–30h)
- [ ] **M3:** Stripe Checkout com cartão + webhook idempotente + página de retorno (20–30h)
- [ ] **M4:** Pix, expiração, falha, reembolso por falta de estoque (15–25h)
- [ ] **M5:** endpoints de admin para o envio (8–12h)
- [ ] **M6:** backups, monitoramento, checklist de go-live (15–20h)

## Checklist do M0

**Spike de premissas (fazer primeiro, 1–2h):**
- [x] Criar a conta Stripe e ativar o Pix no Dashboard (sandbox "Área restrita de KeycapStore", conta BR, Pix habilitado)
- [x] Gerar um pagamento Pix de teste e anotar quais eventos de webhook chegam (Q1, caminho de sucesso; ver "Resultado do spike" abaixo)
- [x] Testar o caminho de **falha do Pix** e anotar os eventos
- [ ] (M4) Confirmar `checkout.session.expired` com Pix pendente, numa sessão de 30 minutos criada pelo código (Q1b)
- [x] Criar a VM na Oracle (Q3 respondida: **sem capacidade A1** em São Paulo em 30/09). Criada a `keycapstore` como **E2.1.Micro** (x86_64, 1 GB), provisória (ADR 0020). Acesso: `ssh keycapstore` (alias em `~/.ssh/config`).
- [ ] Confirmar os planos gratuitos: runners ARM do GitHub, R2/B2, monitor externo, healthchecks.io (Q4). Já confirmados: gitleaks-action gratuito para conta pessoal; GitHub Actions e rulesets no repositório público; GHCR gratuito ("currently free"). Runners ARM não são mais necessários enquanto durar a Micro (x86_64).

**Esqueleto:**
- [x] Solution `KeycapStore.slnx` com os 4 projetos + 2 de testes, `global.json` (SDK 10), `Directory.Build.props`, `.editorconfig` (`insert_final_newline = true`)
- [x] Um teste unitário (arquitetura: `Domain` sem dependências proibidas, visto falhando com sabotagem) e dois de integração (`WebApplicationFactory` + PostgreSQL 17 via Testcontainers) passando
- [x] Workflow de CI com os 5 itens (`.github/workflows/ci.yml`, actions fixadas por hash) + ruleset "Proteger main" (PR obrigatório, 2 checks, branch atualizado, sem bypass). PR #1 juntado com CI verde.
- [x] Dockerfile multi-stage (usuário sem privilégios) + `compose.yaml` (PostgreSQL sem porta, app em `127.0.0.1`, senha via `.env`). PR #3. Com a Micro (x86_64), o build ARM64 não é necessário por enquanto.
- [x] Script de reconstrução da VM `deploy/bootstrap-vm.sh`, idempotente, 5 de 5 partes (PRs #4 e #6): swap 2 GB; fuso de Brasília + atualizações + `unattended-upgrades` (reinício às 04:00); Docker + rotação de logs; portas 80/443 no iptables, SSH só por chave e sem root, fail2ban; Nginx + HTTPS (Let's Encrypt, `certonly` com configuração escrita pelo script, renovação testada com `--dry-run`, `server_tokens off`). Primeira execução exige `LETSENCRYPT_EMAIL`.
- [x] Subdomínio DuckDNS (`keycapstore.duckdns.org` → VM) + HTTPS com redirecionamento de http para https
- [ ] CD. **Parcial:** ✅ job `publish-image` publica `ghcr.io/arrozbr/keycapstore-web:<hash do commit>` após os testes na `main` (PR #7; imagem pública); ✅ `deploy/compose.prod.yaml` usando `image:` (PR #8), com `.env` (senha do banco + `APP_TAG`) em `/opt/keycapstore` na VM; ⬜ `deploy.sh` + usuário `deploy` com comando forçado; ⬜ job de deploy com Environment `production` e aprovação manual.
- [ ] Página "Olá" publicada **via CD** (já publicada **manualmente** em 01/10)

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

1. **Conferir se a loja sobreviveu ao reinício automático das 04:00** (havia "System restart required"): abrir o site e rodar `sudo docker compose ps` em `/opt/keycapstore`.
2. **CD, parte A:** `deploy.sh` (troca o `APP_TAG`, faz `pull` e `up -d`) e o usuário `deploy` com comando forçado, criados pelo bootstrap.
3. **CD, parte B:** job de deploy no GitHub Actions, com Environment `production` (aprovação manual) e a chave SSH guardada só no Environment. Aproveitar para fixar o runner em `ubuntu-24.04` e ligar "Automatically delete head branches".

## Bloqueios

_Nenhum._

## Questões em aberto

- Q1b: confirmar `checkout.session.expired` com Pix pendente (M4)
- Q3: **respondida** (sem capacidade A1 em 30/09); seguir tentando para migrar da Micro
- Q4: R2/B2, monitor externo, healthchecks.io (para o M6)
- O `ubuntu-latest` dos runners do GitHub migra para o Ubuntu 26 a partir de 19/10/2026: avaliar fixar `ubuntu-24.04`
- Q5: prazo legal de retenção dos pedidos (produção, não é código)

## Diário de sessões

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
