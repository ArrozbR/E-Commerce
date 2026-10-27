# 0037. Conciliação com a Stripe: pedidos parados conferidos a cada 10 minutos e corrigidos pelo mesmo caminho do webhook

- **Status:** Aceito
- **Data:** 2026-10-27
- **Relacionado:** ADR 0004 (webhook como fonte da verdade), ADR 0005 (idempotência e concorrência no banco), ADR 0025 (roadmap da v2), ADR 0026 (validade da sessão), ADR 0027 (eventos snapshot); `.claude/rules/pagamentos.md` §1 e §5; `docs/DEFINICOES.md` §3

## Contexto

Item 3 da segunda parte da v2. O webhook assinado continua sendo a fonte da verdade (ADR 0004), mas um aviso pode **nunca chegar**:
- em 12/10, três pedidos foram **pagos** e ficaram em "Aguardando pagamento", porque os eventos nasceram antes de existir o destino (e na versão `dahlia`, recusada pelo webhook, ADR 0027). Foram corrigidos à mão em 15/10;
- a Stripe tenta entregar um evento por até 3 dias em produção (3 vezes em poucas horas na área restrita, documentação de webhooks, conferida em 27/10). Uma queda mais longa, um destino desativado ou um segredo trocado errado perdem eventos.

Um aviso perdido deixa o pedido parado nos dois sentidos: **pago sem a loja saber** (o cliente pagou e não recebe nada) ou **expirado sem a loja saber** (o estoque fica reservado para sempre).

Conferido na documentação oficial da Stripe (27/10):
- a sessão do Checkout tem `status` (`open`, `complete`, `expired`; de `expired`: "No further processing will occur") e `payment_status` (`paid`, `unpaid`), lidos com `GET /v1/checkout/sessions/{id}`;
- limites: 25 requisições por segundo na área restrita (100 em produção); leituras em média de até 500 por transação, com **mínimo de 10.000 por mês**.

## Decisão

1. **O que é conferido:** só pedidos **parados**: `AwaitingPayment` ou `AwaitingConfirmation`, **com** `StripeSessionId`, criados há mais de **40 minutos** (a sessão expira em criação + 31 min, ADR 0026; 9 min de folga para o webhook chegar). Pedidos sem sessão continuam com o job do D35. Pedidos já pagos (reembolsos ou contestações feitos fora da loja) **não** entram (decisão do autor).
2. **Frequência:** a cada **10 minutos**, como mais uma tarefa do `OrderMaintenanceJob` (no seu próprio `try`, como as outras), com um limite de pedidos por rodada para nunca encostar nos limites da API.
3. **O que faz com uma divergência: corrige sozinha** (decisão do autor), pelo **mesmo código** que o webhook usa, nunca por uma cópia:
   - sessão `paid` → a mesma transição do `checkout.session.completed` pago, **incluindo a conferência de `amount_total` e `currency`** e o caminho do pagamento atrasado (reservar de novo → `Paid` ou `PaidOutOfStock`);
   - sessão `expired` → a mesma transição do `checkout.session.expired` (`Expire()` + estoque devolvido na mesma transação);
   - sessão `open` depois de 40 min (não deveria acontecer) ou `complete` com `unpaid` (pagamento ainda em processamento) → não muda nada; só registra no log.
   Para isso, a lógica de cada transição sai do `StripeWebhookHandler` para uma peça usada pelos dois (refatoração sem mudar comportamento, como o `StockRelease` no M4).
4. **Exceção na regra 1 do `pagamentos.md`:** além do webhook, a conciliação pode levar um pedido parado a `Paid`, `PaidOutOfStock` ou `Expired`, **somente** a partir do que a API da Stripe responder sobre a sessão do próprio pedido. Ela nunca decide sozinha que um pedido foi pago.
5. **Corrida com o webhook:** se o evento chegar ao mesmo tempo, o token de concorrência do `Order` (ADR 0005) deixa só um vencer; o outro encontra o pedido já resolvido e não faz nada (o webhook já ignora transições repetidas). Na conciliação, a concorrência descarta aquela rodada para o pedido, que é conferido de novo na próxima.
6. **Log:** toda correção vira um **aviso** (Warning) com o `orderId` e o novo estado, porque significa que um evento se perdeu e alguém deveria descobrir por quê. Nunca nome, e-mail ou endereço.

## Alternativas consideradas

- **Só avisar no log** e corrigir à mão (como em 12/10): mais simples e sem segunda porta, mas depende de alguém ler o log, e o cliente que pagou espera.
- **Conferir também os pedidos pagos** (reembolsos e contestações abertos no Dashboard): mais chamadas e decisões novas (o que fazer com uma contestação). Seria quase outro item; fica para depois, se fizer falta.
- **A cada 1 minuto:** resolve mais rápido, mas reconsulta a cada minuto um pedido que continue parado (por exemplo, um `AwaitingConfirmation`).
- **A cada 1 hora:** menos chamadas, mas o cliente que pagou pode esperar até cerca de 1h30.
- **Listar eventos não entregues** (`/v1/events`) em vez de consultar as sessões: depende da versão dos eventos (o problema de 12/10 era justamente a versão) e de guardar até onde já se leu. Perguntar pelo estado atual da sessão é mais simples e independe da versão.

## Consequências

- **Positivas:** um pedido pago nunca fica preso por um aviso perdido (no pior caso, cerca de 50 min depois da compra); o estoque de um pedido expirado sem aviso volta sozinho; as regras de pagamento continuam num lugar só.
- **Negativas / custos:** uma segunda porta para mudar estado de pagamento (limitada pelo item 4); chamadas à API da Stripe a partir do job (poucas: normalmente nenhum pedido está parado); a Stripe fora só adia a conciliação para a próxima rodada.
- **Como saber se deu errado:** avisos de conciliação aparecendo com frequência (eventos estão se perdendo: conferir o destino no Dashboard); um pedido marcado como pago pela conciliação com valor diferente do cobrado; um pedido parado há mais de 1 hora com sessão paga ou expirada.
