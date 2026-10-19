# 0031. Cancelamento pelo cliente antes do envio, com reembolso automático

- **Status:** Aceito
- **Data:** 2026-10-19
- **Relacionado:** ADR 0025 (v2: parte legal primeiro), `docs/DEFINICOES.md` D17, §7; `.claude/rules/pagamentos.md` §1, §4 e §5; ADR 0028 (página de envio)

## Contexto

A v2 começa pela parte legal (ADR 0025). Das três peças (cancelamento, direito de arrependimento e exclusão de conta), o cancelamento é a menor e reaproveita o reembolso automático da v1 (`refund-{orderId}`, job a cada minuto).

Dois problemas apareceram no desenho:
1. As regras de pagamento diziam que **nenhuma tela** altera estado de pagamento. Um cancelamento pelo cliente muda o pedido e gera um reembolso.
2. O reembolso precisa do id do pagamento (`pi_...`). A v1 só guarda esse id em pedidos `PaidOutOfStock`. Os pedidos pagos normais, inclusive os de produção, não o têm.

## Decisão

1. **Quem e quando:** **só o dono do pedido** pode cancelar, enquanto o pedido estiver **`Paid` ou `Picking`**, ou seja, até o admin marcar o envio. Depois de `Shipped`, o caminho é o direito de arrependimento (próxima peça da v2). Pedidos `AwaitingPayment` não são canceláveis por aqui: a sessão da Stripe expira sozinha e devolve o estoque.
2. **Novo estado `Cancelled`**, com a transição `Order.Cancel()` de `Paid`/`Picking`. Pedido inválido para cancelar → `InvalidOrderTransitionException` → **409**. Pedido de outro cliente → **404**.
3. **Na mesma transação do cancelamento**, o estoque é **devolvido** (`StockRelease`). O produto ainda está na loja e volta a ficar disponível na hora.
4. **O reembolso fica fora da requisição**, como na v1. O job que reembolsa `PaidOutOfStock` passa a reembolsar também `Cancelled`, com a mesma chave `refund-{orderId}`. `MarkRefunded()` passa a aceitar `PaidOutOfStock` **ou** `Cancelled` → `Refunded`. Falha da Stripe → continua `Cancelled` + `LogError` + nova tentativa.
5. **Id do pagamento:**
   - daqui em diante, o webhook guarda o `pi_...` em **toda** confirmação de pagamento;
   - para pedidos antigos sem ele, o job pergunta à Stripe pela **sessão** (`cs_...`, que todo pedido pago tem) e descobre o `pi_...` na hora.
6. **Ajuste da regra "nenhuma tela altera pagamento":** continua valendo para **confirmar, expirar, falhar e marcar sem estoque**, que só o webhook decide. O **cancelamento** é um pedido do **cliente**, não um fato vindo da Stripe, e só cancela; o dinheiro volta pelo job, pela API da Stripe, com chave de idempotência. Nenhuma tela marca `Refunded` diretamente.

## Alternativas consideradas

- **Cancelar só enquanto `Paid`:** mais simples para o admin, mas mais restritivo para o cliente com o produto ainda na loja.
- **Reembolsar dentro da requisição do cancelamento:** a página ficaria esperando a Stripe, e uma falha dela viraria erro para o cliente. O job já resolve isso desde a v1.
- **Guardar o `pi_...` só daqui em diante:** os pedidos pagos que já existem em produção nunca poderiam ser cancelados.
- **Cancelar também `AwaitingPayment`:** exigiria expirar a sessão na Stripe pela API. A expiração automática (31 min) já cobre o caso.

## Consequências

- **Positivas:** o cliente cancela sozinho, o estoque volta na hora, e o reembolso reaproveita o caminho já testado da v1, inclusive com um reembolso real no sandbox.
- **Negativas / custos:** um estado novo (`Cancelled`), uma chamada nova à Stripe (buscar a sessão) e a regra de pagamento ajustada.
- **Corrida conhecida:** o cliente cancela no mesmo instante em que o admin marca o envio. O token de concorrência garante que só um dos dois vale; o outro recebe erro, e os dados ficam certos.
- **Como saber se deu errado:** um pedido `Cancelled` parado por mais de alguns minutos sem virar `Refunded` (o `LogError` do job mostra o motivo); um pedido `Shipped` sendo cancelado.
