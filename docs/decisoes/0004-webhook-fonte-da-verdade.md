# 0004. O webhook assinado é a única fonte da verdade do pagamento

- **Status:** Aceito
- **Data:** 2026-09-29
- **Relacionado:** `docs/DEFINICOES.md` D09, D13; `.claude/rules/pagamentos.md` §1–§2

## Contexto

O pagamento acontece entre a Stripe e o banco, fora do sistema. A aplicação precisa saber o resultado de forma confiável, mesmo que o cliente feche a aba, perca a conexão, ou o servidor esteja fora do ar no momento do pagamento.

## Decisão

- O estado de pagamento de um pedido **só muda pelo handler do webhook** (`POST /webhooks/stripe`), depois de **verificar a assinatura** com o corpo bruto e o segredo `whsec_...`.
- A página de retorno **apenas consulta o próprio backend** e nunca altera estado.
- O webhook **confere `amount_total` e `currency`** contra o pedido antes de confirmar.
- O `order_id` vai em `metadata` e em `client_reference_id` da sessão (para conciliação e para reconstruir pedidos a partir da Stripe, se for preciso).
- Consultar a API da Stripe (polling) fica só como rede de segurança (job de conciliação, fora da v1).

## Alternativas consideradas

- **Confiar no redirecionamento (`success_url`):** um cliente que fecha a aba deixa o pedido pendente para sempre, e qualquer pessoa consegue forjar a URL de sucesso.
- **Polling na Stripe como mecanismo principal:** se ninguém consultar, ninguém fica sabendo. Gera carga e atraso.

## Consequências

- **Positivas:** é confiável mesmo com o cliente offline; a Stripe reenvia por até cerca de 3 dias se o servidor falhar. É o padrão da indústria.
- **Negativas:** os eventos são **assíncronos, e podem chegar duplicados, atrasados ou fora de ordem**. Isso exige idempotência (ADR 0005). O teste local depende da Stripe CLI.
- **Como saber se deu errado:** pedidos pagos na Stripe e pendentes no sistema; alertas de falha de webhook enviados pela Stripe.
