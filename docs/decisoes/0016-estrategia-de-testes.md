# 0016. Estratégia de testes focada no risco

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D27, §10.1; `.claude/rules/testes.md`

## Contexto

Os bugs mais graves deste sistema não estão em formulários: estão em **eventos duplicados, concorrência, ordem dos eventos e falhas no meio de uma transação**. Esses bugs só aparecem com um banco real, porque são as restrições UNIQUE e as transações que garantem a correção.

## Decisão

- **Unitários (muitos):** `Domain` e `Application` com fakes, cobrindo todas as transições (válidas e inválidas), a decisão por `payment_status`, a divergência de valor e o reembolso por falta de estoque.
- **Integração (camada principal):** `WebApplicationFactory` + **PostgreSQL real via Testcontainers**, com eventos **assinados localmente**. Cobre assinatura inválida, duplicata em sequência e **em paralelo**, reserva concorrente do último item, expiração duplicada, eventos fora de ordem e rollback.
- **Ponta a ponta (poucos, manuais):** Stripe em modo teste + Stripe CLI, como checklist antes de aprovar cada deploy.
- Ferramentas: xUnit, Testcontainers, Shouldly ou `Assert`. Sem FluentAssertions (licença comercial nas versões novas). Sem mock de banco.

## Alternativas consideradas

- **Ponta a ponta como garantia principal:** lento, frágil e dependente de rede; acaba testando só o caminho feliz.
- **Banco em memória (EF InMemory, SQLite):** não reproduz UNIQUE, transações e concorrência do PostgreSQL. Daria falsa confiança.

## Consequências

- **Positivas:** os riscos centrais ficam cobertos de forma automática e determinística no CI.
- **Negativas:** os testes de integração exigem Docker e são mais lentos (minutos).
- **Como saber se deu errado:** um bug de pagamento em produção que nenhum teste de integração pegou; testes que falham aleatoriamente sendo ignorados.
