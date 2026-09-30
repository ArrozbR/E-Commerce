# Status

> Atualizado pelo comando `/encerrar`. Lido pelo `/retomar`.

**Marco atual:** M0 (esqueleto que anda), em andamento: 6 de 13 itens (sem contar o item do M4)
**Data de início da v1:** 30/09/2026
**Horas acumuladas:** M0: 2h · total: 2h
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
- [ ] Criar a VM Ampere A1 na Oracle (ex.: 1 OCPU / 6 GB) e confirmar a capacidade na região (Q3)
- [ ] Confirmar os planos gratuitos: runners ARM do GitHub, R2/B2, monitor externo, healthchecks.io (Q4). Já confirmados: gitleaks-action gratuito para conta pessoal; GitHub Actions e rulesets no repositório público.

**Esqueleto:**
- [x] Solution `KeycapStore.slnx` com os 4 projetos + 2 de testes, `global.json` (SDK 10), `Directory.Build.props`, `.editorconfig` (`insert_final_newline = true`)
- [x] Um teste unitário (arquitetura: `Domain` sem dependências proibidas, visto falhando com sabotagem) e dois de integração (`WebApplicationFactory` + PostgreSQL 17 via Testcontainers) passando
- [x] Workflow de CI com os 5 itens (`.github/workflows/ci.yml`, actions fixadas por hash) + ruleset "Proteger main" (PR obrigatório, 2 checks, branch atualizado, sem bypass). PR #1 juntado com CI verde.
- [ ] Dockerfile ARM64 + `compose.yaml` (PostgreSQL sem porta, app em `127.0.0.1`)
- [ ] Script de reconstrução da VM (Docker, Nginx, Certbot, usuário `deploy`, hardening, `unattended-upgrades`), executado de verdade
- [ ] Subdomínio DuckDNS + HTTPS
- [ ] CD: imagem no GHCR, Environment `production` com aprovação, `deploy.sh` com comando forçado
- [ ] Página "Olá" publicada via CD

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

1. **Revisão (20 min):** reler com calma as referências entre projetos (quem referencia quem e por quê, incluindo o motivo do `UnitTests` referenciar todas as camadas) e o código dos 3 testes, **linha por linha** (o que cada linha faz), explicando em voz alta. Foi o ponto confuso da sessão de 30/09.
2. **Dockerfile ARM64 + `compose.yaml`** local (PostgreSQL sem porta publicada, app só em `127.0.0.1`). Tudo via branch + PR.
3. **Criar a VM A1 na Oracle** (Q3) e começar o script de reconstrução.

## Bloqueios

_Nenhum._

## Questões em aberto

- Q1b: confirmar `checkout.session.expired` com Pix pendente (M4)
- Q3: capacidade A1 na região
- Q4: runners ARM do GitHub, R2/B2, monitor externo, healthchecks.io
- Q5: prazo legal de retenção dos pedidos (produção, não é código)

## Diário de sessões

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
