# Status

> Atualizado pelo comando `/encerrar`. Lido pelo `/retomar`.

**Marco atual:** M0 (esqueleto que anda), ainda não iniciado
**Data de início da v1:** _(preencher no primeiro dia de código)_
**Horas acumuladas:** M0: 0h · total: 0h
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
- [ ] Criar a conta Stripe e ativar o Pix no Dashboard (modo teste)
- [ ] Gerar um pagamento Pix de teste e anotar quais eventos de webhook chegam (Q1)
- [ ] Criar a VM Ampere A1 na Oracle (ex.: 1 OCPU / 6 GB) e confirmar a capacidade na região (Q3)
- [ ] Confirmar os planos gratuitos: runners ARM do GitHub, R2/B2, monitor externo, healthchecks.io (Q4)

**Esqueleto:**
- [ ] Solution `KeycapStore` com os 4 projetos + 2 de testes, `Directory.Build.props`, `.editorconfig`
- [ ] Um teste unitário e um de integração (Testcontainers) passando
- [ ] Workflow de CI com os 5 itens + proteção de branch sem bypass
- [ ] Dockerfile ARM64 + `compose.yaml` (PostgreSQL sem porta, app em `127.0.0.1`)
- [ ] Script de reconstrução da VM (Docker, Nginx, Certbot, usuário `deploy`, hardening, `unattended-upgrades`), executado de verdade
- [ ] Subdomínio DuckDNS + HTTPS
- [ ] CD: imagem no GHCR, Environment `production` com aprovação, `deploy.sh` com comando forçado
- [ ] Página "Olá" publicada via CD

> **Plano B do M0:** se passar de 2 semanas, trocar o CD completo por um deploy manual simples e mover o endurecimento para o M6.

## Próximos passos

1. Ler `docs/DEFINICOES.md` e os ADRs em `docs/decisoes/` e **reescrever com suas palavras** os que não conseguir explicar (mitigação do risco R2).
2. Fazer o spike de premissas (acima).
3. Criar a solution e o primeiro teste.

## Bloqueios

_Nenhum._

## Questões em aberto

- Q1: eventos emitidos pelo Checkout com Pix (spike)
- Q2: conta Stripe + Pix ativado (spike)
- Q3: capacidade A1 na região (spike)
- Q4: condições dos planos gratuitos (spike)
- Q5: prazo legal de retenção dos pedidos (produção, não é código)

## Diário de sessões

### 29/09/2026: definição do projeto
- Entrevista de arquitetura concluída; `docs/DEFINICOES.md` gerado.
- Estrutura de contexto criada: `CLAUDE.md`, `.claude/rules/`, `/retomar`, `/encerrar`, subagente `revisor`, `ARQUITETURA.md`, ADRs.
- Nenhum código ainda.

## Histórico

_(sessões antigas resumidas em uma linha cada)_
