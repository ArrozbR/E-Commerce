# 0054. "Cancelar o pagamento" confere a sessão na Stripe quando ela recusa expirar

- **Status:** Aceito
- **Data:** 2026-11-11
- **Relacionado:** ADR 0044 (cancelar o pagamento), ADR 0037 (conciliação com a Stripe), `.claude/rules/pagamentos.md` §1

## Contexto

O "Cancelar o pagamento" (ADR 0044) pede à Stripe que expire a sessão. Quando a Stripe recusava, a loja dizia "tente de novo em alguns minutos". Mas, pela documentação conferida em 11/11, só uma sessão **`open`** pode ser expirada, e a Stripe devolve erro se ela "já expirou ou não está num estado que possa expirar".

Nesses casos, tentar de novo nunca funciona, e o pedido fica "Aguardando pagamento" até a conciliação, que só confere pedidos parados há mais de 40 minutos. Isso foi visto em 09/11 com um pedido do PC que perdeu o aviso de expiração.

## Decisão

1. Quando a Stripe recusa expirar a sessão, a loja **consulta a sessão desse pedido na Stripe** e aplica **a mesma transição da conciliação** (`CheckoutSessionTransitions`, ADR 0037), na hora e só para esse pedido:
   - **sessão expirada** → `Expire()` e o estoque devolvido. A mensagem diz que o prazo já tinha acabado e que dá para fazer outro pedido;
   - **sessão paga** → o caminho do pagamento, com a conferência de valor e moeda. A mensagem diz que o pedido já foi pago;
   - **sessão ainda aberta, ou a Stripe fora do ar até para a consulta** → continua o "tente de novo em alguns minutos".
2. A conciliação ganhou `ReconcileOrderAsync(orderId)`, que confere um pedido só; o laço de 10 em 10 minutos passou a usar o mesmo método, sem mudar o comportamento.
3. A exceção da conciliação no `pagamentos.md` §1 passa a valer também para essa conferência pontual: **o estado continua vindo só da resposta da Stripe sobre a sessão do pedido**, pelo mesmo código do webhook.

## Alternativas consideradas

- **Continuar esperando a conciliação:** o cliente fica até 50 minutos sem conseguir fazer outro pedido (a regra de 1 pedido aberto por cliente, ADR 0044).
- **Marcar o pedido como expirado direto, sem consultar:** a recusa da Stripe não diz o motivo; a sessão podia ter sido **paga**, e expirar um pedido pago seria errado.

## Consequências

- **Positivas:** o "Cancelar o pagamento" não manda mais tentar algo que nunca vai funcionar; o pedido se resolve na hora, pela palavra da Stripe.
- **Negativas / custos:** uma consulta a mais à Stripe quando a expiração falha; mais um ponto, além do webhook e da conciliação, que aplica as mesmas transições (sempre pelo mesmo código).
- **Como saber se deu errado:** um pedido expirado ou pago por essa conferência sem a sessão estar nesse estado na Stripe; aviso "um evento do webhook se perdeu ou ainda não chegou" com origem "Cancelar o pagamento" aparecendo com frequência.
