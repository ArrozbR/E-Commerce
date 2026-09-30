# 0005. Idempotência e concorrência garantidas pelo banco

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D10, D11; `.claude/rules/pagamentos.md` §3

## Contexto

A Stripe pode entregar o mesmo evento mais de uma vez, inclusive **ao mesmo tempo** (em paralelo). Dois processamentos que primeiro consultam "já processei?" e depois gravam recebem "não" ao mesmo tempo, e aplicam o efeito duas vezes (por exemplo, estoque baixado em dobro). A aplicação pode rodar em mais de um processo (durante um deploy, ou com várias instâncias).

## Decisão

- Idempotência por **identificador exato**: o `event.id` é inserido em `ProcessedStripeEvents` com restrição **UNIQUE**, **na mesma transação** da mudança de estado. A violação de unicidade é tratada como duplicata → `200` sem efeito.
- **Checagem de estado** como segunda barreira: transições condicionais (concorrência otimista via token de concorrência do EF Core).
- Chamadas à Stripe com **chaves de idempotência** (`session-{orderId}`, `refund-{orderId}`).
- Falha no meio da transação → rollback completo → `500`, e a Stripe tenta de novo.

## Alternativas consideradas

- **Comparar pedidos "parecidos" dentro de uma janela de tempo:** gera falso positivo (duas compras idênticas legítimas) e falso negativo (reenvio dias depois).
- **Lock em memória (`lock`, `SemaphoreSlim`, `Mutex`):** só funciona dentro de um processo. Passa nos testes locais e falha em produção de forma intermitente.
- **Lock distribuído com Redis:** funciona, mas acrescenta um serviço (e custo) para resolver um problema que o banco já resolve.

## Consequências

- **Positivas:** correto com qualquer número de instâncias; sem infraestrutura nova.
- **Negativas:** exige entender transações e tratar a exceção de unicidade do provedor (atrás de uma abstração na `Infrastructure`).
- **Como saber se deu errado:** o teste de integração "mesmo evento em paralelo" falhar; estoque divergente do número de pedidos pagos.

> Frase para a entrevista: "a garantia de unicidade mora no banco, não na aplicação".
