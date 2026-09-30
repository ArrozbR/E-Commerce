---
paths:
  - "tests/**"
  - "**/*Tests.cs"
  - ".github/workflows/**"
---

# Regras de testes

Estratégia completa em `docs/DEFINICOES.md` §10.1 e no ADR 0016.

## Princípio

**Teste onde está o risco, não onde está o formulário.** Antes de escrever um teste, responda: "de que jeito isso quebra?" (duplicata, concorrência, ordem dos eventos, estado inválido, falha no meio da operação).

## Projetos

| Projeto | Conteúdo | Dependências externas |
|---|---|---|
| `tests/KeycapStore.UnitTests` | `Domain`, `Application` (com fakes), testes de arquitetura | nenhuma |
| `tests/KeycapStore.IntegrationTests` | API em memória (`WebApplicationFactory`) + PostgreSQL real (Testcontainers) | Docker |

Os testes ponta a ponta com a Stripe real **não são automatizados no CI**. Eles são um checklist manual (`docs/DEFINICOES.md` §10.1), executado antes de aprovar o deploy.

## Ferramentas

- **xUnit**, **Testcontainers** (PostgreSQL), **`WebApplicationFactory`**.
- Asserções: `Assert` do xUnit ou **Shouldly**. **Não usar FluentAssertions** (licença comercial nas versões novas).
- **Não mockar o banco** nos testes de integração. Nada de EF InMemory nem SQLite para simular o PostgreSQL: as garantias de UNIQUE, transação e concorrência só existem no banco real.
- A Stripe é substituída por uma implementação falsa de `IPaymentGateway`. Os eventos de webhook são **assinados localmente** com um segredo de teste, para exercitar a verificação de assinatura real.

## Convenções

- Nome do teste: `Method_Scenario_ExpectedResult` (ex.: `ConfirmPayment_WhenAlreadyPaid_DoesNothing`).
- Estrutura Arrange / Act / Assert, com um comportamento por teste.
- Dados de teste por fábricas ou builders (`OrderBuilder.Paid()`), nunca por cópia e cola.
- Testes de concorrência usam `Task.WhenAll` com requisições **realmente paralelas**, e verificam o **efeito no banco** (estoque, quantidade de eventos processados), não só o status HTTP.

## Obrigatórios

- Toda transição da máquina de estados: o caminho válido **e** pelo menos um caminho inválido.
- Todo cenário de integração listado em `.claude/rules/pagamentos.md` §8 quando o fluxo de pagamento mudar.
- Todo bug corrigido ganha um teste que falhava antes da correção.

## CI

O merge na `main` é bloqueado se falhar qualquer um destes: build (avisos como erro), testes unitários, testes de integração, gitleaks, `dotnet format --verify-no-changes`.

**Teste que falha aleatoriamente é consertado imediatamente.** Nunca rodar de novo até passar, nunca marcar como `Skip` sem um item no `docs/STATUS.md`.
